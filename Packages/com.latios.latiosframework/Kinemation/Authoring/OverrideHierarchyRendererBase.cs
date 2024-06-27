using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Latios.Kinemation.Authoring
{
    /// <summary>
    /// Inherit this class and attach to a GameObject to disable normal Mesh Renderer or Skinned Mesh Renderer baking
    /// for possibly all descendants in the hierarchy. Each descendant's baker will query the ShouldOverride() method.
    /// </summary>
    public abstract class OverrideHierarchyRendererBase : MonoBehaviour
    {
        /// <summary>
        /// Return true if default baking should be disabled for this renderer. False otherwise.
        /// </summary>
        public abstract bool ShouldOverride(IBaker baker, Renderer renderer);
    }

    public static partial class RenderingBakingTools
    {
        static List<OverrideHierarchyRendererBase> s_overrideCache = new List<OverrideHierarchyRendererBase>();

        public static bool IsOverridden(IBaker baker, Renderer authoring)
        {
            if (baker.GetComponent<OverrideMeshRendererBase>() != null)
                return true;

            s_overrideCache.Clear();
            baker.GetComponentsInParent(s_overrideCache);
            foreach (var o in s_overrideCache)
            {
                if (o.ShouldOverride(baker, authoring))
                    return true;
            }

            return false;
        }
    }
}

