using Latios.Myri.DSP;
using Unity.Audio;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Latios.Myri
{
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct BrickwallLimiterNode : IAudioKernel<BrickwallLimiterNode.Parameters, BrickwallLimiterNode.SampleProviders>
    {
        public enum Parameters
        {
            Unused
        }
        public enum SampleProviders
        {
            Unused
        }

        BrickwallLimiter m_limiter;
        int              m_expectedSampleBufferSize;

        public void Initialize()
        {
            m_limiter = new BrickwallLimiter(BrickwallLimiter.kDefaultPreGain,
                                             BrickwallLimiter.kDefaultVolume,
                                             BrickwallLimiter.kDefaultReleaseDBPerSample,
                                             BrickwallLimiter.kDefaultLookaheadSampleCount,
                                             Allocator.AudioKernel);
            m_expectedSampleBufferSize = 1024;
        }

        public void Dispose() => m_limiter.Dispose();

        public void Execute(ref ExecuteContext<Parameters, SampleProviders> context)
        {
            // Assume stereo in, stereo out
            if (context.Outputs.Count <= 0)
                return;
            var outputBuffer = context.Outputs.GetSampleBuffer(0);
            if (outputBuffer.Channels <= 1)
            {
                ZeroSampleBuffer(outputBuffer);
                return;
            }
            if (context.Inputs.Count <= 0)
            {
                ZeroSampleBuffer(outputBuffer);
                return;
            }
            var inputBuffer = context.Inputs.GetSampleBuffer(0);
            if (inputBuffer.Channels <= 1)
            {
                ZeroSampleBuffer(outputBuffer);
                return;
            }

            if (context.DSPBufferSize != m_expectedSampleBufferSize)
            {
                m_limiter.releasePerSampleDB *= (float)context.DSPBufferSize / m_expectedSampleBufferSize;
                m_expectedSampleBufferSize    = context.DSPBufferSize;
            }

            var inputL  = inputBuffer.GetBuffer(0);
            var inputR  = inputBuffer.GetBuffer(1);
            var outputL = outputBuffer.GetBuffer(0);
            var outputR = outputBuffer.GetBuffer(1);
            int length  = outputL.Length;

            for (int i = 0; i < length; i++)
            {
                m_limiter.ProcessSample(inputL[i], inputR[i], out var leftOut, out var rightOut);
                outputL[i] = leftOut;
                outputR[i] = rightOut;
            }
        }

        void ZeroSampleBuffer(SampleBuffer sb)
        {
            for (int c = 0; c < sb.Channels; c++)
            {
                var b = sb.GetBuffer(c);
                b.AsSpan().Clear();
            }
        }
    }
}

