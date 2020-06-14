//Implemented:
//4 starfield layers
//transmitting space dust with unrealistic distributions
//nebulas with unrealistic appearances and distributions

//Todo:
//Large stars with rayleigh scattering
//galaxies
//better space dust distributions
//layered nebulas
//other space objects
//More exposed parameters

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Sky/SkyUtils.hlsl"
#include "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl"

#include "FibonacciSphereCellNoise.hlsl"
#include "PerlinNoise3D.hlsl"
#include "StarField.hlsl"
#include "Nebula.hlsl"

float4 _starCellCounts;
float4 _starRadii;
float4 _starBrightnesses;
float4 _starFieldDensities;

float _nebulaCellCount;
float _nebulaDensity;
float _nebulaBrightness;

struct SpaceSky_Attributes
{
	uint vertexId : SV_VertexID;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SpaceSky_Varyings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

SpaceSky_Varyings Vert_SpaceSky(SpaceSky_Attributes i)
{
    SpaceSky_Varyings o;
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS = GetFullScreenTriangleVertexPosition(i.vertexId, UNITY_RAW_FAR_CLIP_VALUE);
    return o;
}

float4 DrawSpaceSky(SpaceSky_Varyings i, float exposure)
{
    float3 viewDirWS = GetSkyViewDirWS(i.positionCS.xy);
    
    float xOffset = abs(ddx(i.positionCS.x));
    float yOffset = abs(ddy(i.positionCS.y));
    float3 viewDirWSAdjacent[8];
    viewDirWSAdjacent[0] = GetSkyViewDirWS(i.positionCS.xy + float2(xOffset, 0));
    viewDirWSAdjacent[1] = GetSkyViewDirWS(i.positionCS.xy + float2(-xOffset, 0));
    viewDirWSAdjacent[2] = GetSkyViewDirWS(i.positionCS.xy + float2(0, yOffset));
    viewDirWSAdjacent[3] = GetSkyViewDirWS(i.positionCS.xy + float2(0, -yOffset));
    viewDirWSAdjacent[4] = GetSkyViewDirWS(i.positionCS.xy + float2(xOffset, yOffset));
    viewDirWSAdjacent[5] = GetSkyViewDirWS(i.positionCS.xy + float2(xOffset, -yOffset));
    viewDirWSAdjacent[6] = GetSkyViewDirWS(i.positionCS.xy + float2(-xOffset, yOffset));
    viewDirWSAdjacent[7] = GetSkyViewDirWS(i.positionCS.xy + float2(-xOffset, -yOffset));

    float3 starfields = Draw4StarFields(viewDirWS, _starCellCounts, _starRadii, _starBrightnesses, _starFieldDensities, viewDirWSAdjacent);
    float3 nebulas = DrawAllNebulas(viewDirWS, _nebulaCellCount, _nebulaDensity, _nebulaBrightness);
    return float4(starfields + nebulas, 1);
}

float4 Frag_SpaceSky_Cubemap(SpaceSky_Varyings i) : SV_Target
{
    return DrawSpaceSky(i, 1.0);
}

float4 Frag_SpaceSky_Screen(SpaceSky_Varyings i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return DrawSpaceSky(i, GetCurrentExposureMultiplier());
}