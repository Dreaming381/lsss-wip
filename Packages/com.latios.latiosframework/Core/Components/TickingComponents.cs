using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios
{
    /// <summary>
    /// A component that signifies that ticking behavior should be applied to the entity.
    /// This tag is used for systems that add or remove ticking components, but otherwise
    /// has no impact on whether an entity is processed during ticking.
    /// </summary>
    public struct TickedEntityTag : IComponentData { }

    /// <summary>
    /// A component that signifies that no interpolation behavior is needed for this entity.
    /// This tag is used for systems that add or remove non-ticking components, but otherwise
    /// has no impact on whether an entity is processed during interpolation.
    /// </summary>
    public struct TickingOnlyEntityTag : IComponentData { }

    /// <summary>
    /// A component that represents everything happening in the current tick.
    /// Usage: Read only. Do not write.
    /// </summary>
    public struct TickingState : IComponentData
    {
        /// <summary>
        /// The time elapsed at the current tick
        /// </summary>
        public double elapsedTime;
        /// <summary>
        /// The time difference between this tick and the previous tick (that is, the tick value one below the current)
        /// </summary>
        public float deltaTime;
        /// <summary>
        /// The current tick value
        /// </summary>
        public int tick;
        /// <summary>
        /// The number of ticks being evaluated this frame (includes rollback and catch-up)
        /// </summary>
        public int ticksThisFrame;
        /// <summary>
        /// The first tick that is evaluated this frame
        /// </summary>
        public int firstTickThisFrame;
        /// <summary>
        /// One tick higher than the highest tick ever evaluated
        /// </summary>
        public int newTick;
        /// <summary>
        /// The tick in which the frame's input should be applied
        /// </summary>
        public int inputTick;
        /// <summary>
        /// How far into the tick the frame's input should be resumed from
        /// </summary>
        public float inputTickFraction;
        /// <summary>
        /// The interpolation factor for the final tick this frame
        /// </summary>
        public float finalTickFraction;
        /// <summary>
        /// A frame counter for convenience
        /// </summary>
        public int frameCounter;
        /// <summary>
        /// The tick that was previously evaluated
        /// </summary>
        public int previousEvaluatedTick;

        /// <summary>
        /// True if this tick has never been simulated before.
        /// </summary>
        public bool isNewTick => tick >= newTick;
        /// <summary>
        /// True if this is the first time a tick was simulated and input should be applied to it.
        /// This is common in tick snapping input mode, and rare otherwise.
        /// </summary>
        public bool isFirstInputTick => tick == newTick && tick == inputTick;
        /// <summary>
        /// True if this tick is being resimulated with additional input.
        /// </summary>
        public bool isAdditiveInputTick => !isNewTick && tick == inputTick;
        /// <summary>
        /// True if this tick is a tick that follows the tick where new input should be applied, because the ticks are trying to catch up with the game time
        /// </summary>
        public bool isCatchupTick => tick > newTick;
        /// <summary>
        /// True if this is a tick that was rolled back with no new inputs to be appended. This only happens in a networked context.
        /// </summary>
        public bool isReplayTick => tick < inputTick;
        /// <summary>
        /// True if this is a tick where rollback should be applied. This only happens in a networked context.
        /// </summary>
        public bool isRollbackTick => isReplayTick && tick == firstTickThisFrame;
        /// <summary>
        /// True if the previous tick is going to be completely resimulated, and therefore its results should be discarded.
        /// </summary>
        public bool discardPreviousTick => tick == previousEvaluatedTick || (tick == firstTickThisFrame && firstTickThisFrame - 1 + ticksThisFrame == previousEvaluatedTick);
    }

    /// <summary>
    /// Add this to a component to allow for automatic structural changes to be applied for the component based on the TickedEntityTag and TickingOnlyEntityTag.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class TickedAutoAddAttribute : Attribute
    {
        public Type nonTickedType;
        public bool copyData;

        /// <param name="nonTickedType">The target type this type pairs with for structural changes</param>
        /// <param name="copyData">If set to true, structural changes will also copy component values. Only valid for unmanaged IComponentData and IBufferElementData.</param>
        public TickedAutoAddAttribute(Type nonTickedType, bool copyData)
        {
            this.nonTickedType = nonTickedType;
            this.copyData      = copyData;
        }
    }
}

