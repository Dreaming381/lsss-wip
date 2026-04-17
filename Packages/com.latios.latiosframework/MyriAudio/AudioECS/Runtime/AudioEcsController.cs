using Latios.Unsafe;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Audio;

namespace Latios.Myri
{
    [BurstCompile]
    internal unsafe struct AudioEcsController : RootOutputInstance.IControl<AudioEcsRootOutput>
    {
        TlsfAllocator*                       m_tlsf;
        IAudioEcsSystemRunner.VPtr           m_runner;
        int*                                 m_atomicCurrentlyProcessingAudioFrameId;
        int                                  m_runnerSize;
        int                                  m_runnerAlignment;
        AudioFormat                          m_audioFormat;
        UnsafeList<ControlToRealtimeMessage> m_sentPipes;

        public AudioEcsController(IAudioEcsSystemRunner.VPtr runner, int runnerSize, int runnerAlignment, long tlsfInitialSize)
        {
#if UNITY_EDITOR
            bool warn = true;
#else
            bool warn = false;
#endif
            m_tlsf  = AllocatorManager.Allocate<TlsfAllocator>(Allocator.Persistent);
            *m_tlsf = new TlsfAllocator(Allocator.Persistent, tlsfInitialSize, warn);
            AllocatorManager.Register(ref *m_tlsf);
            m_tlsf->AllocatePool(tlsfInitialSize);

            m_runner                                 = runner;
            m_atomicCurrentlyProcessingAudioFrameId  = AllocatorManager.Allocate<int>(Allocator.Persistent);
            *m_atomicCurrentlyProcessingAudioFrameId = -1;
            m_runnerSize                             = runnerSize;
            m_runnerAlignment                        = runnerAlignment;

            m_audioFormat = default;
            m_sentPipes   = new UnsafeList<ControlToRealtimeMessage>(32, Allocator.Persistent);
        }

        public AudioEcsRootOutput CreateRealtime()
        {
            return new AudioEcsRootOutput(m_tlsf, m_runner);
        }

        public AudioEcsAtomicFeedbackId CreateAtomicFeedbackId()
        {
            return new AudioEcsAtomicFeedbackId { m_atomicCurrentlyProcessingAudioFrameId = m_atomicCurrentlyProcessingAudioFrameId };
        }

        public JobHandle Configure(ControlContext context, ref AudioEcsRootOutput realtime, in AudioFormat format)
        {
            if (format.speakerMode != m_audioFormat.speakerMode || format.bufferFrameCount != m_audioFormat.bufferFrameCount || format.sampleRate != m_audioFormat.sampleRate)
            {
                m_audioFormat = format;
                realtime.Configure(format);
            }
            return default;
        }

        public void Dispose(ControlContext context, ref AudioEcsRootOutput realtime)
        {
            m_tlsf->GetStats(out var bytesUsed, out var bytesTotal);
            if (bytesUsed > 0)
            {
                UnityEngine.Debug.LogWarning($"Audio ECS allocator has detected a leak of {bytesUsed} bytes out of {bytesTotal} bytes reserved.");
            }
            AllocatorManager.Free(Allocator.Persistent, m_runner.ptr.ptr, m_runnerSize, m_runnerAlignment);
            AllocatorManager.Free(Allocator.Persistent, m_atomicCurrentlyProcessingAudioFrameId);
            AllocatorManager.UnmanagedUnregister(ref *m_tlsf);
            m_tlsf->Dispose();
            AllocatorManager.Free(Allocator.Persistent, m_tlsf);
            foreach (var pipe in m_sentPipes)
            {
                foreach (var threadPipe in pipe.commandPipeList)
                {
                    threadPipe.Dispose();
                }
                pipe.commandPipeList.Dispose();
            }
            m_sentPipes.Dispose();
        }

        public ProcessorInstance.Response OnMessage(ControlContext context, ProcessorInstance.Pipe pipe, ProcessorInstance.Message message)
        {
            if (message.Is<ReadControlMessage>())
            {
                int     maxRetiredCommandId = -1;
                ref var readOp              = ref message.Get<ReadControlMessage>();
                foreach (var pipeMessage in pipe.GetAvailableData(context))
                {
                    if (pipeMessage.TryGetData(out RealtimeToControlMessage r2cMessage))
                    {
                        maxRetiredCommandId = math.max(maxRetiredCommandId, r2cMessage.retiredCommandId);
                        readOp.messages.Add(r2cMessage);
                    }
                }

                int dst = 0;
                for (int i = 0; i < m_sentPipes.Length; i++)
                {
                    var commandPipe = m_sentPipes[i];
                    if (commandPipe.commandBufferId <= maxRetiredCommandId)
                    {
                        foreach (var threadedPipe in commandPipe.commandPipeList)
                            threadedPipe.Dispose();
                        commandPipe.commandPipeList.Dispose();
                    }
                    else
                    {
                        m_sentPipes[dst] = commandPipe;
                        dst++;
                    }
                }
                m_sentPipes.Length = dst;
                return ProcessorInstance.Response.Handled;
            }
            if (message.Is<WriteControlMessage>())
            {
                ref var writeOp = ref message.Get<WriteControlMessage>();
                m_sentPipes.Add(writeOp.message);
                pipe.SendData(context, writeOp.message);
                return ProcessorInstance.Response.Handled;
            }
            return ProcessorInstance.Response.Unhandled;
        }

        public void Update(ControlContext context, ProcessorInstance.Pipe pipe)
        {
        }
    }
}

