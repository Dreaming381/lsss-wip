using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Kinemation.Systems
{
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct ApplyMipMapStreamingLevelsSystem : ISystem, ILatiosApi
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var api = this.OnCreateForLatios(ref state);

            api.worldBlackboardEntity.AddComponent(new TypePack<MipMapStreamingAssignment, MipMapCameraParameters>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var api         = this.GetApi(ref state);
            var assignments = api.worldBlackboardEntity.GetBuffer<MipMapStreamingAssignment>(false);
            foreach (var assignment in assignments)
            {
                assignment.texture.RequestMipMapLevelIfValid(assignment.level);
            }
            assignments.Clear();
            api.worldBlackboardEntity.GetBuffer<MipMapCameraParameters>(false).Clear();
        }
    }
}
