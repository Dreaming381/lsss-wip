using System;
using Unity.Jobs;

namespace Latios
{
    public partial interface IJobEach
    {
        /// <summary>
        /// Marks a type as usable as an Execute() parameter of an IJobEach.
        /// Decorate it with one or more [ParameterHandle] attributes to declare which backing handles can source it.
        /// </summary>
        public interface IParameter
        {
        }

        /// <summary>
        /// The manners in which an IJobEach can be dispatched, as a mask.
        /// </summary>
        [Flags]
        public enum ScheduleModeMask
        {
            /// <summary>
            /// A single job executing on the main thread
            /// </summary>
            Run = 1,
            /// <summary>
            /// A single job executing on a worker thread
            /// </summary>
            Schedule = 2,
            /// <summary>
            /// Multiple job instances executing across worker threads
            /// </summary>
            ScheduleParallel = 4,
            /// <summary>
            /// Executed inline on the calling thread without any job invocation
            /// </summary>
            RunImmediate = 8,
            /// <summary>
            /// Every dispatch which only ever executes on one thread at a time
            /// </summary>
            SingleThreaded = Run | Schedule | RunImmediate,
            /// <summary>
            /// Every dispatch
            /// </summary>
            All = Run | Schedule | ScheduleParallel | RunImmediate
        }

        /// <summary>
        /// Declares a backing handle type which can source an IParameter for the specified dispatch modes, and optionally
        /// only when the Execute() parameter is decorated with all of the specified attributes.
        /// </summary>
        /// <remarks>
        /// When a type declares several handles, the one whose decorating attributes are all present on the parameter is
        /// selected, and the one requiring the most attributes wins a tie. The mode masks take no part in selection; they
        /// are intersected across every handle a job uses to determine which dispatches that job allows.
        ///
        /// For a generic parameter type, specify the open generic handle type, such as typeof(HasHandle&lt;&gt;).
        /// </remarks>
        [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
        public sealed class ParameterHandleAttribute : Attribute
        {
            /// <param name="handleType">A type implementing IParameterHandle for the decorated parameter type</param>
            /// <param name="supportedModes">The dispatch modes this handle can serve</param>
            public ParameterHandleAttribute(Type handleType, ScheduleModeMask supportedModes)
            {
            }

            /// <param name="handleType">A type implementing IParameterHandle for the decorated parameter type</param>
            /// <param name="supportedModes">The dispatch modes this handle can serve</param>
            /// <param name="decoratorAttributeTypes">Attributes which must all decorate the parameter for this handle to be selected</param>
            public ParameterHandleAttribute(Type handleType, ScheduleModeMask supportedModes, params Type[] decoratorAttributeTypes)
            {
            }
        }

        /// <summary>
        /// Sources an IParameter for each entity that an IJobEach processes.
        /// Implement ILatiosApiGettable's methods so the scheduling system caches and updates the handle.
        /// </summary>
        /// <typeparam name="TParameter">The parameter type this handle sources</typeparam>
        public interface IParameterHandle<TParameter> : ILatiosApiGettable where TParameter : unmanaged, IParameter
        {
            /// <summary>
            /// Adds the types this handle accesses to the IJobEach's EntityQuery.
            /// Called on a default instance while the query is being built.
            /// </summary>
            /// <param name="query">The query under construction</param>
            /// <returns>The query with this handle's requirements appended</returns>
            FluentQuery AppendToQuery(FluentQuery query);

            /// <summary>
            /// Called before a chunk is processed. Not guaranteed to be called for every chunk, as some chunks may be filtered
            /// prior to the call. Additionally, this may be called on chunks which get filtered later.
            /// </summary>
            /// <param name="context">The job metadata for the chunk. Per-entity members are not yet valid.</param>
            /// <returns>False to skip the chunk entirely, in which case the job's Execute() is not called for it</returns>
            bool OnChunkBegin(in JobContext context);

            /// <summary>
            /// Called for each chunk that had OnChunkBegin() called, and called after the chunk has been processed or skipped.
            /// </summary>
            /// <param name="context">The job metadata for the chunk. Per-entity members are not valid.</param>
            /// <param name="chunkWasExecuted">Whether the job's Execute() was called for entities in this chunk</param>
            void OnChunkEnd(in JobContext context, bool chunkWasExecuted);

            /// <summary>
            /// Constructs the parameter for the entity currently being processed
            /// </summary>
            /// <param name="context">The full job metadata for the entity</param>
            TParameter GetParameter(in JobContext context);
        }

        /// <summary>
        /// Sources an IParameter which requires that chunks sharing mutable state be processed together on a single thread.
        /// </summary>
        /// <typeparam name="TParameter">The parameter type this handle sources</typeparam>
        public interface IGroupedParameterHandle<TParameter> : IParameterHandle<TParameter> where TParameter : unmanaged, IParameter
        {
            /// <summary>
            /// Schedules the setup job(s) which update the chunk groupings to account for this handle's requirements.
            /// Only called in a ScheduleParallel() context. Otherwise, all chunks end up in a single group.
            /// </summary>
            /// <param name="builder">The shared grouping constraints for the dispatch</param>
            /// <param name="inputDeps">The JobHandle the setup job(s) must depend on</param>
            /// <returns>The JobHandle of the last scheduled setup job</returns>
            JobHandle ScheduleGroupUnions(ChunkGroupBuilder builder, JobHandle inputDeps);

            /// <summary>
            /// Prepares the handle to process a group of chunks. Called before the group's first OnChunkBegin().
            /// </summary>
            /// <param name="groupContext">The job metadata for the group</param>
            void OnGroupBegin(in GroupContext groupContext);

            /// <summary>
            /// Called after every chunk of a group has been processed.
            /// </summary>
            /// <param name="groupContext">The job metadata for the group</param>
            void OnGroupEnd(in GroupContext groupContext);
        }
    }
}

