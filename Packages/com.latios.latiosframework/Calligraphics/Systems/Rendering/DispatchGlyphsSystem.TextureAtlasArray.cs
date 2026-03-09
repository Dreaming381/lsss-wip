using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextMeshDOTS
{
    public partial class DispatchGlyphsSystem
    {
        // Todo: We're doing things the easy way for now. We may want to optimize it in the future to
        // send pixel updates to a compute shader and apply changes on the GPU. However, that is somewhat
        // platform-specific because platforms are inconsistent on whether the first scanline is on top or
        // bottom.
        unsafe class TextureAtlasArray<T> : IDisposable where T : unmanaged
        {
            public struct AtlasPtr
            {
                public int atlasIndex;
                public int dimension;
                public T*  ptr;

                public Span<T> AsSpan() => new Span<T>(ptr, dimension * dimension);
            }

            Texture2DArray      texture2DArray       = null;
            Texture2DArray      oldArray             = null;
            RenderTexture       renderTexture2DArray = null;
            RenderTexture       oldRenderArray       = null;
            int                 shaderPropertyId;
            int                 dimension;
            int                 atlasCount;
            TextureFormat       format;
            RenderTextureFormat renderFormat;
            bool                useMipmapping;
            bool                linear;
            bool                useComputeUpload;

            public TextureAtlasArray(int shaderPropertyId, int dimension, int initialAtlasCount, TextureFormat format, bool useMipmapping, bool linear, bool useComputeUpload)
            {
                this.shaderPropertyId = shaderPropertyId;
                this.dimension        = dimension;
                this.atlasCount       = initialAtlasCount;
                this.format           = format;
                this.useMipmapping    = useMipmapping;
                this.linear           = linear;
                this.useComputeUpload = useComputeUpload;

                if (useComputeUpload)
                {
                    this.renderFormat = format switch
                    {
                        TextureFormat.R8 => RenderTextureFormat.R8,
                        TextureFormat.R16 => RenderTextureFormat.R16,
                        TextureFormat.RGBA32 => RenderTextureFormat.ARGB32,  // Shaders should swizzle this for us
                        _ => throw new NotImplementedException(),
                    };
                    var rtrw                               = linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
                    renderTexture2DArray                   = new RenderTexture(dimension, dimension, initialAtlasCount, renderFormat, rtrw);
                    renderTexture2DArray.dimension         = TextureDimension.Tex2DArray;
                    renderTexture2DArray.volumeDepth       = initialAtlasCount;
                    renderTexture2DArray.enableRandomWrite = true;
                    renderTexture2DArray.useMipMap         = useMipmapping;
                    renderTexture2DArray.autoGenerateMips  = false;

                    CommandBuffer cmd = new CommandBuffer();
                    cmd.SetRenderTarget(new RenderTargetIdentifier(renderTexture2DArray));
                    cmd.ClearRenderTarget(true, true, Color.clear);
                    Graphics.ExecuteCommandBuffer(cmd);
                    cmd.Dispose();
                }
                else
                {
                    texture2DArray = new Texture2DArray(dimension, dimension, initialAtlasCount, format, useMipmapping, linear);
                    // The following results in an error in play mode:
                    // kTextureUploadUninitialized is only allowed for newly created textures
                    //this.texture2DArray = new Texture2DArray(dimension, dimension, initialAtlasCount, format, useMipmapping, linear, true);
                    for (int i = 0; i < initialAtlasCount; i++)
                    {
                        texture2DArray.GetPixelData<T>(0, i).AsSpan().Clear();
                    }
                    // It seems we can do this lazily, which is slightly faster.
                    //texture2DArray.Apply(useMipmapping, false);
                }
            }

            public void Dispose()
            {
                if (Application.isPlaying)
                {
                    if (useComputeUpload)
                    {
                        renderTexture2DArray.Release();
                        UnityEngine.Object.Destroy(renderTexture2DArray);
                    }
                    else
                        UnityEngine.Object.Destroy(texture2DArray);
                }
                else
                {
                    if (useComputeUpload)
                    {
                        renderTexture2DArray.Release();
                        UnityEngine.Object.DestroyImmediate(renderTexture2DArray);
                    }
                    else
                        UnityEngine.Object.DestroyImmediate(texture2DArray);
                }
            }

            public void GetAtlasPtrsForDirtyIndices(ReadOnlySpan<uint> dirtyIndicesSorted, Span<AtlasPtr> ptrs)
            {
                var atlasesNeeded = 1 + (int)(dirtyIndicesSorted[^1] & 0x3fffffffu);
                if (atlasesNeeded >= atlasCount)
                {
                    if (useComputeUpload)
                    {
                        oldRenderArray                         = renderTexture2DArray;
                        var rtrw                               = linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB;
                        renderTexture2DArray                   = new RenderTexture(dimension, dimension, atlasesNeeded, renderFormat, rtrw);
                        renderTexture2DArray.dimension         = TextureDimension.Tex2DArray;
                        renderTexture2DArray.volumeDepth       = atlasesNeeded;
                        renderTexture2DArray.enableRandomWrite = true;
                        renderTexture2DArray.useMipMap         = useMipmapping;
                        renderTexture2DArray.autoGenerateMips  = false;

                        for (int i = 0; i < atlasesNeeded; i++)
                        {
                            Graphics.CopyTexture(oldRenderArray, math.min(i, atlasCount - 1), renderTexture2DArray, i);
                        }
                    }
                    else
                    {
                        oldArray       = texture2DArray;
                        texture2DArray = new Texture2DArray(dimension, dimension, atlasesNeeded, format, useMipmapping, linear);
                        // kTextureUploadUninitialized is only allowed for newly created textures
                        //texture2DArray = new Texture2DArray(dimension, dimension, atlasesNeeded, format, useMipmapping, linear, true);

                        for (int i = 0; i < atlasCount; i++)
                        {
                            texture2DArray.CopyPixels(oldArray, i, 0, i, 0);
                        }
                        for (int i = atlasCount; i < atlasesNeeded; i++)
                        {
                            texture2DArray.GetPixelData<T>(0, i).AsSpan().Clear();
                        }
                    }

                    atlasCount = atlasesNeeded;
                }
                for (int i = 0; i < dirtyIndicesSorted.Length; i++)
                {
                    var atlasIndex = (int)(dirtyIndicesSorted[i] & 0x3fffffffu);
                    ptrs[i]        = new AtlasPtr
                    {
                        atlasIndex = atlasIndex,
                        dimension  = dimension,
                        ptr        = useComputeUpload ? null : (T*)texture2DArray.GetPixelData<T>(0, atlasIndex).GetUnsafePtr()
                    };
                }
            }

            public void ApplyChanges()
            {
                if (useComputeUpload)
                {
                    if (useMipmapping)
                        renderTexture2DArray.GenerateMips();
                    Shader.SetGlobalTexture(shaderPropertyId, renderTexture2DArray);
                    if (oldRenderArray != null)
                    {
                        oldRenderArray.Release();
                        if (Application.isPlaying)
                            UnityEngine.Object.Destroy(oldRenderArray);
                        else
                            UnityEngine.Object.DestroyImmediate(oldRenderArray);
                        oldRenderArray = null;
                    }
                }
                else
                {
                    texture2DArray.Apply(useMipmapping, false);
                    Shader.SetGlobalTexture(shaderPropertyId, texture2DArray);
                    if (oldArray != null)
                    {
                        if (Application.isPlaying)
                            UnityEngine.Object.Destroy(oldArray);
                        else
                            UnityEngine.Object.DestroyImmediate(oldArray);
                        oldArray = null;
                    }
                }
            }

            public RenderTexture GetRenderTextureForUpload() => renderTexture2DArray;
        }
    }
}

