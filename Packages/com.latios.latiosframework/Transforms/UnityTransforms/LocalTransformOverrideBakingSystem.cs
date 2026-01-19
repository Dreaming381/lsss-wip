#if LATIOS_TRANSFORMS_UNITY
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Transforms.Authoring.Systems
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(TransformBakingSystemGroup), OrderLast = true)]  // Unity's TransformBakingSystem is internal.
    [BurstCompile]
    public partial struct LocalTransformOverrideBakingSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new Job().ScheduleParallel();
        }

        [BurstCompile]
        partial struct Job : IJobEntity
        {
            public void Execute(ref Unity.Transforms.LocalTransform dst, in BakedLocalTransformOverride src)
            {
                dst = Unity.Transforms.LocalTransform.FromPositionRotationScale(src.localTransform.position, src.localTransform.rotation, src.localTransform.scale);
            }
        }
    }
}
#endif

