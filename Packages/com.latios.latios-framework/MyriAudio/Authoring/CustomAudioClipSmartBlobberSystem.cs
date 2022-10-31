using System.Collections.Generic;
using Latios.Authoring;
using Latios.Authoring.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Exposed;
using Unity.Entities.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Myri.Authoring
{
    public static class CustomAudioClipBlobberAPIExtensions
    {
        public static unsafe SmartBlobberHandle<AudioClipBlob> RequestCreateBlobAsset(this IBaker baker,
                                                                                      FixedString128Bytes name,
                                                                                      int sampleRate,
                                                                                      int channelCount,
                                                                                      out Entity blobEntity,
                                                                                      int numVoices = 0)
        {
            Entity capturedEntity;
            var    data = new CustomAudioClipBakeData
            {
                name           = name,
                channelCount   = channelCount,
                sampleRate     = sampleRate,
                numVoices      = numVoices,
                capturedEntity = &capturedEntity
            };
            var result = baker.RequestCreateBlobAsset<AudioClipBlob, CustomAudioClipBakeData>(data);
            blobEntity = capturedEntity;
            return result;
        }

        public static unsafe SmartBlobberHandle<AudioClipBlob> RequestCreateBlobAsset(this IBaker baker,
                                                                                      FixedString128Bytes name,
                                                                                      int sampleRate,
                                                                                      out DynamicBuffer<float> monoSamples,
                                                                                      int numVoices = 0)
        {
            DynamicBuffer<CustomAudioClipLeftOrMonoSample> capturedMono;
            var                                            data = new CustomAudioClipBakeData
            {
                name               = name,
                channelCount       = 1,
                sampleRate         = sampleRate,
                numVoices          = numVoices,
                capturedLeftOrMono = &capturedMono
            };
            var result  = baker.RequestCreateBlobAsset<AudioClipBlob, CustomAudioClipBakeData>(data);
            monoSamples = capturedMono.Reinterpret<float>();
            return result;
        }

        public static unsafe SmartBlobberHandle<AudioClipBlob> RequestCreateBlobAsset(this IBaker baker,
                                                                                      FixedString128Bytes name,
                                                                                      int sampleRate,
                                                                                      out DynamicBuffer<float> leftSamples,
                                                                                      out DynamicBuffer<float> rightSamples,
                                                                                      int numVoices = 0)
        {
            DynamicBuffer<CustomAudioClipLeftOrMonoSample> capturedLeft;
            DynamicBuffer<CustomAudioClipRightSample>      capturedRight;
            var                                            data = new CustomAudioClipBakeData
            {
                name               = name,
                channelCount       = 2,
                sampleRate         = sampleRate,
                numVoices          = numVoices,
                capturedLeftOrMono = &capturedLeft,
                capturedRight      = &capturedRight,
            };
            var result   = baker.RequestCreateBlobAsset<AudioClipBlob, CustomAudioClipBakeData>(data);
            leftSamples  = capturedLeft.Reinterpret<float>();
            rightSamples = capturedRight.Reinterpret<float>();
            return result;
        }
    }

    [TemporaryBakingType]
    [InternalBufferCapacity(0)]
    public struct CustomAudioClipLeftOrMonoSample : IBufferElementData
    {
        public float sample;
    }

    [TemporaryBakingType]
    [InternalBufferCapacity(0)]
    public struct CustomAudioClipRightSample : IBufferElementData
    {
        public float sample;
    }

    public unsafe struct CustomAudioClipBakeData : ISmartBlobberRequestFilter<AudioClipBlob>
    {
        public FixedString128Bytes name;
        public int                 channelCount;
        public int                 sampleRate;
        public int                 numVoices;

        internal DynamicBuffer<CustomAudioClipLeftOrMonoSample>* capturedLeftOrMono;
        internal DynamicBuffer<CustomAudioClipRightSample>*      capturedRight;
        internal Entity*                                         capturedEntity;

        public bool Filter(IBaker baker, Entity blobBakingEntity)
        {
            if (channelCount > 2 || channelCount < 1)
            {
                UnityEngine.Debug.LogError($"Myri failed to baked custom clip {name}. Only mono and stereo clips are supported. Please set the channel count to 1 or 2.");
            }
            baker.AddComponent(blobBakingEntity, new CustomAudioClipParametersBlobBakeData
            {
                name       = name,
                sampleRate = sampleRate,
                numVoices  = numVoices
            });
            if (capturedEntity != null)
                *capturedEntity = blobBakingEntity;
            if (capturedLeftOrMono != null)
                *capturedLeftOrMono = baker.AddBuffer<CustomAudioClipLeftOrMonoSample>(blobBakingEntity);
            else
                baker.AddBuffer<CustomAudioClipLeftOrMonoSample>(blobBakingEntity);
            if (channelCount == 2 && capturedRight != null)
                *capturedRight = baker.AddBuffer<CustomAudioClipRightSample>(blobBakingEntity);
            else if (channelCount == 2)
                baker.AddBuffer<CustomAudioClipRightSample>(blobBakingEntity);
            return true;
        }
    }

    [TemporaryBakingType]
    internal struct CustomAudioClipParametersBlobBakeData : IComponentData
    {
        public FixedString128Bytes name;
        public int                 sampleRate;
        public int                 numVoices;
    }
}

