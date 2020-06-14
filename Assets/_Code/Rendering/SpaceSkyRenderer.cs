using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Lsss.Graphics
{
    public class SpaceSkyRenderer : SkyRenderer
    {
        public static readonly int _starCellCounts     = Shader.PropertyToID("_starCellCounts");
        public static readonly int _starRadii          = Shader.PropertyToID("_starRadii");
        public static readonly int _starBrightnesses   = Shader.PropertyToID("_starBrightnesses");
        public static readonly int _starFieldDensities = Shader.PropertyToID("_starFieldDensities");

        public static readonly int _nebulaCellCount  = Shader.PropertyToID("_nebulaCellCount");
        public static readonly int _nebulaDensity    = Shader.PropertyToID("_nebulaDensity");
        public static readonly int _nebulaBrightness = Shader.PropertyToID("nebulaBrightness");

        public static readonly int _PixelCoordToViewDirWS = Shader.PropertyToID("_PixelCoordToViewDirWS");

        private Material              spaceSkyMaterial;
        private MaterialPropertyBlock spaceSkyMaterialProperties = new MaterialPropertyBlock();

        private static int cubemapPass = 0;
        private static int screenPass  = 1;

        public override void Build()
        {
            spaceSkyMaterial = CoreUtils.CreateEngineMaterial("Hidden/SpaceSky");
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(spaceSkyMaterial);
        }

        protected override bool Update(BuiltinSkyParameters builtinParams)
        {
            return false;
        }

        public override void RenderSky(BuiltinSkyParameters builtinParams, bool renderForCubemap, bool renderSunDisk)
        {
            var spaceSkySettings = builtinParams.skySettings as SpaceSkySettings;

            int passId = renderForCubemap ? cubemapPass : screenPass;

            //My properties
            spaceSkyMaterialProperties.SetVector(_starCellCounts,     spaceSkySettings.starCellCounts.value);
            spaceSkyMaterialProperties.SetVector(_starRadii,          spaceSkySettings.starRadii.value);
            spaceSkyMaterialProperties.SetVector(_starBrightnesses,   spaceSkySettings.starBrightnesses.value);
            spaceSkyMaterialProperties.SetVector(_starFieldDensities, spaceSkySettings.starFieldDensities.value);

            spaceSkyMaterialProperties.SetFloat(_nebulaCellCount,  spaceSkySettings.nebulaCellCount.value);
            spaceSkyMaterialProperties.SetFloat(_nebulaDensity,    spaceSkySettings.nebulaDensity.value);
            spaceSkyMaterialProperties.SetFloat(_nebulaBrightness, spaceSkySettings.nebulaBrightness.value);

            spaceSkyMaterialProperties.SetMatrix(_PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);

            CoreUtils.DrawFullScreen(builtinParams.commandBuffer, spaceSkyMaterial, spaceSkyMaterialProperties, passId);
        }
    }
}

