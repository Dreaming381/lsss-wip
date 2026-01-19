using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios
{
    /// <summary>
    /// A type that accelerates checking if a chunk has a component type without the need for a type handle.
    /// You can make this a defaulted private field in a job and use it right away.
    /// </summary>
    /// <typeparam name="T">The component type you want to check for existence.</typeparam>
    public struct HasChecker<T>
    {
        EntityArchetype previousArchetype;
        bool            previousResult;

        public bool this[ArchetypeChunk chunk]
        {
            get
            {
                var archetype = chunk.Archetype;
                if (archetype == previousArchetype)
                    return previousResult;
                previousArchetype = archetype;
                previousResult    = chunk.Has<T>();
                return previousResult;
            }
        }
    }
}

