using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using Hash128 = Unity.Entities.Hash128;

namespace Latios.Myri.Authoring
{
    public abstract class AudioIldProfileBuilder : ScriptableObject
    {
        protected abstract void ComputeProfile();

        protected void AddChannel(float2 minMaxHorizontalAngleInRadiansCounterClockwiseFromRight, float2 minMaxVerticalAngleInRadians, float panFilterRatio, bool isRightChannel)
        {
            if (m_anglesPerLeftChannel.Length + m_anglesPerRightChannel.Length >= 127)
                throw new InvalidOperationException("An IldProfile only supports up to 127 channels");

            if (isRightChannel)
            {
                m_anglesPerRightChannel.Add(new float4(minMaxHorizontalAngleInRadiansCounterClockwiseFromRight, minMaxVerticalAngleInRadians));
                m_panFilterRatiosPerRightChannel.Add(panFilterRatio);
            }
            else
            {
                m_anglesPerLeftChannel.Add(new float4(minMaxHorizontalAngleInRadiansCounterClockwiseFromRight, minMaxVerticalAngleInRadians));
                m_panFilterRatiosPerLeftChannel.Add(panFilterRatio);
            }
        }

        protected void SetChannelPanFilterRatio(float panFilterRatio, int channelIndex, bool isRightChannel)
        {
            if (isRightChannel)
            {
                m_panFilterRatiosPerRightChannel[channelIndex] = panFilterRatio;
            }
            else
            {
                m_panFilterRatiosPerLeftChannel[channelIndex] = panFilterRatio;
            }
        }

        protected void AddFilterToChannel(FrequencyFilter filter, int channelIndex, bool isRightChannel)
        {
            if (isRightChannel)
            {
                m_filtersRight.Add(filter);
                m_channelIndicesRight.Add(channelIndex);
            }
            else
            {
                m_filtersLeft.Add(filter);
                m_channelIndicesLeft.Add(channelIndex);
            }
        }

        #region Internals
        private NativeList<FrequencyFilter> m_filtersLeft;
        private NativeList<int>             m_channelIndicesLeft;
        private NativeList<FrequencyFilter> m_filtersRight;
        private NativeList<int>             m_channelIndicesRight;

        private NativeList<float4> m_anglesPerLeftChannel;
        private NativeList<float4> m_anglesPerRightChannel;
        private NativeList<float>  m_panFilterRatiosPerLeftChannel;
        private NativeList<float>  m_panFilterRatiosPerRightChannel;

        private bool                               m_computedHash = false;
        private Hash128                            m_hash;
        private BlobAssetReference<IldProfileBlob> m_blobProfile;

        internal Hash128 ComputeHash()
        {
            m_computedHash = false;
            m_blobProfile  = default;

            m_filtersLeft.Clear();
            m_filtersRight.Clear();
            m_channelIndicesLeft.Clear();
            m_channelIndicesRight.Clear();
            m_anglesPerLeftChannel.Clear();
            m_anglesPerRightChannel.Clear();
            m_panFilterRatiosPerLeftChannel.Clear();
            m_panFilterRatiosPerRightChannel.Clear();

            ComputeProfile();

            var job = new ComputeHashJob
            {
                filtersLeft                    = m_filtersLeft,
                channelIndicesLeft             = m_channelIndicesLeft,
                filtersRight                   = m_filtersRight,
                channelIndicesRight            = m_channelIndicesRight,
                anglesPerLeftChannel           = m_anglesPerLeftChannel,
                anglesPerRightChannel          = m_anglesPerRightChannel,
                panFilterRatiosPerLeftChannel  = m_panFilterRatiosPerLeftChannel,
                panFilterRatiosPerRightChannel = m_panFilterRatiosPerRightChannel,
                result                         = new NativeReference<Hash128>(Allocator.TempJob)
            };
            job.Run();
            m_hash = job.result.Value;
            job.result.Dispose();
            m_computedHash = true;
            return m_hash;
        }

