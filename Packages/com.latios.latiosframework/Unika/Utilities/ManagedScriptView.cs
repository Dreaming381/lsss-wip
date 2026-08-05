using System.Collections;
using System.Collections.Generic;

namespace Latios.Unika
{
    /// <summary>
    /// A managed-heap view of a single script, intended for IDE debuggers and Editor tooling.
    /// Reads and writes go straight through to the live script buffer.
    /// </summary>
    public struct ManagedScriptView
    {
        Script m_script;

        internal ManagedScriptView(Script script) => m_script = script;

        /// <summary>
        /// The concrete type of the script.
        /// </summary>
        public System.Type scriptType => ScriptTypeExtraction.GetScriptType(m_script);

        /// <summary>
        /// The unique instance identifier of the script within its entity.
        /// </summary>
        public int instanceId => m_script.m_headerRO.instanceId;

        /// <summary>
        /// A user byte value which can be used for fast early-out operations without having to load the full script state.
        /// </summary>
        public byte userByte
        {
            get => m_script.userByte;
            set => m_script.userByte = value;
        }

        /// <summary>
        /// The first of two user flag values which can be used for fast early-out operations without having to load the full script state.
        /// </summary>
        public bool userFlagA
        {
            get => m_script.userFlagA;
            set => m_script.userFlagA = value;
        }

        /// <summary>
        /// The second of two user flag values which can be used for fast early-out operations without having to load the full script state.
        /// </summary>
        public bool userFlagB
        {
            get => m_script.userFlagB;
            set => m_script.userFlagB = value;
        }

        /// <summary>
        /// The boxed contents of the script. Getting this value boxes a snapshot of the script struct.
        /// Setting this value copies the new contents into the live script buffer, and throws if the
        /// assigned value is not an instance of scriptType, since the concrete type of a script cannot
        /// be changed through this view.
        /// </summary>
        public object contents
        {
            get
            {
                var receiver = new GetReceiver();
                ScriptTypeExtraction.Extract(m_script, ref receiver);
                return receiver.boxed;
            }
            set
            {
                if (value == null || value.GetType() != scriptType)
                {
                    throw new System.ArgumentException(
                        $"Expected an instance of {scriptType}, got {(value == null ? "null" : value.GetType().ToString())}.");
                }
                var receiver = new SetReceiver { boxed = value };
                ScriptTypeExtraction.Extract(m_script, ref receiver);
            }
        }

        struct GetReceiver : ScriptTypeExtraction.IReceiver
        {
            public object boxed;
            public void Receive<T>(Script<T> script) where T : unmanaged, IUnikaScript, IUnikaScriptGen => boxed = script.valueRO;
        }

        struct SetReceiver : ScriptTypeExtraction.IReceiver
        {
            public object boxed;
            public void Receive<T>(Script<T> script) where T : unmanaged, IUnikaScript, IUnikaScriptGen => script.valueRW = (T)boxed;
        }
    }

    /// <summary>
    /// A lazily-enumerated managed-heap view of all scripts belonging to an entity, intended for
    /// IDE debuggers and editor tooling. Enumerating this does not allocate an array, and only
    /// boxes a script's contents when ManagedScriptView.contents is accessed.
    /// </summary>
    public struct ManagedScriptCollectionView : IEnumerable<ManagedScriptView>
    {
        EntityScriptCollection m_collection;

        internal ManagedScriptCollectionView(EntityScriptCollection collection) => m_collection = collection;

        public Enumerator GetEnumerator() => new Enumerator(m_collection);

        IEnumerator<ManagedScriptView> IEnumerable<ManagedScriptView>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<ManagedScriptView>
        {
            EntityScriptCollection.Enumerator m_inner;

            internal Enumerator(EntityScriptCollection collection) => m_inner = collection.GetEnumerator();

            public ManagedScriptView Current => new ManagedScriptView(m_inner.Current);

            object IEnumerator.Current => Current;

            public bool MoveNext() => m_inner.MoveNext();

            public void Reset() => throw new System.NotSupportedException();

            public void Dispose()
            {
            }
        }
    }
}

