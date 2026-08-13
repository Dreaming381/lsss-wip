using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Jobs;

namespace Latios
{
    /// <summary>
    /// The methods used to dispatch an IJobEach.
    /// </summary>
    /// <remarks>
    /// The api is passed directly to the dispatch, which is also what populates the job's [Inject] fields.
    ///
    /// Overloads which do not take a JobHandle read and update the system's Dependency. Overloads which take a JobHandle leave
    /// Dependency untouched and return the resulting JobHandle.
    ///
    /// Overloads which do not take an EntityQuery use the job's cached default query, also available from
    /// api.GetDefaultQuery&lt;TJob&gt;().
    ///
    /// Unlike IJobEntity, there is not schedule-time EntityQuery compatibility checking. Use the FluentQuery extension FromJob<>()
    /// to ensure compatibility.
    ///
    /// The ByRef overloads take the job by reference to avoid copying a very large job struct.
    ///
    /// Run(), Schedule(), and ScheduleParallel() additionally schedule any setup jobs required by the job's grouped parameters
    /// or by [RequireEntityIndexInQuery]. Grouping analysis is only performed for ScheduleParallel()
    /// </remarks>
    public static class JobEachExtensions
    {
        #region Run
        /// <summary>
        /// Executes the job on the main thread with job's cached default query
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Run<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api) where T : unmanaged, IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            job.__Schedule(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.Run);
        }

        /// <summary>
        /// Executes the job on the main thread with the specified query
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Run<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api, EntityQuery query) where T : unmanaged, IJobEach where TSystem : unmanaged, ISystem,
        ILatiosApi
        {
            job.__Schedule(api, query, IJobEach.ScheduleMode.Run);
        }

        /// <inheritdoc cref="Run{T, TSystem}(T, LatiosApiInvoker{TSystem})" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api) where T : unmanaged, IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            job.__Schedule(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.Run);
        }

        /// <inheritdoc cref="Run{T, TSystem}(T, LatiosApiInvoker{TSystem}, EntityQuery)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api, EntityQuery query) where T : unmanaged, IJobEach where TSystem : unmanaged,
        ISystem, ILatiosApi
        {
            job.__Schedule(api, query, IJobEach.ScheduleMode.Run);
        }
        #endregion

        #region RunImmediate
        /// <summary>
        /// Executes the job inline on the calling thread without any job invocation, using the job's cached default query
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunImmediate<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api) where T : struct, IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            job.__Schedule(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.RunImmediate);
        }

        /// <summary>
        /// Executes the job inline on the calling thread without any job invocation, using the specified query
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunImmediate<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api, EntityQuery query) where T : struct, IJobEach where TSystem : unmanaged,
        ISystem, ILatiosApi
        {
            job.__Schedule(api, query, IJobEach.ScheduleMode.RunImmediate);
        }

        /// <inheritdoc cref="RunImmediate{T, TSystem}(T, LatiosApiInvoker{TSystem})" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunImmediateByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api) where T : struct, IJobEach where TSystem : unmanaged, ISystem,
        ILatiosApi
        {
            job.__Schedule(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.RunImmediate);
        }

        /// <inheritdoc cref="RunImmediate{T, TSystem}(T, LatiosApiInvoker{TSystem}, EntityQuery)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RunImmediateByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api, EntityQuery query) where T : struct,
        IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            job.__Schedule(api, query, IJobEach.ScheduleMode.RunImmediate);
        }
        #endregion

        #region Schedule
        /// <summary>
        /// Schedules the job on a single worker thread, using the job's cached default query.
        /// SystemState.Dependency is read from and assigned to.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Schedule<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api) where T : unmanaged, IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            job.__Schedule(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.Schedule);
        }

        /// <summary>
        /// Schedules the job on a single worker thread, over the specified query.
        /// SystemState.Dependency is read from and assigned to.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Schedule<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api, EntityQuery query) where T : unmanaged, IJobEach where TSystem : unmanaged, ISystem,
        ILatiosApi
        {
            job.__Schedule(api, query, IJobEach.ScheduleMode.Schedule);
        }

        /// <summary>
        /// Schedules the job on a single worker thread, using the job's cached default query
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api, JobHandle inputDeps) where T : unmanaged, IJobEach where TSystem : unmanaged,
        ISystem, ILatiosApi
        {
            return job.__ScheduleWithDeps(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.Schedule, inputDeps);
        }

        /// <summary>
        /// Schedules the job on a single worker thread, over the specified query
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle Schedule<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api, EntityQuery query, JobHandle inputDeps) where T : unmanaged,
        IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            return job.__ScheduleWithDeps(api, query, IJobEach.ScheduleMode.Schedule, inputDeps);
        }

        /// <inheritdoc cref="Schedule{T, TSystem}(T, LatiosApiInvoker{TSystem})" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ScheduleByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api) where T : unmanaged, IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            job.__Schedule(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.Schedule);
        }

        /// <inheritdoc cref="Schedule{T, TSystem}(T, LatiosApiInvoker{TSystem}, EntityQuery)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ScheduleByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api, EntityQuery query) where T : unmanaged, IJobEach where TSystem : unmanaged,
        ISystem, ILatiosApi
        {
            job.__Schedule(api, query, IJobEach.ScheduleMode.Schedule);
        }

        /// <inheritdoc cref="Schedule{T, TSystem}(T, LatiosApiInvoker{TSystem}, JobHandle)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api, JobHandle inputDeps) where T : unmanaged,
        IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            return job.__ScheduleWithDeps(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.Schedule, inputDeps);
        }

        /// <inheritdoc cref="Schedule{T, TSystem}(T, LatiosApiInvoker{TSystem}, EntityQuery, JobHandle)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api, EntityQuery query, JobHandle inputDeps) where T : unmanaged,
        IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            return job.__ScheduleWithDeps(api, query, IJobEach.ScheduleMode.Schedule, inputDeps);
        }
        #endregion

        #region ScheduleParallel
        /// <summary>
        /// Schedules the job across multiple worker threads, using the job's cached default query.
        /// SystemState.Dependency is read from and assigned to.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ScheduleParallel<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api) where T : unmanaged, IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            job.__Schedule(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.ScheduleParallel);
        }

        /// <summary>
        /// Schedules the job across multiple worker threads, using the specified query.
        /// SystemState.Dependency is read from and assigned to.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ScheduleParallel<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api, EntityQuery query) where T : unmanaged, IJobEach where TSystem : unmanaged,
        ISystem, ILatiosApi
        {
            job.__Schedule(api, query, IJobEach.ScheduleMode.ScheduleParallel);
        }

        /// <summary>
        /// Schedules the job across multiple worker threads, using the job's cached default query
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleParallel<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api, JobHandle inputDeps) where T : unmanaged,
        IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            return job.__ScheduleWithDeps(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.ScheduleParallel, inputDeps);
        }

        /// <summary>
        /// Schedules the job across multiple worker threads, using the specified query
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleParallel<T, TSystem>(this T job, LatiosApiInvoker<TSystem> api, EntityQuery query, JobHandle inputDeps) where T : unmanaged,
        IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            return job.__ScheduleWithDeps(api, query, IJobEach.ScheduleMode.ScheduleParallel, inputDeps);
        }

        /// <inheritdoc cref="ScheduleParallel{T, TSystem}(T, LatiosApiInvoker{TSystem})" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ScheduleParallelByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api) where T : unmanaged, IJobEach where TSystem : unmanaged, ISystem,
        ILatiosApi
        {
            job.__Schedule(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.ScheduleParallel);
        }

        /// <inheritdoc cref="ScheduleParallel{T, TSystem}(T, LatiosApiInvoker{TSystem}, EntityQuery)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ScheduleParallelByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api, EntityQuery query) where T : unmanaged,
        IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            job.__Schedule(api, query, IJobEach.ScheduleMode.ScheduleParallel);
        }

        /// <inheritdoc cref="ScheduleParallel{T, TSystem}(T, LatiosApiInvoker{TSystem}, JobHandle)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleParallelByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api, JobHandle inputDeps) where T : unmanaged,
        IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            return job.__ScheduleWithDeps(api, api.GetDefaultQuery<T>(), IJobEach.ScheduleMode.ScheduleParallel, inputDeps);
        }

        /// <inheritdoc cref="ScheduleParallel{T, TSystem}(T, LatiosApiInvoker{TSystem}, EntityQuery, JobHandle)" />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JobHandle ScheduleParallelByRef<T, TSystem>(ref this T job, LatiosApiInvoker<TSystem> api, EntityQuery query, JobHandle inputDeps) where T : unmanaged,
        IJobEach where TSystem : unmanaged, ISystem, ILatiosApi
        {
            return job.__ScheduleWithDeps(api, query, IJobEach.ScheduleMode.ScheduleParallel, inputDeps);
        }
        #endregion
    }
}