        internal BlobAssetReference<IldProfileBlob> ComputeBlob()
        {
            if (m_blobProfile.IsCreated)
                return m_blobProfile;

            if (!m_computedHash)
                ComputeHash();

            var     builder = new BlobBuilder(Allocator.Temp);
            ref var root    = ref builder.ConstructRoot<IldProfileBlob>();
            builder.ConstructFromNativeArray(ref root.filtersLeft,                    m_filtersLeft);
            builder.ConstructFromNativeArray(ref root.channelIndicesLeft,             m_channelIndicesLeft);
            builder.ConstructFromNativeArray(ref root.filtersRight,                   m_filtersRight);
            builder.ConstructFromNativeArray(ref root.channelIndicesRight,            m_channelIndicesRight);

            builder.ConstructFromNativeArray(ref root.anglesPerLeftChannel,           m_anglesPerLeftChannel);
            builder.ConstructFromNativeArray(ref root.anglesPerRightChannel,          m_anglesPerRightChannel);
            builder.ConstructFromNativeArray(ref root.panFilterRatiosPerLeftChannel,  m_panFilterRatiosPerLeftChannel);
            builder.ConstructFromNativeArray(ref root.panFilterRatiosPerRightChannel, m_panFilterRatiosPerRightChannel);

            m_blobProfile = builder.CreateBlobAssetReference<IldProfileBlob>(Allocator.Persistent);
            return m_blobProfile;
        }

        [BurstCompile]
        private struct ComputeHashJob : IJob
        {
            public NativeList<FrequencyFilter> filtersLeft;
            public NativeList<int>             channelIndicesLeft;
            public NativeList<FrequencyFilter> filtersRight;
            public NativeList<int>             channelIndicesRight;

            public NativeList<float4> anglesPerLeftChannel;
            public NativeList<float4> anglesPerRightChannel;
            public NativeList<float>  panFilterRatiosPerLeftChannel;
            public NativeList<float>  panFilterRatiosPerRightChannel;

            public NativeReference<Hash128> result;

            public unsafe void Execute()
            {
                var bytes = new NativeList<byte>(Allocator.Temp);
                bytes.AddRange(filtersLeft.AsArray().Reinterpret<byte>(UnsafeUtility.SizeOf<FrequencyFilter>()));
                bytes.AddRange(filtersRight.AsArray().Reinterpret<byte>(UnsafeUtility.SizeOf<FrequencyFilter>()));
                bytes.AddRange(channelIndicesLeft.AsArray().Reinterpret<byte>(UnsafeUtility.SizeOf<int>()));
                bytes.AddRange(channelIndicesRight.AsArray().Reinterpret<byte>(UnsafeUtility.SizeOf<int>()));
                bytes.AddRange(anglesPerLeftChannel.AsArray().Reinterpret<byte>(UnsafeUtility.SizeOf<float4>()));
                bytes.AddRange(anglesPerRightChannel.AsArray().Reinterpret<byte>(UnsafeUtility.SizeOf<float4>()));
                bytes.AddRange(panFilterRatiosPerLeftChannel.AsArray().Reinterpret<byte>(UnsafeUtility.SizeOf<float>()));
                bytes.AddRange(panFilterRatiosPerRightChannel.AsArray().Reinterpret<byte>(UnsafeUtility.SizeOf<float>()));

                uint4 temp   = xxHash3.Hash128(bytes.GetUnsafePtr(), bytes.Length);
                result.Value = new Hash128(temp);
            }
        }

        private void OnEnable()
        {
            m_filtersLeft                    = new NativeList<FrequencyFilter>(Allocator.Persistent);
            m_filtersRight                   = new NativeList<FrequencyFilter>(Allocator.Persistent);
            m_channelIndicesLeft             = new NativeList<int>(Allocator.Persistent);
            m_channelIndicesRight            = new NativeList<int>(Allocator.Persistent);
            m_anglesPerLeftChannel           = new NativeList<float4>(Allocator.Persistent);
            m_anglesPerRightChannel          = new NativeList<float4>(Allocator.Persistent);
            m_panFilterRatiosPerLeftChannel  = new NativeList<float>(Allocator.Persistent);
            m_panFilterRatiosPerRightChannel = new NativeList<float>(Allocator.Persistent);
        }

        private void OnDisable()
        {
            {
                m_filtersLeft.Dispose();
                m_filtersRight.Dispose();
                m_channelIndicesLeft.Dispose();
                m_channelIndicesRight.Dispose();
                m_anglesPerLeftChannel.Dispose();
                m_anglesPerRightChannel.Dispose();
                m_panFilterRatiosPerLeftChannel.Dispose();
                m_panFilterRatiosPerRightChannel.Dispose();
            }
        }
        #endregion
    }
}

