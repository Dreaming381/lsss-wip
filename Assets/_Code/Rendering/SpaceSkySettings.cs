using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Lsss.Graphics
{
    [VolumeComponentMenu("Sky/Space Sky")]
    //[SkyUniqueID(381384386)]
    [SkyUniqueID((int)SkyType.Procedural)]  //Stupid bug in HDRP
    public class SpaceSkySettings : SkySettings
    {
        public Vector4Parameter starCellCounts = new Vector4Parameter(new Vector4(2000f, 1000f, 500f, 10000f));

        public Vector4Parameter starRadii = new Vector4Parameter(new Vector4(0.8f, 0.9f, 0.8f, 0.9f));

        public Vector4Parameter starBrightnesses = new Vector4Parameter(new Vector4(2f, 3f, 4f, 0.5f));

        public Vector4Parameter starFieldDensities = new Vector4Parameter(new Vector4(4.8f, 3f, 0.4f, 25f));

        public FloatParameter nebulaCellCount = new FloatParameter(40f);

        public FloatParameter nebulaDensity = new FloatParameter(4f);

        public FloatParameter nebulaBrightness = new FloatParameter(100f);

        [HideInInspector]
        public Material spaceSkyShaderHolder;

        public override Type GetSkyRendererType()
        {
            return typeof(SpaceSkyRenderer);
        }

        //Sky shouldn't change at runtime, so this solves GC issues.
        int  m_hash  = 0;
        bool m_dirty = true;
        public override int GetHashCode()
        {
            if (!m_dirty)
                return m_hash;

            var hash = base.GetHashCode();

            unchecked
            {
                hash = hash * 23 + starCellCounts.GetHashCode();
                hash = hash * 23 + starRadii.GetHashCode();
                hash = hash * 23 + starBrightnesses.GetHashCode();
                hash = hash * 23 + starFieldDensities.GetHashCode();
                hash = hash * 23 + nebulaCellCount.GetHashCode();
                hash = hash * 23 + nebulaDensity.GetHashCode();
                hash = hash * 23 + nebulaBrightness.GetHashCode();
            }
            m_hash  = hash;
            m_dirty = false;
            return hash;
        }

        private void OnValidate()
        {
            if (spaceSkyShaderHolder == null)
            {
                spaceSkyShaderHolder = CoreUtils.CreateEngineMaterial("Hidden/SpaceSky");
            }
            m_dirty = true;
        }
    }
}