namespace Latios.Myri.Authoring.Systems
{
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(SmartBlobberBakingGroup))]
    [BurstCompile]
    public partial struct CustomAudioClipSmartBlobberSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            new SmartBlobberTools<AudioClipBlob>().Register(state.World);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new MonoJob().Schedule();
            new StereoJob().Schedule();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }

        [WithEntityQueryOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)]
        [BurstCompile]
        partial struct MonoJob : IJobEntity
        {
            public void Execute(ref SmartBlobberResult result, in CustomAudioClipParametersBlobBakeData parameters, in DynamicBuffer<CustomAudioClipLeftOrMonoSample> monoSamples)
            {
                var     builder   = new BlobBuilder(Allocator.Temp);
                ref var root      = ref builder.ConstructRoot<AudioClipBlob>();
                var     blobLeft  = builder.Allocate(ref root.samplesLeftOrMono, monoSamples.Length);
                var     blobRight = builder.Allocate(ref root.samplesRight, 1);
                blobRight[0]      = 0f;
                for (int i = 0; i < monoSamples.Length; i++)
                {
                    blobLeft[i] = monoSamples[i].sample;
                }
                int offsetCount = math.max(parameters.numVoices, 1);
                int stride      = blobLeft.Length / offsetCount;
                var offsets     = builder.Allocate(ref root.loopedOffsets, offsetCount);
                for (int i = 0; i < offsetCount; i++)
                {
                    offsets[i] = i * stride;
                }
                root.sampleRate = parameters.sampleRate;
                root.name       = parameters.name;

                result.blob = UnsafeUntypedBlobAssetReference.Create(builder.CreateBlobAssetReference<AudioClipBlob>(Allocator.Persistent));
            }
        }

        [WithEntityQueryOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)]
        [BurstCompile]
        partial struct StereoJob : IJobEntity
        {
            public void Execute(ref SmartBlobberResult result,
                                in CustomAudioClipParametersBlobBakeData parameters,
                                in DynamicBuffer<CustomAudioClipLeftOrMonoSample> leftSamples,
                                in DynamicBuffer<CustomAudioClipRightSample>      rightSamples)
            {
                if (leftSamples.Length != rightSamples.Length)
                {
                    UnityEngine.Debug.LogError($"Myri failed to baked custom clip {parameters.name}. The number of samples provided by the left and right buffers do not match.");
                }

                var     builder   = new BlobBuilder(Allocator.Temp);
                ref var root      = ref builder.ConstructRoot<AudioClipBlob>();
                var     blobLeft  = builder.Allocate(ref root.samplesLeftOrMono, leftSamples.Length);
                var     blobRight = builder.Allocate(ref root.samplesRight, rightSamples.Length);
                for (int i = 0; i < leftSamples.Length; i++)
                {
                    blobLeft[i]  = leftSamples[i].sample;
                    blobRight[i] = rightSamples[i].sample;
                }
                int offsetCount = math.max(parameters.numVoices, 1);
                int stride      = blobLeft.Length / offsetCount;
                var offsets     = builder.Allocate(ref root.loopedOffsets, offsetCount);
                for (int i = 0; i < offsetCount; i++)
                {
                    offsets[i] = i * stride;
                }
                root.sampleRate = parameters.sampleRate;
                root.name       = parameters.name;

                result.blob = UnsafeUntypedBlobAssetReference.Create(builder.CreateBlobAssetReference<AudioClipBlob>(Allocator.Persistent));
            }
        }
    }
}

