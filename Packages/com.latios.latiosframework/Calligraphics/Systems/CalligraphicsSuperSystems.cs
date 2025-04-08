using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Latios.Calligraphics.Systems
{
    [DisableAutoCreation]
    public partial class AnimateGlyphsSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            EnableSystemSorting = true;
        }
    }

    [DisableAutoCreation]
    public partial class CalligraphicsUpdateSuperSystem : SuperSystem
    {
        protected override void CreateSystems()
        {
            GetOrCreateAndAddUnmanagedSystem<StreamingFontRegistrationSystem>();
            GetOrCreateAndAddUnmanagedSystem<SystemFontRegistrationSystem>();
            GetOrCreateAndAddUnmanagedSystem<TextPrepassSystem>();
            GetOrCreateAndAddUnmanagedSystem<FontLoadSystem>();
            GetOrCreateAndAddUnmanagedSystem<GenerateGlyphsSystem>();
            GetOrCreateAndAddManagedSystem<AnimateGlyphsSuperSystem>();
            GetOrCreateAndAddUnmanagedSystem<UpdateGlyphsRenderersSystem>();
        }
    }
}

/*
   WIP Design notes for pipeline:

   StreamingFontRegistrationSystem
   - Query: FontCollectionBlobReference
   - Single-threaded chunk job adding of any unregistered fonts to tables.
   - Maybe preload all fonts on web?

   SystemFontRegistrationSystem
   - Query: CalliByte (to avoid loading when feature is unused)
   - Single-threaded unbursted job to load system fonts to tables
   - Disables itself after running once in a world
   - Depends on StreamingFontRegistrationSystem

   TextPrepassSystem
   - Query: CalliBytes (change filtered)
   - First, parallel job to apply new PreviousCalliBytes from last frame
   - Second, parallel chunk job
   - If PreviousCalliBytes present, compare with CalliBytes to detect dirtiness
   - Parse XML tags for each dirty entity (and maybe store tags in NativeStream?)
   - Scan for fonts and styles not currently used and add to per-thread collection, along with fallback info
   - Add PreviousCalliBytes, ResidentRange, and PreviousRenderGlyphs via ACCB if missing
   - Depends on SystemFontRegistrationSystem and StreamingFontRegistrationSystem

   FontLoadSystem
   - Query: CalliByte (to avoid running when feature is unused)
   - One or more jobs (which parts are parallel should be profiler-guided) which analyzes all used fonts and styles
   - Uses font tables to compute fallbacks and identify unloaded fonts and styles
   - Loads unloaded fonts and styles and creates harfbuzz objects, stored in the tables
   - Depends on TextPrepassSystem

   GenerateGlyphsSystem
   - Query: CalliByte (dirty), RenderGlyphs
   - First, parallel chunk job that does shaping, exporting to NativeStream with embedded UnsafeLists
   - First job also collects missing glyphs and adds them to per-thread collection
   - Second single-threaded job merges missing glyphs, allocates table space and slots for each, and populates inputs for third job
   - Third parallel job computes glyph info and writes data into slots
   - Fourth parallel chunk job reads NativeStream and generates RenderGlyphs.
   - Depends on FontLoadSystem

   AnimateGlyphsGroup
   - User-defined systems which query RenderGlyphs and AnimatedRenderGlyphs
   - Depends on GenerateGlyphsSystem

   UpdateGlyphRenderersSystem
   - Query: RenderGlyphs or PreviousRenderGlyphs or ResidentRange
   - First, parallel job to apply new PreviousRenderGlyphs from last update
   - Second, parallel chunk job that change filters RenderGlyphs if present
   - If PreviousRenderGlyphs present, compare with RenderGlyphs or AnimatedRenderGlyphs to detect dirtiness, then update PreviousRenderGlyphs
   - If PreviousRenderGlyphs not present, reset GpuState
   - Compute new glyph RenderBounds and calculate required submesh
   - If glyph count changed and was GPU-resident, reset GpuState and add to resident deallocation list and clear ResidentRange
   - Collect glyph ref count deltas and add them to per-thread collection
   - Third, single-threaded job to process deallocation list in resident allocation table
   - Fourth, single-threaded job to combine and process ref count changes and possibly free glyph atlas space
   - Jobs three and four only dependent on job two
   - Depends on AnimateGlyphsGroup

   DispatchGlyphsSystem
   - Query: RenderGlyphs (dirty)
   - Todo: Round-robin stuff

 */

