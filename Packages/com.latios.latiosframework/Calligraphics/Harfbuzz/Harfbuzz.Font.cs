using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;

namespace HarfbuzzUnity
{
    public static partial class Harfbuzz
    {
        [DllImport(HarfbuzzDll)]
        public static extern IntPtr hb_font_create(IntPtr face);

        [DllImport(HarfbuzzDll)]
        public static extern void hb_font_destroy(IntPtr font);

        [DllImport(HarfbuzzDll)]
        [return : MarshalAs(UnmanagedType.I1)]
        public static extern bool hb_font_get_glyph_extents(IntPtr font, uint glyph, out hb_glyph_extents_t extents);

        [DllImport(HarfbuzzDll)]
        public static extern void hb_font_set_scale(IntPtr font, int x_scale, int y_scale);
    }
}

