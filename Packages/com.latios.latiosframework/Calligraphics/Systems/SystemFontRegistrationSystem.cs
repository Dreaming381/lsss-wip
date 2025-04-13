using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Calligraphics.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public unsafe partial struct SystemFontRegistrationSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();

            state.Fluent().With<CalliByte, CalliByteChangedFlag, TextBaseConfiguration>(true).Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new Job
            {
                fontTable = latiosWorld.worldBlackboardEntity.GetCollectionComponent<FontTable>(false)
            }.Schedule(state.Dependency);
            state.Enabled = false;
        }

        struct Job : IJob
        {
            public FontTable fontTable;

            public void Execute()
            {
                var systemFontsManaged = UnityEngine.TextCore.Exposed.SystemFonts.GetAll();
                var systemFonts        = new NativeArray<SystemFontUnmanaged>(systemFontsManaged.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                byte* runningPtr     = null;
                int   bytesRemaining = 0;

                for (int i = 0; i < systemFontsManaged.Length; i++)
                {
                    var font       = systemFontsManaged[i];
                    systemFonts[i] = new SystemFontUnmanaged
                    {
                        typographicFamily    = ToSpan(font.typographicFamily, ref runningPtr, ref bytesRemaining),
                        typographicSubfamily = ToSpan(font.typographicSubfamily, ref runningPtr, ref bytesRemaining),
                        faceIndex            = font.faceIndex,
                        filePath             = ToSpan(font.filePath, ref runningPtr, ref bytesRemaining),
                    };
                }

                AddSystemFontsToTable(ref fontTable, ref systemFonts);
            }

            StringSpan ToSpan(string s, ref byte* runningPtr, ref int bytesRemaining)
            {
                int neededSize = UTF8Encoding.UTF8.GetByteCount(s);
                if (neededSize > bytesRemaining)
                {
                    bytesRemaining = math.max(256 * 256, neededSize);
                    runningPtr     = AllocatorManager.Allocate<byte>(Allocator.Temp, bytesRemaining);
                }
                var result = new StringSpan
                {
                    ptr    = runningPtr,
                    length = bytesRemaining,
                };
                runningPtr     += neededSize;
                bytesRemaining -= neededSize;
                result.CopyFromTruncated(s);
                return result;
            }
        }

        [BurstCompile]
        static void AddSystemFontsToTable(ref FontTable fontTable, ref NativeArray<SystemFontUnmanaged> systemFonts)
        {
            // Todo:
        }

        struct SystemFontUnmanaged
        {
            public StringSpan typographicFamily;
            public StringSpan typographicSubfamily;
            public int        faceIndex;
            public StringSpan filePath;
        }

        struct StringSpan : INativeList<byte>, IUTF8Bytes
        {
            public byte* ptr;
            public int   length;

            public byte this[int index] { get => ElementAt(index); set => ElementAt(index) = value; }

            public int Capacity { get => length; set => throw new System.NotImplementedException(); }

            public bool IsEmpty => length == 0;

            public int Length { get => length; set => throw new System.NotImplementedException(); }

            public void Clear()
            {
                throw new System.NotImplementedException();
            }

            public ref byte ElementAt(int index)
            {
                return ref ptr[index];
            }

            public byte* GetUnsafePtr() => ptr;

            public bool TryResize(int newLength, NativeArrayOptions clearOptions = NativeArrayOptions.ClearMemory)
            {
                throw new System.NotImplementedException();
            }
        }
    }
}

