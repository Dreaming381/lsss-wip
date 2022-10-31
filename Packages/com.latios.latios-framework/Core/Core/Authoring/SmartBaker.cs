using System.Collections;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

namespace Latios.Authoring
{
    [TemporaryBakingType]
    public interface ISmartBakeItem<TAuthoring> : IComponentData where TAuthoring : Component
    {
        public bool CaptureInputsAndFilter(TAuthoring authoring, IBaker baker);
        public void Process(EntityManager entityManager, Entity entity);
    }

    [BurstCompile]
    public abstract partial class SmartBaker<TAuthoring, TSmartBakeItem> : Baker<TAuthoring>,
        ICreateSmartBakerSystem where TAuthoring : Component where TSmartBakeItem : unmanaged,
        ISmartBakeItem<TAuthoring>
    {
        public virtual bool RunProcessInBurst()
        {
            return true;
        }

        public sealed override void Bake(TAuthoring authoring)
        {
            // Todo: May need to cache construction of this to avoid GC.
            // However, that might have been a "struct" limitation and not an "unmanaged" limitation.
            var data = new TSmartBakeItem();
            if (data.CaptureInputsAndFilter(authoring, this))
            {
                var smartBakerEntity = CreateAdditionalEntity(TransformUsageFlags.None, true);
                AddComponent(smartBakerEntity, data);
                AddComponent(smartBakerEntity, new SmartBakerTargetEntityReference { targetEntity = GetEntityWithoutDependency() });
            }
        }

        void ICreateSmartBakerSystem.Create(World world, ComponentSystemGroup addToThis)
        {
            var system        = world.GetOrCreateSystemManaged<SmartBakerSystem<TAuthoring, TSmartBakeItem> >();
            system.runInBurst = RunProcessInBurst();
            addToThis.AddSystemToUpdateList(system);
        }

        // These jobs are here but the system is split out due to a bug in source generators dropping the generics
        // on the wrapper type.
        [BurstCompile]
        internal struct ProcessSmartBakeDataBurstedJob : IJob
        {
            public EntityManager                                           em;
            [ReadOnly] public NativeArray<SmartBakerTargetEntityReference> targetReferences;
            public NativeArray<TSmartBakeItem>                             smartDataArray;

            public void Execute()
            {
                for (int i = 0; i < targetReferences.Length; i++)
                {
                    var smartData = smartDataArray[i];
                    smartData.Process(em, targetReferences[i].targetEntity);
                    smartDataArray[i] = smartData;
                }
            }
        }

        [BurstCompile]
        internal struct WriteBackBakedDataJob : IJobFor
        {
            [NativeDisableParallelForRestriction] public ComponentLookup<TSmartBakeItem> smartDataLookup;
            [ReadOnly] public NativeArray<Entity>                                        entities;
            [ReadOnly] public NativeArray<TSmartBakeItem>                                smartDataArray;

            public void Execute(int i)
            {
                smartDataLookup[entities[i]] = smartDataArray[i];
            }
        }
    }

    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    internal partial class SmartBakerSystem<TAuthoring, TSmartBakeItem> : SystemBase where TAuthoring : Component where TSmartBakeItem : unmanaged,
        ISmartBakeItem<TAuthoring>
    {
        public bool runInBurst;

        EntityQuery m_query;

        protected override void OnCreate()
        {
            m_query = new EntityQueryBuilder(Allocator.Temp)
                      .WithAllRW<TSmartBakeItem>()
                      .WithAll<SmartBakerTargetEntityReference>()
                      .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
                      .Build(this);
        }

        protected override void OnUpdate()
        {
            CompleteDependency();

            var entities         = m_query.ToEntityArray(Allocator.TempJob);
            var targetReferences = m_query.ToComponentDataArray<SmartBakerTargetEntityReference>(Allocator.TempJob);
            var smartDataArray   = m_query.ToComponentDataArray<TSmartBakeItem>(Allocator.TempJob);

            var processJob = new SmartBaker<TAuthoring, TSmartBakeItem>.ProcessSmartBakeDataBurstedJob
            {
                em               = EntityManager,
                targetReferences = targetReferences,
                smartDataArray   = smartDataArray
            };

            // Todo: Figure out how to get safety to not complain here.
            //if (runInBurst)
            //    processJob.Run();
            //else
            processJob.Execute();

            var writeBackJob = new SmartBaker<TAuthoring, TSmartBakeItem>.WriteBackBakedDataJob
            {
                smartDataLookup = GetComponentLookup<TSmartBakeItem>(),
                entities        = entities,
                smartDataArray  = smartDataArray
            };
            writeBackJob.RunByRef(entities.Length);

            entities.Dispose();
            targetReferences.Dispose();
            smartDataArray.Dispose();
        }
    }

    internal interface ICreateSmartBakerSystem
    {
        internal void Create(World world, ComponentSystemGroup addToThis);
    }

    [TemporaryBakingType]
    internal struct SmartBakerTargetEntityReference : IComponentData
    {
        public Entity targetEntity;
    }
}

