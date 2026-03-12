using Latios.Calligraphics.HarfBuzz;
using static Unity.Entities.SystemAPI;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Latios.Calligraphics.Systems
{
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(UpdateGlyphsRenderersSystem))]
    public unsafe partial class DispatchGlyphsSystem : SystemBase
    {
        const int kTextureDimension = 4096;
        const int kShelfAlignment   = 16;

        // Todo: Figure out if there are any platform differences to compensate for.
        static readonly bool kEnableComputePixelUpload = true;
        static readonly bool kComputePixelUploadFlipY  = false;

        EntityQuery m_query;

        UnityObjectRef<ComputeShader> m_uploadGlyphsShader;
        UnityObjectRef<ComputeShader> m_copyBytesShader;
        UnityObjectRef<ComputeShader> m_uploadPixelsShader;

        PersistentBuffer         m_glyphsBuffer;
        GraphicsBufferUploadPool m_glyphUploadBuffers;
        GraphicsBufferUploadPool m_glyphMetaUploadBuffers;
        GraphicsBufferUploadPool m_pixelUploadBuffers;
        GraphicsBufferUploadPool m_pixelUploadMetaBuffers;

        TextureAtlasArray<byte>    m_sdf8Array;
        TextureAtlasArray<ushort>  m_sdf16Array;
        TextureAtlasArray<Color32> m_bitmapArray;

        DrawDelegates  m_drawDelegates;
        PaintDelegates m_paintDelegates;

        // Shader bindings
        int _src;
        int _dst;
        int _startOffset;
        int _meta;
        int _flipOffset;

        int _tmdSdf8;
        int _tmdSdf16;
        int _tmdBitmap;
        int _tmdGlyphs;

        AtlasTable    m_atlasToDestroy;
        GlyphGpuTable m_glyphGpuTableToDestroy;

        protected override void OnCreate()
        {
            ref var state = ref CheckedStateRef;

            m_query = QueryBuilder().WithAll<MaterialMeshInfo>().WithAllRW<GpuState>().WithPresent<PreviousRenderGlyph>().WithPresentRW<ResidentRange>().Build();

            m_uploadGlyphsShader = Resources.Load<ComputeShader>("UploadGlyphs");
            m_copyBytesShader    = Resources.Load<ComputeShader>("CopyBytes");
            if (kEnableComputePixelUpload)
            {
                m_uploadPixelsShader     = Resources.Load<ComputeShader>("UploadPixels");
                m_pixelUploadBuffers     = new GraphicsBufferUploadPool(1024 * 4, GraphicsBuffer.Target.Raw, 4);
                m_pixelUploadMetaBuffers = new GraphicsBufferUploadPool(1024, GraphicsBuffer.Target.Raw, 4);
            }

            m_glyphsBuffer           = new PersistentBuffer(1024 * 16 * 128, 4, GraphicsBuffer.Target.Raw, m_copyBytesShader);
            m_glyphUploadBuffers     = new GraphicsBufferUploadPool(1024 * 8 * 4, GraphicsBuffer.Target.Raw, 4);
            m_glyphMetaUploadBuffers = new GraphicsBufferUploadPool(1024, GraphicsBuffer.Target.Raw, 4);

            _src         = Shader.PropertyToID("_src");
            _dst         = Shader.PropertyToID("_dst");
            _startOffset = Shader.PropertyToID("_startOffset");
            _meta        = Shader.PropertyToID("_meta");
            _flipOffset  = Shader.PropertyToID("_flipOffset");

            _tmdSdf8        = Shader.PropertyToID("_tmdSdf8");
            _tmdSdf16       = Shader.PropertyToID("_tmdSdf16");
            _tmdBitmap      = Shader.PropertyToID("_tmdBitmap");
            _tmdGlyphs      = Shader.PropertyToID("_tmdGlyphs");
            var dummyBuffer = m_glyphsBuffer.GetBuffer(0);
            Shader.SetGlobalBuffer(_tmdGlyphs, dummyBuffer);  // fix unbound _tmdGlyphs buffer issue

            var initialAtlasArraySize = kEnableComputePixelUpload ? 1 : 2;  // RenderTexture supports array size 1
            m_sdf8Array               = new TextureAtlasArray<byte>(_tmdSdf8, kTextureDimension, initialAtlasArraySize, TextureFormat.R8, false, true, kEnableComputePixelUpload);
            m_sdf16Array              =
                new TextureAtlasArray<ushort>(_tmdSdf16, kTextureDimension, initialAtlasArraySize, TextureFormat.R16, false, true, kEnableComputePixelUpload);
            m_bitmapArray = new TextureAtlasArray<Color32>(_tmdBitmap,
                                                           kTextureDimension,
                                                           initialAtlasArraySize,
                                                           TextureFormat.RGBA32,
                                                           true,
                                                           false,
                                                           kEnableComputePixelUpload);

            m_drawDelegates  = new DrawDelegates(true);
            m_paintDelegates = new PaintDelegates(true);

            var atlas        = new AtlasTable(Allocator.Persistent, kTextureDimension, kShelfAlignment);
            m_atlasToDestroy = atlas;
            EntityManager.CreateSingleton(atlas);
            var glyphGpuTable = new GlyphGpuTable
            {
                bufferSize          = new NativeReference<uint>(Allocator.Persistent, NativeArrayOptions.ClearMemory),
                dispatchDynamicGaps = new NativeList<uint2>(Allocator.Persistent),
                residentGaps        = new NativeList<uint2>(Allocator.Persistent)
            };
            m_glyphGpuTableToDestroy = glyphGpuTable;
            EntityManager.CreateSingleton(glyphGpuTable);
        }

        protected override void OnUpdate()
        {
            ref var state     = ref CheckedStateRef;
            var     collected = Collect(ref state);
            state.CompleteDependency();
            var written = Write(ref state, ref collected);
            state.CompleteDependency();
            Dispatch(ref state, ref written);
        }

        protected override void OnDestroy()
        {
            ref var state = ref CheckedStateRef;

            GraphicsBuffer b = null;
            Shader.SetGlobalBuffer(_tmdGlyphs, b);
            Texture2DArray t = null;
            Shader.SetGlobalTexture(_tmdSdf8,   t);
            Shader.SetGlobalTexture(_tmdSdf16,  t);
            Shader.SetGlobalTexture(_tmdBitmap, t);

            m_sdf8Array.Dispose();
            m_sdf16Array.Dispose();
            m_bitmapArray.Dispose();

            m_drawDelegates.Dispose();
            m_paintDelegates.Dispose();

            m_atlasToDestroy.TryDispose(default);
            m_glyphGpuTableToDestroy.TryDispose(default);

            m_glyphsBuffer.Dispose();
            m_glyphUploadBuffers.Dispose();
            m_glyphMetaUploadBuffers.Dispose();

            if (kEnableComputePixelUpload)
            {
                m_pixelUploadBuffers.Dispose();
                m_pixelUploadMetaBuffers.Dispose();
            }
        }

        public CollectState Collect(ref SystemState state)
        {
            var glyphTable    = SystemAPI.GetSingletonRW<GlyphTable>().ValueRW;
            var glyphGpuTable = SystemAPI.GetSingletonRW<GlyphGpuTable>().ValueRW;
            var atlasTable    = SystemAPI.GetSingletonRW<AtlasTable>().ValueRW;

            var glyphEntryIDsToRasterizeSet = new NativeParallelHashSet<uint>(1, state.WorldUpdateAllocator);
            var allocateJh                  = new AllocateJob
            {
                glyphTable                  = glyphTable,
                glyphEntryIDsToRasterizeSet = glyphEntryIDsToRasterizeSet,
            }.Schedule(state.Dependency);

            var chunkCount                = m_query.CalculateChunkCountWithoutFiltering();
            var renderGlyphCapturesStream = new NativeStream(chunkCount, state.WorldUpdateAllocator);
            var captureJh                 = new CaptureRenderGlyphsJob
            {
                glyphEntryIDsToRasterizeSet = glyphEntryIDsToRasterizeSet.AsParallelWriter(),
                glyphTable                  = glyphTable,
                gpuStateHandle              = GetComponentTypeHandle<GpuState>(false),
                renderGlyphCapturesStream   = renderGlyphCapturesStream.AsWriter(),
                renderGlyphHandle           = GetBufferTypeHandle<PreviousRenderGlyph>(true),
                residentRangeHandle         = GetComponentTypeHandle<ResidentRange>(false),
                textShaderIndexHandle       = GetComponentTypeHandle<TextShaderIndex>(false),
            }.ScheduleParallel(m_query, allocateJh);

            var captures = new NativeList<RenderGlyphCapture>(state.WorldUpdateAllocator);
            var assignJh = new AssignShaderIndicesJob
            {
                captures                  = captures,
                glyphGpuTable             = glyphGpuTable,
                renderGlyphCapturesStream = renderGlyphCapturesStream
            }.Schedule(captureJh);

            var glyphEntryIDsToRasterize  = new NativeList<uint>(state.WorldUpdateAllocator);
            var atlasDirtyIDs             = new NativeList<uint>(state.WorldUpdateAllocator);
            var pixelUploadOffsetsInBytes = new NativeList<int>(state.WorldUpdateAllocator);
            var pixelBytesCount           = new NativeReference<int>(state.WorldUpdateAllocator);
            var atlasJh                   = new AllocateGlyphsInAtlasJob
            {
                atlasDirtyIDs               = atlasDirtyIDs,
                atlasTable                  = atlasTable,
                glyphEntryIDsToRasterize    = glyphEntryIDsToRasterize,
                glyphEntryIDsToRasterizeSet = glyphEntryIDsToRasterizeSet,
                glyphTable                  = glyphTable,
                pixelUploadOffsetsInBytes   = pixelUploadOffsetsInBytes,
                pixelBytesCount             = pixelBytesCount,
                enableAtlasGC               = true
            }.Schedule(captureJh);

            state.Dependency = JobHandle.CombineDependencies(assignJh, atlasJh);

            return new CollectState
            {
                atlasDirtyIDs             = atlasDirtyIDs,
                glyphEntryIDsToRasterize  = glyphEntryIDsToRasterize,
                glyphsToUpload            = captures,
                pixelUploadOffsetsInBytes = pixelUploadOffsetsInBytes,
                pixelBytesCount           = pixelBytesCount,
            };
        }

        public WriteState Write(ref SystemState state, ref CollectState collected)
        {
            WriteState writeState = default;

            if (collected.glyphsToUpload.IsEmpty && collected.glyphEntryIDsToRasterize.IsEmpty)
                return writeState;

            var glyphTable = SystemAPI.GetSingleton<GlyphTable>();
            var fontTable  = SystemAPI.GetSingleton<FontTable>();

            var rasterizeJh    = state.Dependency;
            var uploadGlyphsJh = rasterizeJh;

            if (!collected.glyphEntryIDsToRasterize.IsEmpty)
            {
                int dirtySdf8Count;
                for (dirtySdf8Count = 0; dirtySdf8Count < collected.atlasDirtyIDs.Length; dirtySdf8Count++)
                {
                    var dirtyId = collected.atlasDirtyIDs[dirtySdf8Count];
                    if (dirtyId >= 0x40000000u)
                        break;
                }
                int dirtySdf16Count;
                for (dirtySdf16Count = dirtySdf8Count; dirtySdf16Count < collected.atlasDirtyIDs.Length; dirtySdf16Count++)
                {
                    var dirtyId = collected.atlasDirtyIDs[dirtySdf16Count];
                    if (dirtyId >= 0x80000000u)
                        break;
                }
                dirtySdf16Count      -= dirtySdf8Count;
                var dirtyBitmapCount  = collected.atlasDirtyIDs.Length - dirtySdf8Count - dirtySdf16Count;

                var sdf8Ptrs = CollectionHelper.CreateNativeArray<TextureAtlasArray<byte>.AtlasPtr>(dirtySdf8Count,
                                                                                                    state.WorldUpdateAllocator,
                                                                                                    NativeArrayOptions.UninitializedMemory);
                var sdf16Ptrs = CollectionHelper.CreateNativeArray<TextureAtlasArray<ushort>.AtlasPtr>(dirtySdf16Count,
                                                                                                       state.WorldUpdateAllocator,
                                                                                                       NativeArrayOptions.UninitializedMemory);
                var bitmapPtrs = CollectionHelper.CreateNativeArray<TextureAtlasArray<Color32>.AtlasPtr>(dirtyBitmapCount,
                                                                                                         state.WorldUpdateAllocator,
                                                                                                         NativeArrayOptions.UninitializedMemory);

                if (dirtySdf8Count > 0)
                {
                    m_sdf8Array.GetAtlasPtrsForDirtyIndices(collected.atlasDirtyIDs.AsArray().GetSubArray(0, dirtySdf8Count).AsSpan(), sdf8Ptrs.AsSpan());
                    writeState.isSdf8Dirty = true;
                }
                if (dirtySdf16Count > 0)
                {
                    m_sdf16Array.GetAtlasPtrsForDirtyIndices(collected.atlasDirtyIDs.AsArray().GetSubArray(dirtySdf8Count, dirtySdf16Count).AsSpan(), sdf16Ptrs.AsSpan());
                    writeState.isSdf16Dirty = true;
                }
                if (dirtyBitmapCount > 0)
                {
                    m_bitmapArray.GetAtlasPtrsForDirtyIndices(collected.atlasDirtyIDs.AsArray().GetSubArray(dirtySdf8Count + dirtySdf16Count, dirtyBitmapCount).AsSpan(),
                                                              bitmapPtrs.AsSpan());
                    writeState.isBitmapDirty = true;
                }

                GraphicsBuffer     uploadBuffer     = default;
                GraphicsBuffer     uploadMetaBuffer = default;
                NativeArray<byte>  uploadArray;
                NativeArray<uint4> uploadMetaArray;
                if (kEnableComputePixelUpload)
                {
                    uploadBuffer     = m_pixelUploadBuffers.Allocate(collected.pixelBytesCount.Value / 4);
                    uploadArray      = uploadBuffer.LockBufferForWrite<byte>(0, collected.pixelBytesCount.Value);
                    uploadMetaBuffer = m_pixelUploadMetaBuffers.Allocate(collected.glyphEntryIDsToRasterize.Length * 4);
                    uploadMetaArray  = uploadMetaBuffer.LockBufferForWrite<uint4>(0, collected.glyphEntryIDsToRasterize.Length);
                }
                else
                {
                    uploadArray     = CollectionHelper.CreateNativeArray<byte>(1, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);
                    uploadMetaArray = CollectionHelper.CreateNativeArray<uint4>(1, state.WorldUpdateAllocator, NativeArrayOptions.UninitializedMemory);
                }
                rasterizeJh = new RasterizeJob
                {
                    bitmapPtrs                = bitmapPtrs,
                    drawDelegates             = m_drawDelegates,
                    fontTable                 = fontTable,
                    glyphEntryIDsToRasterize  = collected.glyphEntryIDsToRasterize.AsArray(),
                    glyphTable                = glyphTable,
                    paintDelegates            = m_paintDelegates,
                    sdf16Ptrs                 = sdf16Ptrs,
                    sdf8Ptrs                  = sdf8Ptrs,
                    pixelUploadOffsetsInBytes = collected.pixelUploadOffsetsInBytes.AsArray(),
                    uploadBuffer              = uploadArray,
                    uploadMetaBuffer          = uploadMetaArray,
                    useComputeUpload          = kEnableComputePixelUpload,
                    atomicPrioritizer         = new NativeReference<int>(0, state.WorldUpdateAllocator),
                }.ScheduleParallel(collected.glyphEntryIDsToRasterize.Length, 1, rasterizeJh);

                writeState.pixelUploadBuffer               = uploadBuffer;
                writeState.pixelUploadBufferWriteCount     = collected.pixelBytesCount.Value;
                writeState.pixelUploadMetaBuffer           = uploadMetaBuffer;
                writeState.pixelUploadMetaBufferWriteCount = collected.glyphEntryIDsToRasterize.Length;
            }
            if (!collected.glyphsToUpload.IsEmpty)
            {
                var lastCapture      = collected.glyphsToUpload[^ 1];
                var glyphCount       = lastCapture.writeStart + lastCapture.glyphCount;
                var uploadBuffer     = m_glyphUploadBuffers.Allocate(glyphCount * UnsafeUtility.SizeOf<RenderGlyph>() / 4);
                var uploadArray      = uploadBuffer.LockBufferForWrite<RenderGlyph>(0, glyphCount);
                var captureCount     = collected.glyphsToUpload.Length;
                var uploadMetaBuffer = m_glyphMetaUploadBuffers.Allocate(captureCount * 3);
                var uploadMetaArray  = uploadMetaBuffer.LockBufferForWrite<uint3>(0, captureCount);

                uploadGlyphsJh = new WriteRenderGlyphsToGpuJob
                {
                    captures        = collected.glyphsToUpload.AsArray(),
                    uploadArray     = uploadArray,
                    uploadMetaArray = uploadMetaArray,
                    glyphTable      = glyphTable
                }.ScheduleParallel(collected.glyphsToUpload.Length, 8, uploadGlyphsJh);

                writeState.glyphUploadBuffer               = uploadBuffer;
                writeState.glyphUploadBufferWriteCount     = glyphCount;
                writeState.glyphUploadMetaBuffer           = uploadMetaBuffer;
                writeState.glyphUploadMetaBufferWriteCount = captureCount;
            }

            state.Dependency = JobHandle.CombineDependencies(rasterizeJh, uploadGlyphsJh);
            return writeState;
        }

        public void Dispatch(ref SystemState state, ref WriteState written)
        {
            if (kEnableComputePixelUpload && (written.isSdf8Dirty || written.isSdf16Dirty || written.isBitmapDirty))
            {
                written.pixelUploadBuffer.UnlockBufferAfterWrite<byte>(written.pixelUploadBufferWriteCount);
                written.pixelUploadMetaBuffer.UnlockBufferAfterWrite<uint4>(written.pixelUploadMetaBufferWriteCount);

                var shader = m_uploadPixelsShader.Value;
                shader.SetTexture(0, _tmdSdf8,   m_sdf8Array.GetRenderTextureForUpload());
                shader.SetTexture(0, _tmdSdf16,  m_sdf16Array.GetRenderTextureForUpload());
                shader.SetTexture(0, _tmdBitmap, m_bitmapArray.GetRenderTextureForUpload());
                shader.SetBuffer(0, _src,  written.pixelUploadBuffer);
                shader.SetBuffer(0, _meta, written.pixelUploadMetaBuffer);
                shader.SetInt(_flipOffset, math.select(0, kTextureDimension - 1, kComputePixelUploadFlipY));
                for (uint dispatchesRemaining = (uint)written.pixelUploadMetaBufferWriteCount, offset = 0; dispatchesRemaining > 0;)
                {
                    uint dispatchCount = math.min(dispatchesRemaining, 65535);
                    shader.SetInt(_startOffset, (int)offset);
                    shader.Dispatch(0, (int)dispatchCount, 1, 1);
                    offset              += dispatchCount;
                    dispatchesRemaining -= dispatchCount;
                }
            }

            if (written.isSdf8Dirty)
                m_sdf8Array.ApplyChanges();
            if (written.isSdf16Dirty)
                m_sdf16Array.ApplyChanges();
            if (written.isBitmapDirty)
                m_bitmapArray.ApplyChanges();

            if (written.glyphUploadBufferWriteCount > 0)
            {
                var glyphGpuTable = SystemAPI.GetSingleton<GlyphGpuTable>();

                written.glyphUploadMetaBuffer.UnlockBufferAfterWrite<uint3>(written.glyphUploadMetaBufferWriteCount);
                written.glyphUploadBuffer.UnlockBufferAfterWrite<RenderGlyph>(written.glyphUploadBufferWriteCount);

                var persistentBuffer = m_glyphsBuffer.GetBuffer(glyphGpuTable.bufferSize.Value);
                var shader           = m_uploadGlyphsShader.Value;
                shader.SetBuffer(0, _dst,  persistentBuffer);
                shader.SetBuffer(0, _src,  written.glyphUploadBuffer);
                shader.SetBuffer(0, _meta, written.glyphUploadMetaBuffer);

                for (uint dispatchesRemaining = (uint)written.glyphUploadMetaBufferWriteCount, offset = 0; dispatchesRemaining > 0;)
                {
                    uint dispatchCount = math.min(dispatchesRemaining, 65535);
                    shader.SetInt(_startOffset, (int)offset);
                    shader.Dispatch(0, (int)dispatchCount, 1, 1);
                    offset              += dispatchCount;
                    dispatchesRemaining -= dispatchCount;
                }

                Shader.SetGlobalBuffer(_tmdGlyphs, persistentBuffer);
            }
        }

        public struct CollectState
        {
            internal NativeList<RenderGlyphCapture> glyphsToUpload;
            internal NativeList<uint>               glyphEntryIDsToRasterize;
            internal NativeList<uint>               atlasDirtyIDs;
            internal NativeList<int>                pixelUploadOffsetsInBytes;
            internal NativeReference<int>           pixelBytesCount;
        }

        public struct WriteState
        {
            internal bool isSdf8Dirty;
            internal bool isSdf16Dirty;
            internal bool isBitmapDirty;

            internal GraphicsBuffer glyphUploadBuffer;
            internal GraphicsBuffer glyphUploadMetaBuffer;
            internal int            glyphUploadBufferWriteCount;
            internal int            glyphUploadMetaBufferWriteCount;

            internal GraphicsBuffer pixelUploadBuffer;
            internal GraphicsBuffer pixelUploadMetaBuffer;
            internal int            pixelUploadBufferWriteCount;
            internal int            pixelUploadMetaBufferWriteCount;
        }

        internal struct RenderGlyphCapture
        {
            public TextShaderIndex* textShaderIndexPtr;
            public ResidentRange*   residentRangePtr;
            public RenderGlyph*     glyphBuffer;
            public int              glyphCount;
            public bool             makeResident;
            public int              writeStart;
            public int              gpuStart;
        }
    }
}

