using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    [BurstCompile]
    public partial struct SpeedShaderUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = state.Time.DeltaTime;
            state.Entities.ForEach((ref IntegratedSpeed integratedSpeed, in Speed speed) =>
            {
                integratedSpeed.integratedSpeed += speed.speed * dt;
            }).ScheduleParallel();

            //Optimization if someone gets the idea of putting a _Speed shader on a bullet or something.
            state.Entities.ForEach((ref SpeedProperty speedProperty, in Speed speed) =>
            {
                speedProperty.speed = speed.speed;
            }).ScheduleParallel();

            state.Entities.ForEach((ref IntegratedSpeedProperty integratedSpeedProperty, in IntegratedSpeed integratedSpeed) =>
            {
                integratedSpeedProperty.integratedSpeed = integratedSpeed.integratedSpeed;
            }).ScheduleParallel();

            var speedCdfe = state.GetComponentDataFromEntity<Speed>(true);
            state.Entities.WithNone<Speed>().ForEach((Entity entity, ref SpeedProperty speedProperty, in SpeedEntity speedEntity) =>
            {
                speedProperty.speed = speedCdfe[speedEntity.entityWithSpeed].speed;
            }).WithReadOnly(speedCdfe).ScheduleParallel();

            var isCdfe = state.GetComponentDataFromEntity<IntegratedSpeed>(true);
            state.Entities.WithNone<IntegratedSpeed>().ForEach((Entity entity, ref IntegratedSpeedProperty integratedSpeedProperty, in SpeedEntity speedEntity) =>
            {
                integratedSpeedProperty.integratedSpeed = isCdfe[speedEntity.entityWithSpeed].integratedSpeed;
            }).WithReadOnly(isCdfe).ScheduleParallel();
        }
    }
}

