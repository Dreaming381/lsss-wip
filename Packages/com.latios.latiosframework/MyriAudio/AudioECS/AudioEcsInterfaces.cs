using System;
using Latios.AuxEcs;
using Latios.Unsafe;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Audio;

namespace Latios.Myri
{
    /// <summary>
    /// A managed interface used to setup the Audio ECS runtime on the audio thread.
    /// This instance is maintained through the entire lifecycle of the ECS World,
    /// and should own any unmanaged allocations it creates for the IAudioEcsSystemRunner.
    /// If this instance implements IDisposable, that will be called upon shutdown.
    /// </summary>
    public interface IAudioEcsBootstrap
    {
        public struct Configurator
        {
            internal IAudioEcsSystemRunner.VPtr runnerPtr;
            internal int                        runnerSizeInBytes;
            internal int                        runnerAlignmentInBytes;
            internal long                       tlsfPoolSize;
            internal bool                       configured;

            /// <summary>
            /// Configure the Audio ECS runtime by specifying an IAudioEcsSystemRunner and initial memory reservation
            /// </summary>
            /// <typeparam name="T">An IAudioEcsSystemRunner</typeparam>
            /// <param name="runner">A runner instance only populated with environment context required to configure itself.
            /// Any allocations it contains at this point should solely serve for readonly purposes and be managed by the
            /// IAudioEcsBootstrap. All other allocations such as caches and dynamic collections should be allocated inside
            /// the instance's OnInitialize() implmentation using context.auxWorld.allocator.</param>
            /// <param name="initialMemorySize">The number of bytes that should be initially reserved for Audio ECS.
            /// If Audio ECS has less than half this amount remaining, it will attempt to reserve additional blocks of memory
            /// of this size.</param>
            public unsafe void Configure<T>(in T runner, long initialMemorySize) where T : unmanaged, IAudioEcsSystemRunner
            {
                runnerSizeInBytes      = UnsafeUtility.SizeOf<T>();
                runnerAlignmentInBytes = UnsafeUtility.AlignOf<T>();
                T* ptr                 = (T*)AllocatorManager.Allocate(Allocator.Persistent, runnerSizeInBytes, runnerAlignmentInBytes);
                runnerPtr              = IAudioEcsSystemRunner.VPtr.Create<T>(ptr);
                tlsfPoolSize           = math.max(initialMemorySize, 128 * 1024);  // Work around user specifying a stupidly small value.
                configured             = true;
            }
        }

        /// <summary>
        /// Called on the first frame Myri updates. If you return true, Myri won't
        /// set up the Audio ECS runtime until a source or listener is present in
        /// the main ECS world.
        /// </summary>
        bool ShouldWaitForMyriSourceOrListenerBeforeStarting();
        /// <summary>
        /// Called when Myri is ready to set up the Audio ECS runtime.
        /// </summary>
        void OnStart(ref Configurator configurator);
    }

    /// <summary>
    /// An interface that can be implemented to orchestrate all processing within the Audio ECS thread.
    /// </summary>
    public partial interface IAudioEcsSystemRunner : IVInterface
    {
        public struct AudioFormatChangedContext
        {
            public AuxWorld           auxWorld;
            public RealtimeContext    unityAudioRealtimeContext;
            public FeedbackPipeWriter pipeWriter;
            public int                feedbackID;
            public AudioFormat        newAudioFormat;
        }

        /// <summary>
        /// Called once on the audio thread before any other callback,
        /// and is when any dynamic runtime resources should be allocated.
        /// </summary>
        public void OnInitialize(ref AudioFormatChangedContext context);

        /// <summary>
        /// Called if the format changes during runtime after the first call to OnInitialize().
        /// </summary>
        public void OnAudioFormatChanged(ref AudioFormatChangedContext context);

        public struct UpdateContext
        {
            public AuxWorld              auxWorld;
            public RealtimeContext       unityAudioRealtimeContext;
            public FeedbackPipeWriter    pipeWriter;
            public int                   feedbackID;
            public JobHandle             allRootContextsEarlyProcessingJobHandle;
            public AllVisualFrameUpdates visualFrameUpdates;
            public FinalOutputBuffer     finalOutputBuffer;
        }

        /// <summary>
        /// Called once each DSP mix cycle.
        /// </summary>
        /// <param name="context"></param>
        public void Update(ref UpdateContext context);

        public struct ShutdownContext
        {
            public AuxWorld auxWorld;
        }

        /// <summary>
        /// Called once before the AuxWorld is disposed.
        /// </summary>
        public void OnShutdown(ref ShutdownContext context);
    }

    public struct VisualFrameUpdate
    {
        public int               bufferId;
        public CommandPipeReader pipeReader;
    }

    public struct AllVisualFrameUpdates
    {
        internal UnsafeList<VisualFrameUpdate> updates;

        public UnsafeList<VisualFrameUpdate>.Enumerator GetEnumerator() => updates.GetEnumerator();
    }

    public unsafe struct FinalOutputBuffer
    {
        internal UnsafeList<float> m_buffer;
        internal int               m_channelCount;
        internal int               m_samplesPerChannel;
        internal int               m_sampleRate;
        internal BitField32        m_channelInitialized;

        public int channelCount => m_channelCount;
        public int samplesPerChannel => m_samplesPerChannel;
        public int sampleRate => m_sampleRate;

        /// <summary>
        /// Gets the channel output buffer at the specified index (based on AudioFormat.channelCount).
        /// The buffer's contents are undefined, and must be overwritten if you call this.
        /// If you skip calling this for a specific channel, then that channel will be populated with 0s
        /// automatically.
        /// </summary>
        public Span<float> GetChannel(int channelIndex)
        {
            m_channelInitialized.SetBits(channelIndex, true);
            return new Span<float>(m_buffer.Ptr, m_buffer.Length).Slice(channelIndex * m_samplesPerChannel, m_samplesPerChannel);
        }
    }
}

