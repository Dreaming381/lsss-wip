namespace Latios
{
    /// <summary>
    /// The FluentQuery methods used to compose an IJobEach's query requirements into a query built by hand.
    /// </summary>
    public static class JobEachFluentExtensions
    {
        /// <summary>
        /// Adds everything an IJobEach requires of its entities (including attributes) to the query under construction.
        /// Use this to build a custom query guaranteed to satisfy the job, then pass that query to the job's dispatch method.
        /// </summary>
        /// <typeparam name="T">The IJobEach whose requirements to add</typeparam>
        /// <param name="query">The query under construction</param>
        /// <returns>The query with the job's requirements appended</returns>
        public static FluentQuery FromJob<T>(this FluentQuery query) where T : unmanaged, IJobEach
        {
            return default(T).__AppendToQuery(query);
        }
    }
}

