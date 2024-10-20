using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Latios.LifeFX
{
    public abstract class GraphicsEventTunnel<T> : GraphicsEventTunnelBase where T : unmanaged
    {
        internal sealed override TypeInfo GetEventType() => new TypeInfo
        {
            type      = typeof(T),
            size      = UnsafeUtility.SizeOf<T>(),
            alignment = UnsafeUtility.AlignOf<T>(),
        };

        internal sealed override int GetEventIndex()
        {
            GraphicsEventTypeRegistry.Init();
            return GraphicsEventTypeRegistry.TypeToIndex<T>.typeToIndex;
        }
    }
}

