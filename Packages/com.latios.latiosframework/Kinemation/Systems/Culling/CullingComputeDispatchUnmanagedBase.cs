using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Kinemation
{
    public interface ICullingComputeDispatchSystem<TCollect, TWrite> where TCollect : unmanaged where TWrite : unmanaged
    {
        public TCollect Collect(ref SystemState state);
        public TWrite Write(ref SystemState state, ref TCollect collected);
        public void Dispatch(ref SystemState state, ref TWrite written);
    }

    public struct CullingComputeDispatchData<TCollect, TWrite> where TCollect : unmanaged where TWrite : unmanaged
    {
        TCollect                    collected;
        TWrite                      written;
        BlackboardEntity            worldBlackboardEntity;
        CullingComputeDispatchState nextExpectedState;

        public CullingComputeDispatchData(LatiosWorldUnmanaged latiosWorld)
        {
            collected             = default;
            written               = default;
            worldBlackboardEntity = latiosWorld.worldBlackboardEntity;
            nextExpectedState     = CullingComputeDispatchState.Collect;
        }

        public void DoUpdate<TSystem>(ref SystemState state, ref TSystem system) where TSystem : ICullingComputeDispatchSystem<TCollect, TWrite>
        {
            var activeState = worldBlackboardEntity.GetComponentData<CullingComputeDispatchActiveState>();
            if (activeState.state != nextExpectedState)
            {
                UnityEngine.Debug.LogError("The CullingComputeDispatch expected state does not match the current state. Behavior may not be correct.");
            }
            switch (activeState.state)
            {
                case CullingComputeDispatchState.Collect:
                    collected         = system.Collect(ref state);
                    nextExpectedState = CullingComputeDispatchState.Write;
                    break;
                case CullingComputeDispatchState.Write:
                    written           = system.Write(ref state, ref collected);
                    nextExpectedState = CullingComputeDispatchState.Dispatch;
                    break;
                case CullingComputeDispatchState.Dispatch:
                    system.Dispatch(ref state, ref written);
                    nextExpectedState = CullingComputeDispatchState.Collect;
                    break;
            }
        }
    }
}

