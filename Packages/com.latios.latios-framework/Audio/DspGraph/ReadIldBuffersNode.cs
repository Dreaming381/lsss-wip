using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Audio
{
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct ReadIldBuffersNode : IAudioKernel<ReadIldBuffersNode.Parameters, ReadIldBuffersNode.SampleProviders>
    {
        public enum Parameters
        {
            Unused
        }
        public enum SampleProviders
        {
            Unused
        }

        int       m_currentFrame;
        int       m_currentSubframe;
        int       m_lastPlayedBufferID;
        IldBuffer m_ildBuffer;

        internal IldBuffer m_queuedIldBuffer;

        public void Initialize()
        {
            m_currentFrame       = 0;
            m_currentSubframe    = 0;
            m_lastPlayedBufferID = -1;
            m_ildBuffer          = default;
            m_queuedIldBuffer    = default;
        }

        public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
        {
            bool bufferStarved = false;

            m_currentSubframe++;
            if (m_currentSubframe >= m_ildBuffer.subframesPerFrame)
            {
                m_currentSubframe = 0;
                m_currentFrame++;
                m_lastPlayedBufferID = m_ildBuffer.bufferId;
                m_ildBuffer          = m_queuedIldBuffer;
            }

            for (int outputChannelIndex = 0; outputChannelIndex < context.Outputs.Count; outputChannelIndex++)
            {
                var channelOutput = context.Outputs.GetSampleBuffer(outputChannelIndex);
                var outputBuffer  = channelOutput.GetBuffer(0);
                if (m_ildBuffer.leftBufferChannels.Length + m_ildBuffer.rightBufferChannels.Length < context.Outputs.Count)
                {
                    for (int i = 0; i < outputBuffer.Length; i++)
                    {
                        outputBuffer[i] = 0f;
                    }
                }
                else if (m_currentFrame - m_ildBuffer.frame >= m_ildBuffer.framesInBuffer)
                {
                    for (int i = 0; i < outputBuffer.Length; i++)
                    {
                        outputBuffer[i] = 0f;
                    }
                    bufferStarved = true;
                }
                else
                {
                    bool useLeft               = outputChannelIndex < m_ildBuffer.leftBufferChannels.Length;
                    int  ildBufferChannelIndex = math.select(outputChannelIndex - m_ildBuffer.leftBufferChannels.Length, outputChannelIndex, useLeft);
                    var  ildBufferChannel      = useLeft ? m_ildBuffer.leftBufferChannels[ildBufferChannelIndex] : m_ildBuffer.rightBufferChannels[ildBufferChannelIndex];

                    int bufferOffset  = m_ildBuffer.subframesPerFrame * (m_currentFrame - m_ildBuffer.frame) + m_currentSubframe;
                    bufferOffset     *= outputBuffer.Length;
                    for (int i = 0; i < outputBuffer.Length; i++)
                    {
                        outputBuffer[i] = ildBufferChannel.buffer[bufferOffset + i];
                    }
                }
            }

            if (bufferStarved && m_ildBuffer.warnIfStarved)
            {
                UnityEngine.Debug.LogWarning(
                    $"Dsp buffer starved. Kernel frame: {m_currentFrame}, IldBuffer frame: {m_ildBuffer.frame}, ildBuffer Id: {m_ildBuffer.bufferId}, frames in buffer: {m_ildBuffer.framesInBuffer}, subframe: {m_currentSubframe}/{m_ildBuffer.subframesPerFrame}");
            }

            if (m_currentSubframe == 0)
            {
                //Todo: Dispatch both to the ECS job chain for realtime audio frame updates and to the main thread for buffer disposal.
            }
        }

        public void Dispose()
        {
        }
    }

    internal unsafe struct MixWithDfgNodeUpdate : IAudioKernelUpdate<ReadIldBuffersNode.Parameters, ReadIldBuffersNode.SampleProviders, ReadIldBuffersNode>
    {
        public IldBuffer ildBuffer;

        public void Update(ref ReadIldBuffersNode audioKernel)
        {
            audioKernel.m_queuedIldBuffer = ildBuffer;
        }
    }
}

