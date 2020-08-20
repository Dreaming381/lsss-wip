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
        public static readonly int _SpaceSkyCubemap       = Shader.PropertyToID("_SpaceSkyCubemap");

        private Material              m_spaceSkyMaterial;
        private MaterialPropertyBlock m_spaceSkyMaterialProperties = new MaterialPropertyBlock();
        private RenderTexture         m_spaceSkyCubemapRenderTexture;

        int  m_renderForCubemapCount   = 0;
        int  m_prevHashCode            = 0;
        bool m_prevHashCodeInitialized = false;

        private static int kCubemapPass       = 0;
        private static int kScreenPass        = 1;
        private static int kCachedPass        = 2;
        private static int kCubemapCachedPass = 3;

        public override void Build()
        {
            m_spaceSkyMaterial             = CoreUtils.CreateEngineMaterial("Hidden/SpaceSky");
            m_spaceSkyCubemapRenderTexture = HDRenderUtilities.CreateReflectionProbeRenderTarget(4096);
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(m_spaceSkyMaterial);
            CoreUtils.Destroy(m_spaceSkyCubemapRenderTexture);
        }

        protected override bool Update(BuiltinSkyParameters builtinParams)
        {
            return false;
        }

        public override void RenderSky(BuiltinSkyParameters builtinParams, bool renderForCubemap, bool renderSunDisk)
        {
            var spaceSkySettings = builtinParams.skySettings as SpaceSkySettings;

            int passId = renderForCubemap ? kCubemapPass : kCachedPass;
            if (!m_prevHashCodeInitialized || m_prevHashCode != spaceSkySettings.GetHashCode())
            {
                passId = renderForCubemap ? kCubemapPass : kScreenPass;
            }

            m_spaceSkyMaterialProperties.Clear();

            //My properties
            m_spaceSkyMaterialProperties.SetVector(_starCellCounts,     spaceSkySettings.starCellCounts.value);
            m_spaceSkyMaterialProperties.SetVector(_starRadii,          spaceSkySettings.starRadii.value);
            m_spaceSkyMaterialProperties.SetVector(_starBrightnesses,   spaceSkySettings.starBrightnesses.value);
            m_spaceSkyMaterialProperties.SetVector(_starFieldDensities, spaceSkySettings.starFieldDensities.value);

            m_spaceSkyMaterialProperties.SetFloat(_nebulaCellCount,  spaceSkySettings.nebulaCellCount.value);
            m_spaceSkyMaterialProperties.SetFloat(_nebulaDensity,    spaceSkySettings.nebulaDensity.value);
            m_spaceSkyMaterialProperties.SetFloat(_nebulaBrightness, spaceSkySettings.nebulaBrightness.value);

            m_spaceSkyMaterialProperties.SetMatrix(_PixelCoordToViewDirWS, builtinParams.pixelCoordToViewDirMatrix);

            if (passId == kCachedPass)
            {
                m_spaceSkyMaterialProperties.SetTexture(_SpaceSkyCubemap, m_spaceSkyCubemapRenderTexture);
            }

            CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_spaceSkyMaterial, m_spaceSkyMaterialProperties, passId);

            if (renderForCubemap)
            {
                var lookAt      = Matrix4x4.LookAt(Vector3.zero, CoreUtils.lookAtList[m_renderForCubemapCount], CoreUtils.upVectorList[m_renderForCubemapCount]);
                var worldToView = lookAt * Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f));  // Need to scale -1.0 on Z to match what is being done in the camera.wolrdToCameraMatrix API. ...

                var cubemapResolution  = 4096f;
                var cubemapScreenSize  = new Vector4(cubemapResolution, cubemapResolution, 1.0f / cubemapResolution, 1.0f / cubemapResolution);
                var pixelViewDirMatrix = ComputePixelCoordToWorldSpaceViewDirectionMatrix(0.5f * Mathf.PI, Vector2.zero, cubemapScreenSize, worldToView, true);
                m_spaceSkyMaterialProperties.SetMatrix(_PixelCoordToViewDirWS, pixelViewDirMatrix);
                CoreUtils.SetRenderTarget(builtinParams.commandBuffer, m_spaceSkyCubemapRenderTexture, ClearFlag.None, 0, (CubemapFace)m_renderForCubemapCount);
                CoreUtils.DrawFullScreen(builtinParams.commandBuffer, m_spaceSkyMaterial, m_spaceSkyMaterialProperties, kCubemapCachedPass);

                m_renderForCubemapCount++;
                if (m_renderForCubemapCount == 6)
                {
                    m_renderForCubemapCount   = 0;
                    m_prevHashCodeInitialized = true;
                    m_prevHashCode            = spaceSkySettings.GetHashCode();
                    builtinParams.commandBuffer.GenerateMips(m_spaceSkyCubemapRenderTexture);
                }
            }
        }

        //Borrowed from HDRP SkyManager.cs
        internal static Matrix4x4 ComputePixelCoordToWorldSpaceViewDirectionMatrix(float verticalFoV,
                                                                                   Vector2 lensShift,
                                                                                   Vector4 screenSize,
                                                                                   Matrix4x4 worldToViewMatrix,
                                                                                   bool renderToCubemap,
                                                                                   float aspectRatio = -1)
        {
            aspectRatio = aspectRatio < 0 ? screenSize.x * screenSize.w : aspectRatio;

            // Compose the view space version first.
            // V = -(X, Y, Z), s.t. Z = 1,
            // X = (2x / resX - 1) * tan(vFoV / 2) * ar = x * [(2 / resX) * tan(vFoV / 2) * ar] + [-tan(vFoV / 2) * ar] = x * [-m00] + [-m20]
            // Y = (2y / resY - 1) * tan(vFoV / 2)      = y * [(2 / resY) * tan(vFoV / 2)]      + [-tan(vFoV / 2)]      = y * [-m11] + [-m21]

            float tanHalfVertFoV = Mathf.Tan(0.5f * verticalFoV);

            // Compose the matrix.
            float m21 = (1.0f - 2.0f * lensShift.y) * tanHalfVertFoV;
            float m11 = -2.0f * screenSize.w * tanHalfVertFoV;

            float m20 = (1.0f - 2.0f * lensShift.x) * tanHalfVertFoV * aspectRatio;
            float m00 = -2.0f * screenSize.z * tanHalfVertFoV * aspectRatio;

            if (renderToCubemap)
            {
                // Flip Y.
                m11 = -m11;
                m21 = -m21;
            }

            var viewSpaceRasterTransform = new Matrix4x4(new Vector4(m00, 0.0f, 0.0f, 0.0f),
                                                         new Vector4(0.0f, m11, 0.0f, 0.0f),
                                                         new Vector4(m20, m21, -1.0f, 0.0f),
                                                         new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

            // Remove the translation component.
            var homogeneousZero = new Vector4(0, 0, 0, 1);
            worldToViewMatrix.SetColumn(3, homogeneousZero);

            // Flip the Z to make the coordinate system left-handed.
            worldToViewMatrix.SetRow(2, -worldToViewMatrix.GetRow(2));

            // Transpose for HLSL.
            return Matrix4x4.Transpose(worldToViewMatrix.transpose * viewSpaceRasterTransform);
        }
    }
}

