using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Systems
{
    #region Injectable
    /// <summary>
    /// ComponentSystemGroup which updates both at runtime and at bake time (for closed subscenes) responsible
    /// for correcting entities and their attached components based on the presence of the TickedEntityTag and
    /// TickingOnlyEntityTag.
    /// </summary>
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.EntitySceneOptimizations)]
    public partial class TickedArchetypeCorrectionSystemGroup : ComponentSystemGroup
    {
    }

    /// <summary>
    /// SuperSystem where history is either updated or reverted.
    /// isReplayTick - load snapshot for whatever needs it (Todo: How to cull this?)
    /// discardPreviousTick - Set current equal to previous
    /// !discardPreviousTick - Set previous equal to current for all predicted (replay) or all (!replay)
    /// </summary>
    [DisableAutoCreation]
    public partial class TickedUpdateHistorySuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            EnableSystemSorting = true;
        }
    }

    /// <summary>
    /// SuperSystem where inputs can be processed based on the TickingState.
    /// isFirstInputTick - read and apply input from scratch
    /// isAdditiveInputTick - add states and merge events
    /// isCatchupTick - sustain states and ignore events
    /// isRollbackTick - provide input recorded from a history buffer (for networking use cases)
    /// </summary>
    [DisableAutoCreation]
    public partial class TickedInputSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            EnableSystemSorting = true;
        }
    }

    /// <summary>
    /// SuperSystem where tick-based simulation occurs
    /// </summary>
    [DisableAutoCreation]
    public partial class TickedSimulationSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            EnableSystemSorting = true;
        }
    }

    /// <summary>
    /// SuperSystem where interpolation may occur based on the TickingState.
    /// finalTickFraction - interpolation factor between previous and current
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(TickedLocalSuperSystem))]
    [DisableAutoCreation]
    public partial class TickedInterpolateSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            EnableSystemSorting = true;
        }
    }
    #endregion

    #region Loop Management
    /// <summary>
    /// Internal SuperSystem used for ticking loop management
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
    [DisableAutoCreation]
    public partial class TickedLocalSuperSystem : RootSuperSystem
    {
        protected override void CreateSystems()
        {
            EnableSystemSorting = false;

            GetOrCreateAndAddManagedSystem<TickedSyncPointPlaybackSystemDispatch>();
            GetOrCreateAndAddManagedSystem<TickedArchetypeCorrectionSystemGroup>();
            GetOrCreateAndAddManagedSystem<TickedInputSuperSystem>();
            GetOrCreateAndAddManagedSystem<TickedUpdateHistorySuperSystem>();
            GetOrCreateAndAddManagedSystem<TickedSimulationSuperSystem>();
        }

        protected override void OnUpdate()
        {
            var tickingState = worldBlackboardEntity.GetComponentData<TickingState>();

            for (int i = 0; i < tickingState.ticksThisFrame; i++)
            {
                World.PushTime(new Unity.Core.TimeData(tickingState.elapsedTime, tickingState.deltaTime));
                base.OnUpdate();
                tickingState.elapsedTime           += tickingState.deltaTime;
                tickingState.previousEvaluatedTick  = tickingState.tick;
                tickingState.tick++;
                worldBlackboardEntity.SetComponentData(tickingState);
                World.PopTime();
            }
        }
    }
    #endregion
}

/*
   Networking uses a "predict-the-past" for game environment and other players.
   The systems predicting the past have the ability to "read the future" as a means to implement simple interpolation.
   Every networked entity can exist with its own delay target, although usually these are grouped by player.
   This allows interpolation to seemlessly switch to extrapolation, and allows mediation between predicted and streamed physics.
   It also allows for dead-reckoning approaches to be inserted into the pipeline.
   Everything keeps a tick-level history on the client so it can easily be retrieved and when predicting other entities with older data.

   The server basically uses the local loop, except it does not have interpolation, and it does not discard a previous tick nor additively
   apply input. It sends out snapshots afterwards.

   The client could possibly move input ahead of the loop, by zipping to the final tick, doing input, and then rolling back afterwards.
   The client would need to save and restore this final input. But this comes at the slight advantage of getting inputs on the network sooner.
   The client will have several systems after tick sync point but before TickUpdateHistorySuperSystem for managing snapshots and prediction needs.
   The client will have several systems after TickSimulationSuperSystem and possibly after the ticking loop but before interpolation for saving predicted states.
 */

