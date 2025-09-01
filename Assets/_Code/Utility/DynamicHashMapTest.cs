using System.Collections.Generic;
using Latios;
using Latios.Calci;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class DynamicHashMapTest : MonoBehaviour
{
    void Start()
    {
        var ecb    = new EntityCommandBuffer(Allocator.Temp);
        var entity = ecb.CreateEntity();
        var buffer = ecb.AddBuffer<IntPair>(entity);
        var map    = new DynamicHashMap<int, int>(buffer.Reinterpret<DynamicHashMap<int, int>.Pair>());
        var list   = new NativeList<int>(Allocator.Temp);
        var rng    = new Rng("DynamicHashMapTest").GetSequence(0);

        for (int i = 0; i < 100; i++)
        {
            var v = rng.NextInt(0, 100);
            list.Add(v);
            map.TryAdd(v, v);
        }

        foreach (var v in list)
        {
            if (!map.TryGetValue(v, out var v2))
            {
                UnityEngine.Debug.LogError($"Map corrupted. Failed to find {v}");
            }
            else if (v != v2)
            {
                UnityEngine.Debug.LogError($"Map corrupted. Value does not match key. {v} - {v2}");
            }
        }

        ecb.Dispose();
        UnityEngine.Debug.Log("Done running hashmap test");
    }

    struct IntPair : IBufferElementData
    {
        public DynamicHashMap<int, int>.Pair pair;
    }
}

