#ifndef INCLUDE_DECAL_SHADER_GRAPH_HELPERS
#define INCLUDE_DECAL_SHADER_GRAPH_HELPERS

// This file contains shader graph utilities to transform a mesh decal into a projected decal
// for both URP and HDRP for DOTS shaders.
//
// There are two features that are only supported with projected decals and not mesh decals.
// The first is DBuffer layers. This is stupid. There's absolutely no reason this can't work.
// In URP, this can be trivially fixed in a mesh decal shader with the following line:
// #define _DecalLayerMaskFromDecal asuint(unity_RenderingLayer.x)
// In HDRP, there is no good workaround, as it is tied to the pass. But it is easy to reimplement.
//
// The second feature is angle fade. This one is slightly less stupid, because it requires
// fade limits be passed in, and those limits require C# precomputation. However, Unity
// could still make a separate MonoBehaviour for it, as we do (and then bake).
// Once again, in URP, this is fixable by using a similar definition of getNormalToWorld()
// below, except you replace the third column (column index 2) with the negative of the
// interpolated vertex normal from the mesh decal.
//
// In URP, we only need a small amount of hackery to make the transformation, and then can
// rely upon the built-in logic and optimizations.
// In HDRP, we have to reimplement the logic ourselves, and then figure out how to hook it.
// Unfortunately, hooking the angle fade may not even be possible, because DecalPass.template
// assigns it to 1.0 in the same function it uses it to augment the results. This can be
// experimentally enabled by uncommenting the following:
//#define HDRP_DECAL_EXPERIMENTAL

// Begin shared helper functions
void SetupProjectedDecalVertex_float(in float3 positionObjectSpace, in float3 normalObjectSpace, in float nearClipPlane, out float3 outPositionObjectSpace, out float3 outNormalObjectSpace, out float3 outTangentObjectSpace)
{
    float3 posVS = mul(UNITY_MATRIX_V, mul(UNITY_MATRIX_M, float4(positionObjectSpace, 1.0))).xyz;
    float3 normalVS = mul(UNITY_MATRIX_V, mul(UNITY_MATRIX_M, float4(normalObjectSpace, 0.0))).xyz;
    float distanceBehindPlane = posVS.z - nearClipPlane;
    if (distanceBehindPlane < 0.00001 && normalVS.z < -0.00001)
    {
        distanceBehindPlane -= 0.00001;
        float distanceAgainstNormal = abs(distanceBehindPlane / normalVS.z);
        posVS -= distanceAgainstNormal * normalVS;
        outPositionObjectSpace = mul(UNITY_MATRIX_I_M, mul(UNITY_MATRIX_I_V, float4(posVS, 1.0))).xyz;
    }
    else
    {
        outPositionObjectSpace = positionObjectSpace;
    }
    
    outNormalObjectSpace = float3(0.0, 0.0, -1.0);
    outTangentObjectSpace = float3(1.0, 0.0, 0.0);
}

