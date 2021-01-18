using System.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Latios
{
    public static class BlobBuilderExtensions
    {
        public static BlobBuilderArray<T> ConstructFromNativeArray<T>(this BlobBuilder builder, ref BlobArray<T> ptr, NativeArray<T> array) where T : struct
        {
            var result = builder.Allocate(ref ptr, array.Length);
            for (int i = 0; i < array.Length; i++)
                result[i] = array[i];
            return result;
        }
    }
}

