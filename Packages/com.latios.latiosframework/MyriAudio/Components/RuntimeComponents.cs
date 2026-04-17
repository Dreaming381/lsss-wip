using System.Threading;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Myri
{
    public unsafe struct AudioEcsAtomicFeedbackId : IComponentData
    {
        internal int* m_atomicCurrentlyProcessingAudioFrameId;

        public int Read() => Volatile.Read(ref *m_atomicCurrentlyProcessingAudioFrameId);
    }
}