void ProjectDecalOrthographic_float(in float2 screenUV, in float sceneDepthRaw, out float2 projectionUVs, out float distanceFactor)
{
    float3 positionWS = ComputeWorldSpacePosition(screenUV, sceneDepthRaw, UNITY_MATRIX_I_VP);
    float3 positionLS = mul(UNITY_MATRIX_I_M, float4(positionWS, 1.0)).xyz;
    projectionUVs = positionLS.xy + 0.5;
    distanceFactor = positionLS.z;
    
    // In HDRP, there's a note that clipping on Metal platforms causes weird behaviors with
    // derivative functions. There's not really a good way to deal with this from within this
    // file though.
    float clipValue = 1.0;
    positionLS.z -= 0.5;
    float3 absolutes = abs(positionLS);
    float biggest = max(absolutes.x, max(absolutes.y, absolutes.z));
    if (biggest < 0.0 || biggest > 0.5)
        clipValue = -1.0;
    
#if defined(IS_HD_RENDER_PIPELINE) && !defined(HDRP_DECAL_EXPERIMENTAL)
    float depth = sceneDepthRaw;
#if (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_MESH) && UNITY_REVERSED_Z
    // For the sky adjust the depth so that the following LOD calculation (GetSurfaceData() in DecalData.hlsl) of adjacent
    // non-sky pixels using depth derivatives results in LOD0 sampling
    depth = IsSky(depth) ? UNITY_NEAR_CLIP_VALUE : depth;
#endif
    PositionInputs posInput = GetPositionInput(screenUV * _ScreenSize.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

    // Decal layer mask accepted by the receiving material
    DecalPrepassData material;
    ZERO_INITIALIZE(DecalPrepassData, material);
    if (_EnableDecalLayers)
    {
        DecodeFromDecalPrepass(posInput.positionSS, material);
        
        // Clip the decal if it does not pass the decal layer mask of the receiving material.
        // Decal layer of the decal
        uint decalLayerMask = uint(UNITY_ACCESS_INSTANCED_PROP(Decal, _DecalLayerMaskFromDecal).x);

        if ((decalLayerMask & material.renderingLayerMask) == 0)
        {
            clipValue = -1.0;
        }
    }
#endif
    
    clip(clipValue);
}

void ProjectDecalPerspective_float(in float screenUV, in float sceneDepthRaw, out float2 projectionUVs, out float distanceFactor)
{
    float3 positionWS = ComputeWorldSpacePosition(screenUV, sceneDepthRaw, UNITY_MATRIX_I_VP);
    float3 positionLS = mul(UNITY_MATRIX_I_M, float4(positionWS, 1.0)).xyz;
    distanceFactor = positionLS.z;
    
    // In HDRP, there's a note that clipping on Metal platforms causes weird behaviors with
    // derivative functions. There's not really a good way to deal with this from within this
    // file though.
    float clipValue = 1.0;
    positionLS.z -= 0.5;
    positionLS.xy /= max(distanceFactor, 0.00001);
    projectionUVs = positionLS.xy + 0.5;
    float3 absolutes = abs(positionLS);
    float biggest = max(absolutes.x, max(absolutes.y, absolutes.z));
    if (biggest < 0.0 || biggest > 0.5)
        clipValue = -1.0;
    
#if defined(IS_HD_RENDER_PIPELINE) && !defined(HDRP_DECAL_EXPERIMENTAL)
    float depth = sceneDepthRaw;
#if (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_MESH) && UNITY_REVERSED_Z
    // For the sky adjust the depth so that the following LOD calculation (GetSurfaceData() in DecalData.hlsl) of adjacent
    // non-sky pixels using depth derivatives results in LOD0 sampling
    depth = IsSky(depth) ? UNITY_NEAR_CLIP_VALUE : depth;
#endif
    PositionInputs posInput = GetPositionInput(screenUV * _ScreenSize.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

    // Decal layer mask accepted by the receiving material
    DecalPrepassData material;
    ZERO_INITIALIZE(DecalPrepassData, material);
    if (_EnableDecalLayers)
    {
        DecodeFromDecalPrepass(posInput.positionSS, material);
        
        // Clip the decal if it does not pass the decal layer mask of the receiving material.
        // Decal layer of the decal
        uint decalLayerMask = asuint(unity_RenderingLayer.x);

        if ((decalLayerMask & material.renderingLayerMask) == 0)
        {
            clipValue = -1.0;
        }
    }
#endif
    
    clip(clipValue);
}
// End shared helper functions

// Begin URP
#ifdef UNIVERSAL_SHADERPASS_INCLUDED
#define IS_UNIVERSAL_RENDER_PIPELINE
#else
#ifdef SHADERPASS_CS_HLSL // Todo: Someone could have copied this from HDRP
#define IS_HD_RENDER_PIPELINE
#endif
#endif

// URP is capable of running the angle fade algorithm on mesh decals if the define is present.
// We just have to teach it how to read our data and also force the define to be present, as
// it won't add it for mesh decal passes regardless of the ShaderGraph checkbox.
#ifdef IS_UNIVERSAL_RENDER_PIPELINE
//#ifdef DECAL_ANGLE_FADE
#ifndef DECAL_ANGLE_FADE
#define DECAL_ANGLE_FADE
#endif
half4x4 getNormalToWorld()
{
    half4x4 result = UNITY_MATRIX_M;
#ifdef DOTS_INSTANCING_ON
    half2 angles = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float2, _DecalAngleFade);
#else
    half2 angles = half2(0, 0);
#endif
    // If angles were in radians start and end, the following would show what the conversion
    // process would look like. However, we try to do this on the C# side in the DecalAngleFade
    // constructor.
    //angles = clamp(angles.x, 0, PI) / PI;
    //if (angles.x > 0)
    //{
    //    half angleStart = angles.x;
    //    half angleEnd = angles.y;
    //    half range = max(0.0001, angleEnd - angleStart);
    //    angles = half2(1.0 - (0.25f - angleStart) / range, -0.25 / range);
    //}
    result[1][3] = angles.x;
    result[2][3] = angles.y;
    return result;
}
#define _NormalToWorld getNormalToWorld()
//#endif
#define _DecalLayerMaskFromDecal asuint(unity_RenderingLayer.x)
#endif
// End URP

// Begin HDRP. Define HDRP_DECAL_EXPERIMENTAL to enable angle fade hackery.
#ifdef IS_HDRP_RENDER_PIPELINE
#ifdef HDRP_DECAL_EXPERIMENTAL
#ifdef LOD_FADE_CROSSFADE
void LODDitheringTransitionWrapped(uint2 fadeMaskSeed, float ditherFactor)
{
    LODDitheringTransition(fadeMaskSeed, ditherFactor);
}

#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Decal/DecalPrepassBuffer.hlsl"

#define LODDitheringTransition float projectedFadeAngle = ProjectedDecalHdrpPatch(posInput.positionSS); \
LODDitheringTransitionWrapped

#else
#define LODDitheringTransition(a, b) float projectedFadeAngle = ProjectedDecalHdrpPatch(posInput.positionSS);
#define LOD_FADE_CROSSFADE
#endif

#define fadeFactor fadeFactorHacked; \
fadeFactorHacked = projectedFadeAngle; \
float dummy

float ProjectedDecalHdrpPatch(float3 positionSS)
{
    float clipValue = 1.0;
    float angleFadeFactor = 1.0;

    float depth = LoadCameraDepth(positionSS.xy);
#if (SHADERPASS == SHADERPASS_FORWARD_EMISSIVE_MESH) && UNITY_REVERSED_Z
    // For the sky adjust the depth so that the following LOD calculation (GetSurfaceData() in DecalData.hlsl) of adjacent
    // non-sky pixels using depth derivatives results in LOD0 sampling
    depth = IsSky(depth) ? UNITY_NEAR_CLIP_VALUE : depth;
#endif
    PositionInputs posInput = GetPositionInput(positionSS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

    // Decal layer mask accepted by the receiving material
    DecalPrepassData material;
    ZERO_INITIALIZE(DecalPrepassData, material);
    if (_EnableDecalLayers)
    {
        DecodeFromDecalPrepass(posInput.positionSS, material);

        // Evaluate angle fade
        float4x4 normalToWorld = UNITY_MATRIX_M;
#ifdef DOTS_INSTANCING_ON
        float2 angleFade = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float2, _DecalAngleFade);
#else
        float2 angleFade = float2(0, 0);
#endif
        // If angles were in radians start and end, the following would show what the conversion
        // process would look like. However, we try to do this on the C# side in the DecalAngleFade
        // constructor.
        //angleFade = clamp(angleFade.x, 0, PI) / PI;
        //if (angles.x > 0)
        //{
        //    half angleStart = angleFade.x;
        //    half angleEnd = angleFade.y;
        //    half range = max(0.0001, angleEnd - angleStart);
        //    angleFade.x = 0.222222222 / range;
        //    angleFade.y = (angleEnd - 0.5) / range;
        //}
        if (angleFade.x > 0.0f) // if angle fade is enabled
        {
            float3 decalNormal = float3(normalToWorld[0].z, normalToWorld[1].z, normalToWorld[2].z);
            angleFadeFactor = DecodeAngleFade(dot(material.geomNormalWS, decalNormal), angleFade);
            if (angleFadeFactor < 0.00001)
            {
                clipValue -= 2.0;
            }
        }
        
        // Clip the decal if it does not pass the decal layer mask of the receiving material.
        // Decal layer of the decal
        uint decalLayerMask = asuint(unity_RenderingLayer.x);

        if ((decalLayerMask & material.renderingLayerMask) == 0)
        {
            clipValue -= 2.0;
            angleFadeFactor = 0;
        }
    }

// According to HDRP code, Metal doesn't like early clips if you need derivatives.
#ifndef SHADER_API_METAL
    clip(clipValue);
#endif
    return angleFadeFactor;
}
#endif
#endif

void test_dots_float(in float2 uv, out float2 uvo)
{
    uvo = uv;
}

#endif