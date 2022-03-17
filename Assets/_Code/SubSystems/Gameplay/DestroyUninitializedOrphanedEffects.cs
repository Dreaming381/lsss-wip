using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

//Firing effects will spawn parented to their ships.
//If the ship is destroyed the same frame, then the parent won't exist
//when the transform system runs which produces an error.
//Since the transform data is inherited from the parent which no longer
//exists and is thus lost, we should just kill this effect entity early.

namespace Lsss
{
    public partial class DestroyUninitializedOrphanedEffects : SubSystem
    {
        protected override void OnUpdate()
        {
            var dcb = new DestroyCommandBuffer(Allocator.TempJob);

            Entities.WithNone<PreviousParent>().ForEach((Entity entity, in Parent parent) =>
            {
                //Todo: Check for existence rather than existence of a particular component.
                if (!HasComponent<LocalToWorld>(parent.Value))
                {
                    dcb.Add(entity);
                }
            }).Run();

            dcb.Playback(EntityManager);
            dcb.Dispose();
        }
    }
}

