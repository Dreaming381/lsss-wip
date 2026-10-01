using System;
using System.Text;
using Unity.Collections;

// Todo: This does 32-bit to 16-bit casts, and may not work correctly in some situations.

namespace Latios
{
    public static class StringBuilderExtensions
    {
        public static void Append(this StringBuilder builder, in FixedString32Bytes fixedString)
        {
            foreach (var c in fixedString)
            {
                builder.Append((char)c.value);
            }
        }

        public static void Append(this StringBuilder builder, in FixedString64Bytes fixedString)
        {
            foreach (var c in fixedString)
            {
                builder.Append((char)c.value);
            }
        }

        public static void Append(this StringBuilder builder, in FixedString128Bytes fixedString)
        {
            foreach (var c in fixedString)
            {
                builder.Append((char)c.value);
            }
        }

        public static void Append(this StringBuilder builder, in FixedString512Bytes fixedString)
        {
            foreach (var c in fixedString)
            {
                builder.Append((char)c.value);
            }
        }

        public static void Append(this StringBuilder builder, in FixedString4096Bytes fixedString)
        {
            foreach (var c in fixedString)
            {
                builder.Append((char)c.value);
            }
        }
    }
}

