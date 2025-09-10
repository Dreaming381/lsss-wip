using System.Runtime.InteropServices;
using Latios;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace UnitTests
{
    [StructLayout(LayoutKind.Explicit)]
    struct TestHashmapPair : IBufferElementData
    {
        [FieldOffset(0)] public DynamicHashMap<ushort, ushort>.Pair element;

        [FieldOffset(4)] ushort readableKey;

        public ushort readKey => readableKey;
    }

    public class DynamicHashMapTests
    {
        [Test]
        public void SequenceA()
        {
            var ecb    = new EntityCommandBuffer(Allocator.Temp);
            var buffer = ecb.AddBuffer<TestHashmapPair>(ecb.CreateEntity());
            var map    = new DynamicHashMap<ushort, ushort>(buffer.Reinterpret<DynamicHashMap<ushort, ushort>.Pair>());

            map.AddOrSet(1,   default);
            map.AddOrSet(256, default);
            map.AddOrSet(257, default);
            map.AddOrSet(258, default);
            map.AddOrSet(513, default);

            Assert.IsTrue(map.TryGetValue(1, out _));
            Assert.IsTrue(map.TryGetValue(256, out _));
            Assert.IsTrue(map.TryGetValue(257, out _));
            Assert.IsTrue(map.TryGetValue(258, out _));
            Assert.IsTrue(map.TryGetValue(513, out _));

            ecb.Dispose();
        }

        [Test]
        public void SequenceB()
        {
            var ecb    = new EntityCommandBuffer(Allocator.Temp);
            var buffer = ecb.AddBuffer<TestHashmapPair>(ecb.CreateEntity());
            var map    = new DynamicHashMap<ushort, ushort>(buffer.Reinterpret<DynamicHashMap<ushort, ushort>.Pair>());

            map.AddOrSet(1,   default);
            map.AddOrSet(2,   default);
            map.AddOrSet(257, default);
            map.AddOrSet(258, default);

            Assert.IsTrue(map.ContainsKey(1));
            Assert.IsTrue(map.ContainsKey(2));
            Assert.IsTrue(map.ContainsKey(257));
            Assert.IsTrue(map.ContainsKey(258));

            ecb.Dispose();
        }

        [Test]
        public void SequenceC()
        {
            var ecb    = new EntityCommandBuffer(Allocator.Temp);
            var buffer = ecb.AddBuffer<TestHashmapPair>(ecb.CreateEntity());
            var map    = new DynamicHashMap<ushort, ushort>(buffer.Reinterpret<DynamicHashMap<ushort, ushort>.Pair>());

            map.AddOrSet(1,   default);
            map.AddOrSet(2,   default);
            map.AddOrSet(5,   default);
            map.AddOrSet(256, default);

            Assert.AreEqual(buffer.Length,     4);
            Assert.AreEqual(map.capacity,      4);
            Assert.AreEqual(buffer[0].readKey, 2);
            Assert.AreEqual(buffer[1].readKey, 1);
            Assert.AreEqual(buffer[2].readKey, 5);
            Assert.AreEqual(buffer[3].readKey, 256);

            Assert.IsTrue(map.Remove(1));

            Assert.AreEqual(buffer.Length,     3);
            Assert.AreEqual(map.capacity,      4);
            Assert.AreEqual(buffer[0].readKey, 2);
            Assert.AreEqual(buffer[1].readKey, 5);
            Assert.AreEqual(buffer[2].readKey, 256);

            Assert.IsTrue(map.Remove(2));

            Assert.AreEqual(buffer.Length,     3);
            Assert.AreEqual(map.capacity,      4);
            Assert.AreEqual(buffer[0].readKey, 256);
            Assert.AreEqual(buffer[1].readKey, 5);

            Assert.IsFalse(map.ContainsKey(1));
            Assert.IsFalse(map.ContainsKey(2));
            Assert.IsTrue(map.ContainsKey(5));
            Assert.IsTrue(map.ContainsKey(256));

            Assert.IsTrue(map.Remove(5));

            ecb.Dispose();
        }
    }
}

