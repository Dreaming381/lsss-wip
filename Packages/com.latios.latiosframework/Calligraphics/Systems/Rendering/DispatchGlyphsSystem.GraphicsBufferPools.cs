using System;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace TextMeshDOTS
{
    public partial class DispatchGlyphsSystem
    {
        struct GraphicsBufferUploadPool : IDisposable
        {
            GraphicsBuffer[] buffers;
            int nextIndex;
            GraphicsBuffer.Target target;
            int stride;

            public GraphicsBufferUploadPool(int initialCapacities, GraphicsBuffer.Target target, int stride)
            {
                // SparseUploader.MaxFramesInFlight is internally, so we just use the max here.
                int numRotations = math.max(4, QualitySettings.maxQueuedFrames);
                nextIndex = 0;
                this.target = target;
                this.stride = stride;
                buffers = new GraphicsBuffer[numRotations];
                for (int i = 0; i < numRotations; i++)
                {
                    buffers[i] = new GraphicsBuffer(target, GraphicsBuffer.UsageFlags.LockBufferForWrite, initialCapacities, stride);
                }
            }

            public void Dispose()
            {
                foreach (var buffer in buffers)
                    buffer.Dispose();
                buffers = null;
            }

            public GraphicsBuffer Allocate(int requiredElements)
            {
                var buffer = buffers[nextIndex];
                if (buffer.count < requiredElements)
                {
                    buffer.Dispose();
                    buffer = new GraphicsBuffer(target, GraphicsBuffer.UsageFlags.LockBufferForWrite, math.ceilpow2(requiredElements), stride);
                    buffers[nextIndex] = buffer;
                }
                nextIndex++;
                if (nextIndex == buffers.Length)
                    nextIndex = 0;
                return buffer;
            }
        }

        internal struct PersistentBuffer : IDisposable
        {
            GraphicsBuffer m_currentBuffer;
            ComputeShader m_copyShader;
            uint m_currentSize;
            uint m_stride;
            GraphicsBuffer.Target m_bindingTarget;

            static readonly SharedStatic<CopyShaderNames> s_copyShaderNames = SharedStatic<CopyShaderNames>.GetOrCreate<PersistentBuffer>();

            struct CopyShaderNames
            {
                public int _src;
                public int _dst;
                public int _start;
            }

            public static void InitStatics()
            {
                s_copyShaderNames.Data = new CopyShaderNames
                {
                    _src = Shader.PropertyToID("_src"),
                    _dst = Shader.PropertyToID("_dst"),
                    _start = Shader.PropertyToID("_start")
                };
            }

            public PersistentBuffer(uint initialSize,
                                    uint stride,
                                    GraphicsBuffer.Target bufferType,
                                    ComputeShader copyShader)
            {
                uint size = math.ceilpow2(initialSize);
                m_currentBuffer = new GraphicsBuffer(bufferType, GraphicsBuffer.UsageFlags.None, (int)size, (int)stride);
                m_copyShader = copyShader;
                m_currentSize = size;
                m_stride = stride;
                m_bindingTarget = bufferType;
            }

            public void Dispose()
            {
                m_currentBuffer.Dispose();
                this = default;
            }

            public bool valid => m_currentBuffer.IsValid();

            public GraphicsBuffer GetBufferNoResize() => m_currentBuffer;

            public GraphicsBuffer GetBuffer(uint requiredSize)
            {
                //UnityEngine.Debug.Log($"Requested Persistent Buffer of size: {requiredSize} while currentSize is: {m_currentSize}");
                if (requiredSize <= m_currentSize)
                    return m_currentBuffer;

                uint size = math.ceilpow2(requiredSize);
                if (requiredSize * m_stride > 1024 * 1024 * 1024)
                    Debug.LogWarning("Attempted to allocate a persistent graphics buffer over 1 GB. Rendering artifacts may occur.");
                if (requiredSize * m_stride < 1024 * 1024 * 1024 && size * m_stride > 1024 * 1024 * 1024)
                    size = 1024 * 1024 * 1024 / m_stride;
                var prevBuffer = m_currentBuffer;
                m_currentBuffer = new GraphicsBuffer(m_bindingTarget, GraphicsBuffer.UsageFlags.None, (int)size, (int)m_stride);
                if (m_copyShader != null)
                {
                    m_copyShader.GetKernelThreadGroupSizes(0, out var threadGroupSize, out _, out _);
                    m_copyShader.SetBuffer(0, s_copyShaderNames.Data._dst, m_currentBuffer);
                    m_copyShader.SetBuffer(0, s_copyShaderNames.Data._src, prevBuffer);
                    uint copySize = m_currentSize;
                    for (uint dispatchesRemaining = (copySize + threadGroupSize - 1) / threadGroupSize, start = 0; dispatchesRemaining > 0;)
                    {
                        uint dispatchCount = math.min(dispatchesRemaining, 65535);
                        m_copyShader.SetInt(s_copyShaderNames.Data._start, (int)(start * threadGroupSize));
                        m_copyShader.Dispatch(0, (int)dispatchCount, 1, 1);
                        dispatchesRemaining -= dispatchCount;
                        start += dispatchCount;
                        //UnityEngine.Debug.Log($"Dispatched buffer type: {m_bindingTarget} with dispatchCount: {dispatchCount}");
                    }
                }
                m_currentSize = size;
                prevBuffer.Dispose();
                return m_currentBuffer;
            }
        }
    }
}
