using System;
using Unity.Audio;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Latios.Myri
{
    internal unsafe struct ListenerGraphState : ICleanupComponentData
    {
        public DSPNode                                 listenerMixNode;
        public DSPConnection                           masterOutputConnection;
        public int                                     masterPortIndex;
        public int                                     inletPortCount;
        public UnsafeList<DSPConnection>               ildConnections;
        public BlobAssetReference<ListenerProfileBlob> lastUsedProfile;
    }

    internal struct TrackedListener : ICleanupComponentData
    {
        public byte packed;
        public bool hasChannelIDs
        {
            get => Bits.GetBit(packed, 0);
            set => Bits.SetBit(ref packed, 0, value);
        }
    }

    internal partial struct AudioEcsBootstrapCarrier : IManagedStructComponent, IDisposable
    {
        public IAudioEcsBootstrap bootstrap;

        public void Dispose()
        {
            if (bootstrap is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}

