using Latios.CalligraphicsV1.Rendering;
using Unity.Burst;
using Unity.Entities;

namespace Latios.CalligraphicsV1
{
    internal interface ITransitionProvider
    {
        void Initialize(ref TextAnimationTransition transition, ref Rng.RngSequence rng, GlyphMapper glyphMapper);

        void SetValue(ref DynamicBuffer<RenderGlyph> renderGlyphs, TextAnimationTransition transition, GlyphMapper glyphMapper, int startIndex, int endIndex, float normalizedTime);

        void DisposeTransition(ref TextAnimationTransition transition)
        {
        }
    }

    internal interface ITransitionProvider<out T> where T : unmanaged, ITransitionProvider<T>
    {
    }
}

