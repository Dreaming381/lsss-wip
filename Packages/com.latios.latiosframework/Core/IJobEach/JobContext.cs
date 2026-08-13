using System.Diagnostics;
using Unity.Burst.Intrinsics;
using Unity.Entities;

namespace Latios
{
    public partial interface IJobEach
    {
        /// <summary>
        /// The manner in which an IJobEach was dispatched.
        /// </summary>
        public enum ScheduleMode
        {
            /// <summary>
            /// A single job executing on the main thread
            /// </summary>
            Run,
            /// <summary>
            /// A single job executing on a worker thread
            /// </summary>
            Schedule,
            /// <summary>
            /// Multiple job instances executing across worker threads
            /// </summary>
            ScheduleParallel,
            /// <summary>
            /// Executed inline on the calling thread without any job invocation
            /// </summary>
            RunImmediate
        }

        /// <summary>
        /// All metadata the invoking job knows about the job, chunk, and entity currently being processed.
        /// Specify this as an `in` Execute() parameter of an IJobEach.
        /// </summary>
        /// <remarks>Add an Entity Execute() parameter when you need the entity itself.</remarks>
        public struct JobContext
        {
            internal ArchetypeChunk m_chunk;
            internal v128           m_chunkEnabledMask;
            internal int            m_indexInChunk;
            internal int            m_chunkIndexInQuery;
            internal int            m_entityIndexInQuery;
            internal uint           m_lastSystemVersion;
            internal ScheduleMode   m_mode;
            internal bool           m_useEnabledMask;

            /// <summary>
            /// The chunk containing the entity currently being processed
            /// </summary>
            public ArchetypeChunk chunk => m_chunk;
            /// <summary>
            /// The index of the entity currently being processed within its chunk
            /// </summary>
            public int indexInChunk => m_indexInChunk;
            /// <summary>
            /// The index of the current chunk within the unfiltered EntityQuery.
            /// This is a safe sort key for an EntityCommandBuffer.ParallelWriter.
            /// </summary>
            public int chunkIndexInQuery => m_chunkIndexInQuery;
            /// <summary>
            /// The enabled mask of the current chunk. Only meaningful when useEnabledMask is true.
            /// </summary>
            public v128 chunkEnabledMask => m_chunkEnabledMask;
            /// <summary>
            /// Whether chunkEnabledMask must be consulted to determine which entities in the chunk match the query
            /// </summary>
            public bool useEnabledMask => m_useEnabledMask;
            /// <summary>
            /// The manner in which this job was dispatched
            /// </summary>
            public ScheduleMode mode => m_mode;
            /// <summary>
            /// The global system version of the previous time the scheduling system updated,
            /// for use with change filtering methods such as ArchetypeChunk.DidChange().
            /// </summary>
            public uint lastSystemVersion => m_lastSystemVersion;

            /// <summary>
            /// Whether entityIndexInQuery is available for this dispatch
            /// </summary>
            public bool hasEntityIndexInQuery => m_entityIndexInQuery >= 0;

            /// <summary>
            /// The index of the entity currently being processed among all entities matched by the EntityQuery.
            /// Only available for single-threaded dispatches, jobs using a grouped parameter, and jobs marked
            /// [RequireEntityIndexInQuery]. Throws otherwise.
            /// </summary>
            public int entityIndexInQuery
            {
                get
                {
                    CheckEntityIndexInQueryIsValid(m_entityIndexInQuery);
                    return m_entityIndexInQuery;
                }
            }

            [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
            static void CheckEntityIndexInQueryIsValid(int value)
            {
                if (value < 0)
                    throw new System.InvalidOperationException(
                        "JobContext.entityIndexInQuery is not available for this dispatch. Add [RequireEntityIndexInQuery] to the IJobEach type.");
            }
        }

        /// <summary>
        /// Metadata describing a group of chunks an IJobEach must process together on a single thread.
        /// Passed to the group callbacks of an IGroupedParameterHandle.
        /// </summary>
        public struct GroupContext
        {
            internal int          m_groupIndex;
            internal int          m_chunkCount;
            internal uint         m_lastSystemVersion;
            internal ScheduleMode m_mode;

            /// <summary>
            /// The index of this group among all groups produced for the dispatch
            /// </summary>
            public int groupIndex => m_groupIndex;
            /// <summary>
            /// The number of chunks belonging to this group
            /// </summary>
            public int chunkCount => m_chunkCount;
            /// <summary>
            /// The global system version of the previous time the scheduling system updated
            /// </summary>
            public uint lastSystemVersion => m_lastSystemVersion;
            /// <summary>
            /// The manner in which this job was dispatched
            /// </summary>
            public ScheduleMode mode => m_mode;
        }
    }
}

