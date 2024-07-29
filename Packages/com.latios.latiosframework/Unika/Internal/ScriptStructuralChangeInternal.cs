using Latios.Unsafe;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Unika
{
    internal static unsafe class ScriptStructuralChangeInternal
    {
        struct InstanceIdWarningKey { }

        static readonly SharedStatic<byte> s_instanceIdWarning = SharedStatic<byte>.GetOrCreate<InstanceIdWarningKey>();

        public static void InitializeStatics() => s_instanceIdWarning.Data = 0;

        public static int AllocateScript(ref DynamicBuffer<UnikaScripts> scriptBuffer, int scriptType)
        {
            var currentScriptCount = scriptBuffer.AllScripts(default).length;
            var scripts            = scriptBuffer.Reinterpret<ScriptHeader>();
            var mask               = ScriptTypeInfoManager.GetBloomMask((short)scriptType);
            var sizeAndAlignment   = ScriptTypeInfoManager.GetSizeAndAlignement((short)scriptType);
            UnityEngine.Assertions.Assert.IsTrue((ulong)sizeAndAlignment.x < ScriptHeader.kMaxByteOffset);
            UnityEngine.Assertions.Assert.IsTrue(sizeAndAlignment.y <= UnsafeUtility.SizeOf<ScriptHeader>());

            if (currentScriptCount == 0)
            {
                scripts.Add(new ScriptHeader
                {
                    bloomMask          = mask,
                    instanceCount      = 1,
                    lastUsedInstanceId = 1
                });

                var newCapacity = math.ceilpow2(1);
                scripts.Add(new ScriptHeader
                {
                    bloomMask  = mask,
                    instanceId = 1,
                    scriptType = scriptType,
                    byteOffset = 0
                });
                for (int i = 1; i < newCapacity; i++)
                    scripts.Add(default);

                var elementsNeeded = CollectionHelper.Align(sizeAndAlignment.x, UnsafeUtility.SizeOf<ScriptHeader>()) / UnsafeUtility.SizeOf<ScriptHeader>();
                for (int i = 0; i < elementsNeeded; i++)
                    scripts.Add(default);

                return 0;
            }

            var scriptCapacity = math.ceilpow2(currentScriptCount);
            if (currentScriptCount == scriptCapacity)
            {
                for (int i = 0; i < scriptCapacity; i++)
                    scripts.Add(default);

                var src       = scripts.AsNativeArray().GetSubArray(1 + scriptCapacity, 1).GetUnsafePtr();
                var dst       = scripts.AsNativeArray().GetSubArray(1 + scriptCapacity * 2, 1).GetUnsafePtr();
                var byteCount = (scripts.Length - (1 + scriptCapacity * 2)) * UnsafeUtility.SizeOf<ScriptHeader>();
                UnsafeUtility.MemMove(dst, src, byteCount);
                scriptCapacity *= 2;
            }

            ref var master        = ref scripts.ElementAt(0);
            master.bloomMask     |= mask;
            master.instanceCount  = currentScriptCount + 1;
            var nextIndex         = master.lastUsedInstanceId;
            if ((ulong)nextIndex == ScriptHeader.kMaxInstanceId)
            {
                if (s_instanceIdWarning.Data == 0)
                {
                    UnityEngine.Debug.LogWarning(
                        "Exhausted all instance IDs in a Unika entity. Instance IDs will be reused, which may result in stale references incorrectly referencing new scripts. This message will be disabled to prevent spamming.");
                    s_instanceIdWarning.Data = 1;
                }
                using var allocator   = ThreadStackAllocator.GetAllocator();
                var       rawArray    = allocator.Allocate<int>(currentScriptCount);
                var       usedIndices = new UnsafeList<int>(rawArray, currentScriptCount);
                for(int   i           = 0; i < currentScriptCount; i++)
                {
                    usedIndices[i] = scripts[i + 1].instanceId;
                }
                usedIndices.Sort();
                if ((ulong)usedIndices[usedIndices.Length - 1] < ScriptHeader.kMaxInstanceId / 2)
                {
                    nextIndex                 = usedIndices[usedIndices.Length - 1] + 1;
                    master.lastUsedInstanceId = nextIndex;
                }
                else
                {
                    for (int i = 0; i < usedIndices.Length; i++)
                    {
                        if (i + 1 != usedIndices[i])
                        {
                            nextIndex = i + 1;
                            break;
                        }
                    }
                }
            }
            else
            {
                nextIndex++;
                master.lastUsedInstanceId = nextIndex;
            }

            var nextFreeByteOffset       = scripts[currentScriptCount].byteOffset + ScriptTypeInfoManager.GetSizeAndAlignement((short)scripts[currentScriptCount].scriptType).x;
            var alignment                = CollectionHelper.Align(nextFreeByteOffset, sizeAndAlignment.y);
            var requiredTotalElementSize =
                (CollectionHelper.Align(alignment + sizeAndAlignment.x, UnsafeUtility.SizeOf<ScriptHeader>()) / UnsafeUtility.SizeOf<ScriptHeader>()) + scriptCapacity + 1;
            for (int i = scripts.Length; i < requiredTotalElementSize; i++)
                scripts.Add(default);

            scripts[currentScriptCount + 1] = new ScriptHeader
            {
                bloomMask  = mask,
                scriptType = scriptType,
                byteOffset = alignment,
                instanceId = nextIndex
            };
            return currentScriptCount + 1;
        }
    }
}

