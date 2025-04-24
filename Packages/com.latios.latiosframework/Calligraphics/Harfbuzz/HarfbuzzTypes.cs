using Unity.Collections;
using Unity.Mathematics;

namespace HarfbuzzUnity
{
    public struct hb_glyph_extents_t
    {
        public int x_bearing;  // Distance from the x-origin to the left extremum of the glyph.
        public int y_bearing;  // Distance from the top extremum of the glyph to the y-origin.
        public int width;  // Distance from the left extremum of the glyph to the right extremum.
        public int height;  // Distance from the top extremum of the glyph to the bottom extremum.
    }
}

