using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    public partial class SpeedShaderUpdateSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            float dt = Time.DeltaTime;
            Entities.ForEach((ref IntegratedSpeed integratedSpeed, in Speed speed) =>
            {
                integratedSpeed.integratedSpeed += speed.speed * dt;
            }).ScheduleParallel();

            //Optimization if someone gets the idea of putting a _Speed shader on a bullet or something.
            Entities.ForEach((ref SpeedProperty speedProperty, in Speed speed) =>
            {
                speedProperty.speed = speed.speed;
            }).ScheduleParallel();

            Entities.ForEach((ref IntegratedSpeedProperty integratedSpeedProperty, in IntegratedSpeed integratedSpeed) =>
            {
                integratedSpeedProperty.integratedSpeed = integratedSpeed.integratedSpeed;
            }).ScheduleParallel();

            Entities.WithNone<Speed>().ForEach((Entity entity, ref SpeedProperty speedProperty, in SpeedEntity speedEntity) =>
            {
                speedProperty.speed = GetComponent<Speed>(speedEntity.entityWithSpeed).speed;
            }).ScheduleParallel();

            Entities.WithNone<IntegratedSpeed>().ForEach((Entity entity, ref IntegratedSpeedProperty integratedSpeedProperty, in SpeedEntity speedEntity) =>
            {
                integratedSpeedProperty.integratedSpeed = GetComponent<IntegratedSpeed>(speedEntity.entityWithSpeed).integratedSpeed;
            }).ScheduleParallel();
        }
    }
}

