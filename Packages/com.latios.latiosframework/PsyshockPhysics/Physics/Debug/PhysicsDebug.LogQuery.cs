using System;
using System.Runtime.InteropServices;
using AOT;
using Latios.Transforms;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;

namespace Latios.Psyshock
{
    public static unsafe partial class PhysicsDebug
    {
        private delegate void ManagedDelegate(IntPtr context, int operation);
        static ManagedDelegate                                          managedDelegate;
        static readonly SharedStatic<FunctionPointer<ManagedDelegate> > functionPtr = SharedStatic<FunctionPointer<ManagedDelegate> >.GetOrCreate<StaticTag>();

        public static NativeText LogDistanceBetween(in Collider colliderA,
                                                    in TransformQvvs transformA,
                                                    in Collider colliderB,
                                                    in TransformQvvs transformB,
                                                    float maxDistance,
                                                    Allocator allocator = Allocator.Temp)
        {
            var context = new LogDistanceBetweenContext
            {
                colliderA   = colliderA,
                transformA  = transformA,
                colliderB   = colliderB,
                transformB  = transformB,
                maxDistance = maxDistance,
                allocator   = allocator
            };
            DoManagedExecute((IntPtr)(&context), 1);
            return context.result;
        }

        public static void WriteToFile(NativeText text, FixedString512Bytes filepath)
        {
            var context = new WriteToFileContext
            {
                text = text,
                path = filepath
            };
            DoManagedExecute((IntPtr)(&context), 2);
        }

        internal static void LogWarning(NativeText textToLog)
        {
            DoManagedExecute((IntPtr)(&textToLog), 3);
        }

        struct StaticTag { }

        internal static void Initialize()
        {
            managedDelegate  = ManagedExecute;
            functionPtr.Data = new FunctionPointer<ManagedDelegate>(Marshal.GetFunctionPointerForDelegate<ManagedDelegate>(ManagedExecute));
        }

        static void DoManagedExecute(IntPtr context, int operation)
        {
            bool didIt = false;
            ManagedExecuteFromManaged(context, operation, ref didIt);

            if (!didIt)
                functionPtr.Data.Invoke(context, operation);
        }

        [BurstDiscard]
        static void ManagedExecuteFromManaged(IntPtr context, int operation, ref bool didIt)
        {
            didIt = true;
            ManagedExecute(context, operation);
        }

        [MonoPInvokeCallback(typeof(ManagedDelegate))]
        static void ManagedExecute(IntPtr context, int operation)
        {
            try
            {
                switch (operation)
                {
                    case 1:
                    {
                        ref var ctx    = ref *(LogDistanceBetweenContext*)context;
                        var     writer = new HexWriter(ctx.allocator);
                        writer.Write((byte)QueryType.DistanceBetweenColliderCollider);
                        writer.WriteCollider(ctx.colliderA);
                        writer.WriteTransform(ctx.transformA);
                        writer.WriteCollider(ctx.colliderB);
                        writer.WriteTransform(ctx.transformB);
                        writer.WriteFloat(ctx.maxDistance);
                        ctx.result = writer.content;
                        break;
                    }
                    case 2:
                    {
                        ref var ctx = ref *(WriteToFileContext*)context;
                        System.IO.File.WriteAllText(ctx.path.ToString(), ctx.text.ToString());
                        break;
                    }
                    case 3:
                    {
                        ref var text = ref *(NativeText*)context;
                        UnityEngine.Debug.LogWarning($"{text}");
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        struct LogDistanceBetweenContext
        {
            public Collider      colliderA;
            public TransformQvvs transformA;
            public Collider      colliderB;
            public TransformQvvs transformB;
            public float         maxDistance;
            public Allocator     allocator;
            public NativeText    result;
        }

        struct WriteToFileContext
        {
            public NativeText          text;
            public FixedString512Bytes path;
        }

        enum QueryType : byte
        {
            //DistanceBetweenPointCollider = 0,
            DistanceBetweenColliderCollider = 1,
        }

        unsafe struct HexWriter : BinaryWriter
        {
            public NativeText content;

            public HexWriter(Allocator allocator = Allocator.Temp)
            {
                content  = new NativeText(allocator);
                Position = 0;
            }
            public byte* Data => content.GetUnsafePtr();

            public int Length => content.Length;

            public long Position { get; set; }

            public void Dispose()
            {
                content.Dispose();
            }

            public void WriteBytes(void* data, int bytes)
            {
                var bytePtr = (byte*)data;
                for (int i = 0; i < bytes; i++)
                {
                    var b          = *bytePtr;
                    var highNibble = (b >> 4) & 0xf;
                    var lowNibble  = b & 0xf;
                    content.Append(CharFromNibble(highNibble));
                    content.Append(CharFromNibble(lowNibble));
                    bytePtr++;
                    Position++;
                }

                static char CharFromNibble(int nibble)
                {
                    return nibble switch
                           {
                               0 => '0',
                               1 => '1',
                               2 => '2',
                               3 => '3',
                               4 => '4',
                               5 => '5',
                               6 => '6',
                               7 => '7',
                               8 => '8',
                               9 => '9',
                               10 => 'a',
                               11 => 'b',
                               12 => 'c',
                               13 => 'd',
                               14 => 'e',
                               15 => 'f',
                               _ => ' ',
                           };
                }
            }
        }

        unsafe static void WriteCollider<T>(this ref T writer, Collider collider) where T : unmanaged, BinaryWriter
        {
            writer.WriteBytes(UnsafeUtility.AddressOf(ref collider), UnsafeUtility.SizeOf<Collider>());
            if (collider.type == ColliderType.Convex)
            {
                ConvexCollider convex = collider;
                writer.Write(convex.convexColliderBlob);
            }
            else if (collider.type == ColliderType.TriMesh)
            {
                TriMeshCollider triMesh = collider;
                writer.Write(triMesh.triMeshColliderBlob);
            }
            else if (collider.type == ColliderType.Compound)
            {
                CompoundCollider compound = collider;
                writer.Write(compound.compoundColliderBlob);
            }
        }

        unsafe static void WriteTransform<T>(this ref T writer, TransformQvvs transform) where T : unmanaged, BinaryWriter
        {
            writer.WriteBytes(UnsafeUtility.AddressOf(ref transform), UnsafeUtility.SizeOf<TransformQvvs>());
        }

        unsafe static void WriteFloat<T>(this ref T writer, float value) where T : unmanaged, BinaryWriter
        {
            writer.WriteBytes(&value, sizeof(float));
        }
    }
}

