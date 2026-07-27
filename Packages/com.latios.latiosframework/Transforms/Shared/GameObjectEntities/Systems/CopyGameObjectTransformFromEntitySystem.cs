using Latios.Transforms.Abstract;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Jobs;

namespace Latios.Transforms.Systems
{
    [RequireMatchingQueriesForUpdate]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct CopyGameObjectTransformFromEntitySystem : ISystem, ILatiosApi
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);

            state.Fluent().With<GameObjectEntity.ExistComponent>(true).With<CopyTransformFromEntityTag>(true).With<CopyTransformFromEntityCleanupTag>(true)
            .WithWorldTransformReadOnly().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api          = this.GetApi(ref state);
            var mapping      = api.worldBlackboardEntity.GetCollectionComponent<CopyTransformFromEntityMapping>();
            state.Dependency = new Job
            {
                indexToEntityMap = mapping.indexToEntityMap,
            }.Inject(api).Schedule(mapping.transformAccessArray, state.Dependency);
        }

        [BurstCompile]
        partial struct Job : IJobParallelForTransform, IInjectable
        {
            [ReadOnly] public NativeHashMap<int, Entity>           indexToEntityMap;
            [ReadOnly, Inject] WorldTransformReadOnlyAspect.Lookup transformLookup;

            public void Execute(int index, TransformAccess transform)
            {
                var entityTransform = transformLookup[indexToEntityMap[index]];
                transform.SetPositionAndRotation(entityTransform.position, entityTransform.rotation);
                transform.localScale = entityTransform.worldTransformQvvs.scale * entityTransform.worldTransformQvvs.stretch;
            }
        }
    }
}

