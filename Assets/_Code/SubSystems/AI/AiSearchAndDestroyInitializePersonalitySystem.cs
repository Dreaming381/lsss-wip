using Latios;
using Latios.Calci;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Lsss
{
    [BurstCompile]
    public partial struct AiSearchAndDestroyInitializePersonalitySystem : ISystem, ILatiosApi, ISystemNewScene
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            this.OnCreateForLatios(ref state);
        }

        public void OnNewScene(ref SystemState state) => state.InitSystemRng("AiSearchAndDestroyInitializePersonalitySystem");

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api = this.GetApi(ref state);
            var ecb = api.syncPoint.CreateEntityCommandBuffer();

            new Job().ScheduleParallel(api);

            ecb.RemoveComponent<AiSearchAndDestroyPersonalityInitializerValues>(api.GetDefaultQuery<Job>().ToEntityArray(Allocator.Temp));
        }

        [With(typeof(AiTag))]
        [BurstCompile]
        partial struct Job : IJobEach
        {
            public void Execute(RngEach rng, ref AiSearchAndDestroyPersonality personality, in AiSearchAndDestroyPersonalityInitializerValues initalizer)
            {
                personality.targetLeadDistance = rng.NextFloat(initalizer.targetLeadDistanceMinMax.x, initalizer.targetLeadDistanceMinMax.y);
            }
        }
    }
}

