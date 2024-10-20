using UnityEngine;

namespace Latios.LifeFX
{
    public abstract class GraphicsEventTunnelBase : ScriptableObject
    {
        internal abstract TypeInfo GetEventType();

        internal abstract int GetEventIndex();

        internal struct TypeInfo
        {
            public System.Type type;
            public int         size;
            public int         alignment;
        }
    }
}

