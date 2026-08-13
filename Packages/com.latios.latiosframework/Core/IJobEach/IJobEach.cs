using Unity.Entities;
using Unity.Jobs;

namespace Latios
{
    /// <summary>
    /// Implement this interface on a partial struct to define a job which processes each entity matching an EntityQuery,
    /// as a feature-rich alternative to IJobEntity.
    ///
    /// Write an Execute() method whose parameters describe the data each entity needs. The default EntityQuery is derived
    /// from those parameters. Dispatch the job with Run(), RunImmediate(), Schedule(), or ScheduleParallel(), passing in
    /// the api.
    ///
    /// Do not implement the methods in the Impl region yourself.
    /// </summary>
    /// <remarks>
    /// The EntityQuery uses FluentQuery rules, so enableable states are ignored on parameters by default. Use the attributes
    /// [With], [Without], [WithEnabled], [Entities.WithDisabled], [WithoutEnabled], [WithAnyEnabled], [IgnoreEnableableBits],
    /// [IncludeDisabledEntities], [IncludePrefabs], [IncludeSystemEntities], [IncludeMetaEntities], [UseWriteGroups], and
    /// [NotRequired].
    ///
    /// Supported Execute() parameter kinds:
    /// - `in` and `ref` components, plus `RefRO&lt;T&gt;` and `RefRW&lt;T&gt;`
    /// - `EnableableRefRO&lt;T&gt;` and `EnableableRefRW&lt;T&gt;` for a component and its enabled bit together
    /// - `EnabledRefRO&lt;T&gt;` and `EnabledRefRW&lt;T&gt;` for enabled bits alone
    /// - `DynamicBuffer&lt;T&gt;` by `in`, `ref`, or value
    /// - `Entity`
    /// - `in JobContext` for job and entity metadata
    /// - Any type implementing IParameter
    ///
    /// Use [RequireEntityIndexInQuery] for parallel jobs that don't use grouping IParameters.
    /// Use [AllowScheduleModes] to restrict the modes that can be scheduled with the job.
    /// Use [Inject] on compatible fields to have them auto-populated.
    /// </remarks>
    public partial interface IJobEach
    {
        /// <summary>
        /// Optionally implement this to perform work before each chunk is processed, or to skip chunks entirely.
        /// </summary>
        /// <param name="context">The job metadata for the chunk. entityIndexInQuery represents the first entity's
        /// index. indexInChunk is 0.</param>
        /// <returns>False to skip the chunk, in which case Execute() is not called for it</returns>
        public bool OnChunkBegin(in JobContext context) => true;

        /// <summary>
        /// Optionally implement this to perform work after each chunk is processed.
        /// </summary>
        /// <param name="context">The job metadata for the chunk. entityIndexInQuery represents the first entity's
        /// index. indexInChunk is 0.</param>
        /// <param name="chunkWasExecuted">Whether Execute() was called for entities in this chunk</param>
        public void OnChunkEnd(in JobContext context, bool chunkWasExecuted)
        {
        }

        #region Impl
        public void __Schedule<TSystem>(LatiosApiInvoker<TSystem> api, EntityQuery query, ScheduleMode mode) where TSystem : unmanaged, ISystem, ILatiosApi
        {
            throw new System.NotImplementedException("This method should have been replaced by codegen.");
        }

        public JobHandle __ScheduleWithDeps<TSystem>(LatiosApiInvoker<TSystem> api, EntityQuery query, ScheduleMode mode,
                                                     JobHandle inputDeps) where TSystem : unmanaged, ISystem, ILatiosApi
        {
            throw new System.NotImplementedException("This method should have been replaced by codegen.");
        }

        public FluentQuery __AppendToQuery(FluentQuery query)
        {
            throw new System.NotImplementedException("This method should have been replaced by codegen.");
        }
        #endregion
    }
}

