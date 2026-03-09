#ifndef TMDGLOBALSAPI
#define TMDGLOBALSAPI

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

uniform ByteAddressBuffer _tmdGlyphs;
TEXTURE2D_ARRAY(_tmdSdf8);
SAMPLER(sampler_tmdSdf8);
TEXTURE2D_ARRAY(_tmdSdf16);
SAMPLER(sampler_tmdSdf16);
TEXTURE2D_ARRAY(_tmdBitmap);
SAMPLER(sampler_tmdBitmap);

// Helpers
float4 UnpackHalfColor(uint2 packedColor)
{
    uint4 expanded = packedColor.xxyy;
    expanded.yw = expanded.yw >> 16u;
    expanded = expanded & 0xffffu;
    return f16tof32(expanded);
}


// Base APIs
void GetGlyph(uint glyphIndex, uint glyphStartIndex, uint glyphCount,
    out float2 blPosition,
    out float2 brPosition,
    out float2 tlPosition,
    out float2 trPosition,

    out float2 blUVB,
    out float2 brUVB,
    out float2 tlUVB,
    out float2 trUVB,

    out float4 blColor,
    out float4 brColor,
    out float4 tlColor,
    out float4 trColor,

    out float2 blUVA,
    out float2 trUVA,

    out float arrayIndex,
    out uint glyphEntryId,
    out float scale,
    out uint reserved)
{
    if (glyphIndex >= glyphCount)
    {
        blPosition = asfloat(~0u);
        brPosition = blPosition;
        tlPosition = blPosition;
        trPosition = blPosition;
        blUVB = blPosition;
        brUVB = blPosition;
        tlUVB = blPosition;
        trUVB = blPosition;
        blColor = 0;
        brColor = blColor;
        tlColor = blColor;
        trColor = blColor;
        blUVA = blPosition;
        trUVA = blPosition;
        arrayIndex = 0;
        glyphEntryId = 0;
        scale = 0;
        reserved = 0u;        
    }
    else
    {
        uint baseAddress = (glyphStartIndex + glyphIndex) * 128;
        uint4 load0_15 = _tmdGlyphs.Load4(baseAddress);
        blPosition = asfloat(load0_15.xy);
        brPosition = asfloat(load0_15.zw);
        uint4 load16_31 = _tmdGlyphs.Load4(baseAddress + 16);
        tlPosition = asfloat(load16_31.xy);
        trPosition = asfloat(load16_31.zw);

        uint4 load32_47 = _tmdGlyphs.Load4(baseAddress + 32);
        blUVB = asfloat(load32_47.xy);
        brUVB = asfloat(load32_47.zw);
        uint4 load48_63 = _tmdGlyphs.Load4(baseAddress + 48);
        tlUVB = asfloat(load48_63.xy);
        trUVB = asfloat(load48_63.zw);

        uint4 load64_79 = _tmdGlyphs.Load4(baseAddress + 64); //load half4 blColor and half4 brColor
        blColor = UnpackHalfColor(load64_79.xy); //convert blColor from half4 to float4
        brColor = UnpackHalfColor(load64_79.zw); //convert brColor from half4 to float4
        uint4 load80_95 = _tmdGlyphs.Load4(baseAddress + 80);
        tlColor = UnpackHalfColor(load80_95.xy);
        trColor = UnpackHalfColor(load80_95.zw);

        uint4 load96_111 = _tmdGlyphs.Load4(baseAddress + 96);
        blUVA = asfloat(load96_111.xy);
        trUVA = asfloat(load96_111.zw);

        uint4 load112_127 = _tmdGlyphs.Load4(baseAddress + 112);
        arrayIndex = asfloat(load112_127.x);
        glyphEntryId = load112_127.y;
        scale = asfloat(load112_127.z);
        reserved = load112_127.w;
    }
    return;
}

// Corner order:  bl = 0, tl = 1, tr = 2, br = 3
void GetGlyphCorner(uint glyphIndex, uint cornerIndex, uint glyphStartIndex, uint glyphCount, out float2 position, out float3 uvA, out float2 uvB, out float4 color, out float scale, out uint glyphEntryID)
{
    float2 blPosition;
    float2 brPosition;
    float2 tlPosition;
    float2 trPosition;
    float2 blUVB;
    float2 brUVB;
    float2 tlUVB;
    float2 trUVB;
    float4 blColor;
    float4 brColor;
    float4 tlColor;
    float4 trColor;
    float2 blUVA;
    float2 trUVA;
    float arrayIndex;
    uint reserved;
    GetGlyph(glyphIndex, glyphStartIndex, glyphCount, blPosition, brPosition, tlPosition, trPosition, blUVB, brUVB, tlUVB, trUVB, blColor, brColor, tlColor, trColor, blUVA, trUVA, arrayIndex, glyphEntryID, scale, reserved);
    if (cornerIndex == 0)
    {
        // bottom left
        position = blPosition;
        uvA = float3(blUVA, arrayIndex);
        //uvB = blUVB;
        uvB = float2(0, 0);
        color = blColor;
    }
    else if (cornerIndex == 1)
    {
        // top left
        position = tlPosition;
        uvA = float3(blUVA.x, trUVA.y, arrayIndex);
        //uvB = tlUVB;
        uvB = float2(0, 1);
        color = tlColor;
    }
    else if (cornerIndex == 2)
    {
        // top right
        position = trPosition;
        uvA = float3(trUVA, arrayIndex);
        //uvB = trUVB;
        uvB = float2(1, 1);
        color = trColor;
    }
    else
    {
        // bottom right
        position = brPosition;
        uvA = float3(trUVA.x, blUVA.y, arrayIndex);
        //uvB = brUVB;
        uvB = float2(1, 0);
        color = brColor;
    }
}

void ExtractGlyphFlagsFromEntryID(uint glyphEntryID, out bool isSdf16, out bool isBitmap)
{
    uint format = glyphEntryID >> 30u;
    isSdf16 = format == 1;
    isBitmap = format == 2;
}

void GetGlyphIndexAndCornerFromQuadVertexID(uint vertexID, out uint glyphIndex, out uint cornerIndex)
{
    glyphIndex = vertexID >> 2u;
    cornerIndex = vertexID & 3u;
}
void GetGlyphFromBuffer_float(float2 textShaderIndex, float vertexID, out float3 position, out float3 normal, out float3 tangent, out float4 vertexColor, out float4 uvAandB, out float4 atlasIndexScaleIsSdf16IsBitmap)
{
    uint glyphIndex;
    uint cornerIndex;
    GetGlyphIndexAndCornerFromQuadVertexID(vertexID, glyphIndex, cornerIndex);
    uint glyphStartIndex = asuint(textShaderIndex.x);
    uint glyphCount = asuint(textShaderIndex.y);
    float2 position2D;
    float3 uvA;
    float2 uvB;
    float4 color;
    float scale;
    uint glyphEntryID;
    GetGlyphCorner(glyphIndex, cornerIndex, glyphStartIndex, glyphCount, position2D, uvA, uvB, color, scale, glyphEntryID);
    bool isSdf16;
    bool isBitmap;
    ExtractGlyphFlagsFromEntryID(glyphEntryID, isSdf16, isBitmap);
    position = float3(position2D, 0.0);
    normal = float3(0.0, 0.0, -1.0); //text face is pointing forward
    tangent = float3(1.0, 0.0, 0.0);
    vertexColor = color;
    uvAandB = float4(uvA.xy, uvB);
	atlasIndexScaleIsSdf16IsBitmap = float4(uvA.z, scale, isSdf16, isBitmap);
}


//API to sample Bitmap and SDF TEXTURE2D_ARRAY

// Todo: This causes Unity's shader compiler to break. Attempt to reenable this later.
//UnityTexture2DArray GetSdfTextureArray(bool is16Bit)
//{
//    if (is16Bit)
//    {
//        return UnityBuildTexture2DArrayStruct(_tmdSdf16);
//    }
//    else
//    {
//        return UnityBuildTexture2DArrayStruct(_tmdSdf8);
//    }
//}

void GetSurfaceNormal(
    UnityTexture2DArray sdf,
    float2 texelSize, 
    float2 uvA,
	float arrayIndex,
    float SDR,
    bool isFront,     
    bool innerBevel,
    float bevelAmount,
    float bevelWidth,
    float bevelRoundness,
    float bevelClamp,
    out float3 normal)
{
    float3 delta = float3(texelSize, 0.0);

	// Read "height field"
    float4 h = float4(
		SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - delta.xz, arrayIndex).r,
		SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy + delta.xz, arrayIndex).r,
		SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - delta.zy, arrayIndex).r,
		SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy + delta.zy, arrayIndex).r);
    

    //h += _BevelOffset;
    bevelWidth = max(.01, bevelWidth);

	// Track outline
    h -= .5;
    h /= bevelWidth;
    h = saturate(h + .5);

    if (innerBevel)
        h = 1 - abs(h * 2.0 - 1.0);
    h = lerp(h, sin(h * 3.141592 / 2.0), float4(bevelRoundness, bevelRoundness, bevelRoundness, bevelRoundness));
    h = min(h, 1.0 - float4(bevelClamp, bevelClamp, bevelClamp, bevelClamp));
    h *= bevelAmount * bevelWidth * SDR * -2.0;

    float3 va = normalize(float3(-1.0, 0.0, h.y - h.x));
    float3 vb = normalize(float3(0.0, 1.0, h.w - h.z));

	float3 f = isFront ? float3(1, 1, -1) : float3(1, 1, 1);
    normal = cross(va, vb) * f;
}

//sample SDF 5+4 times: face, outline1, outline2, outline3, underlay, and 4x for light normal
void Sample5Texture2DArrayLIT_float(
    float4 uvAandB,
    float4 atlasIndexScaleIsSdf16IsBitmap,
    bool isFront,
    bool innerBevel,
    float bevelAmount,
    float bevelWidth,
    float bevelRoundness,
    float bevelClamp,
    float2 underlayColorOffset,
    float2 outlineColor1Offset,
    float2 outlineColor2Offset,
    float2 outlineColor3Offset,
    out bool isBitmap,
    out float4 bitmapColor,
    out float2 texelSize,
	out float SDR,
    out float4 SD,      //x: face, y: outline1, z: outline2, w: outline3
    out float underlaySD,    
    out float3 normal,    
	out float2 uvA,
    out float2 uvB,
    out float scale)
{
	uint width, height, elements, numberOfLevels;
	uvA = uvAandB.xy;
	uvB = uvAandB.zw;
	float arrayIndex = atlasIndexScaleIsSdf16IsBitmap.x;
	scale = atlasIndexScaleIsSdf16IsBitmap.y;
	bool isSdf16 = atlasIndexScaleIsSdf16IsBitmap.z;
	isBitmap = atlasIndexScaleIsSdf16IsBitmap.w;
	SD = 0;
	SDR = 0;
    if (isBitmap)
	{
		_tmdBitmap.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
		texelSize = 1.0f / float2(width, height);
                 
        UnityTexture2DArray bitmap = UnityBuildTexture2DArrayStruct(_tmdBitmap);
        bitmapColor = SAMPLE_TEXTURE2D_ARRAY(bitmap, sampler_LinearClamp, uvA.xy, arrayIndex);
        normal = float3(0, 0, -1);
        underlaySD = 0;    
        return;
    }
    else
    {
        bitmapColor = float4(0,0,0,0);        
        if (isSdf16)
        {
			SDR = 32; // = spread * 2 Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
            _tmdSdf16.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
            texelSize = 1.0f / float2(width, height);
            float offSetScale = SDR * texelSize.x;            
            
            outlineColor1Offset *= offSetScale;
            outlineColor2Offset *= offSetScale;
            outlineColor3Offset *= offSetScale;
            underlayColorOffset *= offSetScale;
            
            UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf16);
            SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
            SD.y = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor1Offset, arrayIndex).r;             
            SD.z = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor2Offset, arrayIndex).r;            
            SD.w = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor3Offset, arrayIndex).r;
            underlaySD = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - underlayColorOffset, arrayIndex).r;
            
			GetSurfaceNormal(sdf, texelSize, uvA, arrayIndex, SDR, isFront, innerBevel, bevelAmount, bevelWidth, bevelRoundness, bevelClamp, normal);
		}
        else
        {
			SDR = 16; // = spread * 2 Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
            _tmdSdf8.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
            texelSize = 1.0f / float2(width, height);
            float offSetScale = SDR * texelSize.x;
            
            outlineColor1Offset *= offSetScale;
            outlineColor2Offset *= offSetScale;
            outlineColor3Offset *= offSetScale;
            underlayColorOffset *= offSetScale;
            
            UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf8);
            SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
            SD.y = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor1Offset, arrayIndex).r;
            SD.z = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor2Offset, arrayIndex).r;
            SD.w = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor3Offset, arrayIndex).r;
            underlaySD = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - underlayColorOffset, arrayIndex).r;
            
			GetSurfaceNormal(sdf, texelSize, uvA, arrayIndex, SDR, isFront, innerBevel, bevelAmount, bevelWidth, bevelRoundness, bevelClamp, normal);
		}
    }
}
//sample SDF 5 times: face, outline1, outline2, outline3, underlay
void Sample5Texture2DArrayUNLIT_float(
    float4 uvAandB,
    float4 atlasIndexScaleIsSdf16IsBitmap,    
    float2 underlayColorOffset,
    float2 outlineColor1Offset,
    float2 outlineColor2Offset,
    float2 outlineColor3Offset,
    out bool isBitmap,
    out float4 bitmapColor,
    out float2 texelSize,
	out float SDR,
    out float4 SD, //x: face, y: outline1, z: outline2, w: outline3
    out float underlaySD,
	out float2 uvA,
    out float2 uvB,
    out float scale)
{
	uint width, height, elements, numberOfLevels;
	uvA = uvAandB.xy;
	uvB = uvAandB.zw;
	float arrayIndex = atlasIndexScaleIsSdf16IsBitmap.x;
	scale = atlasIndexScaleIsSdf16IsBitmap.y;
	bool isSdf16 = atlasIndexScaleIsSdf16IsBitmap.z;
	isBitmap = atlasIndexScaleIsSdf16IsBitmap.w;
	SD = 0;
	SDR = 0;
    if (isBitmap)
	{
		_tmdBitmap.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
		texelSize = 1.0f / float2(width, height);
        
        UnityTexture2DArray bitmap = UnityBuildTexture2DArrayStruct(_tmdBitmap);
        bitmapColor = SAMPLE_TEXTURE2D_ARRAY(bitmap, sampler_LinearClamp, uvA.xy, arrayIndex);
        underlaySD = 0;
        return;
    }
    else
    {
        bitmapColor = float4(0, 0, 0, 0);        
        if (isSdf16)
        {
			SDR = 32; // = spread * 2 Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
            _tmdSdf16.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
            texelSize = 1.0f / float2(width, height);
            float offSetScale = SDR * texelSize.x;
            
            outlineColor1Offset *= offSetScale;
            outlineColor2Offset *= offSetScale;
            outlineColor3Offset *= offSetScale;
            underlayColorOffset *= offSetScale;
            
            UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf16);
            SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
            SD.y = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor1Offset, arrayIndex).r;
            SD.z = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor2Offset, arrayIndex).r;
            SD.w = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor3Offset, arrayIndex).r;
            underlaySD = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - underlayColorOffset, arrayIndex).r;
        }
        else
        {
			SDR = 16; // = spread * 2 Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
            _tmdSdf8.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
            texelSize = 1.0f / float2(width, height);
            float offSetScale = SDR * texelSize.x;
            
            outlineColor1Offset *= offSetScale;
            outlineColor2Offset *= offSetScale;
            outlineColor3Offset *= offSetScale;
            underlayColorOffset *= offSetScale;
            
            UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf8);
            SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
            SD.y = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor1Offset, arrayIndex).r;
            SD.z = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor2Offset, arrayIndex).r;
            SD.w = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor3Offset, arrayIndex).r;
            underlaySD = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - underlayColorOffset, arrayIndex).r;
        }
    }
}

//sample SDF 3+4 times: face, outline1, underlay, and 4x for light normal
void Sample3Texture2DArrayLIT_float(
    float4 uvAandB,
    float4 atlasIndexScaleIsSdf16IsBitmap,
    bool isFront,
    bool innerBevel,
    float bevelAmount,
    float bevelWidth,
    float bevelRoundness,
    float bevelClamp,
    float2 underlayColorOffset,
    float2 outlineColor1Offset,
    out bool isBitmap,
    out float4 bitmapColor,
    out float2 texelSize,
	out float SDR,
    out float2 SD, //x: face, y: outline1
    out float underlaySD,
    out float3 normal,
	out float2 uvA,
    out float2 uvB,
    out float scale)
{
	uint width, height, elements, numberOfLevels;
	uvA = uvAandB.xy;
	uvB = uvAandB.zw;
	float arrayIndex = atlasIndexScaleIsSdf16IsBitmap.x;
	scale = atlasIndexScaleIsSdf16IsBitmap.y;
	bool isSdf16 = atlasIndexScaleIsSdf16IsBitmap.z;
	isBitmap = atlasIndexScaleIsSdf16IsBitmap.w;
	SD = 0;
	SDR = 0;
    if (isBitmap)
	{
		_tmdBitmap.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
		texelSize = 1.0f / float2(width, height);
        
        UnityTexture2DArray bitmap = UnityBuildTexture2DArrayStruct(_tmdBitmap);
        bitmapColor = SAMPLE_TEXTURE2D_ARRAY(bitmap, sampler_LinearClamp, uvA.xy, arrayIndex);
        normal = float3(0, 0, -1);
        underlaySD = 0;
        return;
    }
    else
    {
        bitmapColor = float4(0, 0, 0, 0);        
        if (isSdf16)
        {
			SDR = 32; // = spread * 2 Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
            _tmdSdf16.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
            texelSize = 1.0f / float2(width, height);
            float offSetScale = SDR * texelSize.x;
            
            outlineColor1Offset *= offSetScale;
            underlayColorOffset *= offSetScale;
            
            UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf16);
            SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
            SD.y = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor1Offset, arrayIndex).r;
            underlaySD = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - underlayColorOffset, arrayIndex).r;
            
			GetSurfaceNormal(sdf, texelSize, uvA, arrayIndex, SDR, isFront, innerBevel, bevelAmount, bevelWidth, bevelRoundness, bevelClamp, normal);
		}
        else
        {
			SDR = 16; // = spread * 2 Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
            _tmdSdf8.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
            texelSize = 1.0f / float2(width, height);
            float offSetScale = SDR * texelSize.x;
            
            outlineColor1Offset *= offSetScale;
            underlayColorOffset *= offSetScale;
            
            UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf8);
            SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
            SD.y = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor1Offset, arrayIndex).r;
            underlaySD = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - underlayColorOffset, arrayIndex).r;
            
			GetSurfaceNormal(sdf, texelSize, uvA, arrayIndex, SDR, isFront, innerBevel, bevelAmount, bevelWidth, bevelRoundness, bevelClamp, normal);
		}
    }
}
//sample SDF 3 times: face, outline1, underlay
void Sample3Texture2DArrayUNLIT_float(
    float4 uvAandB,
    float4 atlasIndexScaleIsSdf16IsBitmap,   
    float2 underlayColorOffset,
    float2 outlineColor1Offset,
    out bool isBitmap,
    out float4 bitmapColor,
    out float2 texelSize,
	out float SDR,
    out float2 SD, //x: face, y: outline1
    out float underlaySD,
	out float2 uvA,
    out float2 uvB,
    out float scale)
{
	uint width, height, elements, numberOfLevels;
	uvA = uvAandB.xy;
	uvB = uvAandB.zw;
	float arrayIndex = atlasIndexScaleIsSdf16IsBitmap.x;
	scale = atlasIndexScaleIsSdf16IsBitmap.y;
	bool isSdf16 = atlasIndexScaleIsSdf16IsBitmap.z;
	isBitmap = atlasIndexScaleIsSdf16IsBitmap.w;
	SD = 0;
	SDR = 0;
    if (isBitmap)
	{
		_tmdBitmap.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
		texelSize = 1.0f / float2(width, height);
        
        UnityTexture2DArray bitmap = UnityBuildTexture2DArrayStruct(_tmdBitmap);
        bitmapColor = SAMPLE_TEXTURE2D_ARRAY(bitmap, sampler_LinearClamp, uvA.xy, arrayIndex);
        underlaySD = 0;
        return;
    }
    else
    {
        bitmapColor = float4(0, 0, 0, 0);        
        if (isSdf16)
        {
			SDR = 32; // = spread * 2 Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
            _tmdSdf16.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
            texelSize = 1.0f / float2(width, height);
            float offSetScale = SDR * texelSize.x;
            
            outlineColor1Offset *= offSetScale;
            underlayColorOffset *= offSetScale;
            
            UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf16);
            SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
            SD.y = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor1Offset, arrayIndex).r;
            underlaySD = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - underlayColorOffset, arrayIndex).r;
        }
        else
        {
			SDR = 16; // = spread * 2 Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
            _tmdSdf8.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
            texelSize = 1.0f / float2(width, height);
            float offSetScale = SDR * texelSize.x;
            
            outlineColor1Offset *= offSetScale;
            underlayColorOffset *= offSetScale;
            
            UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf8);
            SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
            SD.y = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - outlineColor1Offset, arrayIndex).r;
            underlaySD = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy - underlayColorOffset, arrayIndex).r;
        }
    }
}
//sample SDF 1+4 times: face, and 4x for light normal
void Sample1Texture2DArrayLIT_float(
    float4 uvAandB,
    float4 atlasIndexScaleIsSdf16IsBitmap,
    bool isFront,
    bool innerBevel,
    float bevelAmount,
    float bevelWidth,
    float bevelRoundness,
    float bevelClamp,
    out bool isBitmap,
    out float4 bitmapColor,
    out float2 texelSize,
	out float SDR,
    out float SD, //x: face
    out float3 normal,
	out float2 uvA,
    out float2 uvB,
    out float scale)
{
	uint width, height, elements, numberOfLevels;
	uvA = uvAandB.xy;
	uvB = uvAandB.zw;
	float arrayIndex = atlasIndexScaleIsSdf16IsBitmap.x;
	scale = atlasIndexScaleIsSdf16IsBitmap.y;
	bool isSdf16 = atlasIndexScaleIsSdf16IsBitmap.z;
	isBitmap = atlasIndexScaleIsSdf16IsBitmap.w;
    SD = 0;
	SDR = 0;
    if (isBitmap)
	{
		_tmdBitmap.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
		texelSize = 1.0f / float2(width, height);
        
        UnityTexture2DArray bitmap = UnityBuildTexture2DArrayStruct(_tmdBitmap);
		bitmapColor = SAMPLE_TEXTURE2D_ARRAY(bitmap, sampler_LinearClamp, uvA.xy, arrayIndex);
        normal = float3(0, 0, -1);
        return;
    }
    else
    {
        bitmapColor = float4(0, 0, 0, 0);        
        if (isSdf16)
        {
			SDR = 32; // = spread * 2 Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
            _tmdSdf16.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
            texelSize = 1.0f / float2(width, height);
            
            UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf16);
			SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
            
			GetSurfaceNormal(sdf, texelSize, uvA, arrayIndex, SDR, isFront, innerBevel, bevelAmount, bevelWidth, bevelRoundness, bevelClamp, normal);
		}
        else
        {
			SDR = 16; // = spread * 2 Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
            _tmdSdf8.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
            texelSize = 1.0f / float2(width, height);
            
            UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf8);
			SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
            
			GetSurfaceNormal(sdf, texelSize, uvA, arrayIndex, SDR, isFront, innerBevel, bevelAmount, bevelWidth, bevelRoundness, bevelClamp, normal);
		}
    }
}

//sample SDF 1 time: face
void Sample1Texture2DArrayUNLIT_float(
    float4 uvAandB,
    float4 atlasIndexScaleIsSdf16IsBitmap,
    out bool isBitmap,
    out float4 bitmapColor,
    out float2 texelSize,
	out float SDR,
    out float SD, //x: face
	out float2 uvA,
    out float2 uvB,
    out float scale)
{
	uint width, height, elements, numberOfLevels;
	uvA = uvAandB.xy;	
	uvB = uvAandB.zw;
	float arrayIndex = atlasIndexScaleIsSdf16IsBitmap.x;
	scale = atlasIndexScaleIsSdf16IsBitmap.y;
	bool isSdf16 = atlasIndexScaleIsSdf16IsBitmap.z;
	isBitmap = atlasIndexScaleIsSdf16IsBitmap.w;
	SD = 0;
	SDR = 0;
	if (isBitmap)
	{
		_tmdBitmap.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
		texelSize = 1.0f / float2(width, height);
        
		UnityTexture2DArray bitmap = UnityBuildTexture2DArrayStruct(_tmdBitmap);
		bitmapColor = SAMPLE_TEXTURE2D_ARRAY(bitmap, sampler_LinearClamp, uvA.xy, arrayIndex);
		return;
	}
	else
	{
		bitmapColor = float4(0, 0, 0, 0);        
		if (isSdf16)
		{
			SDR = 32; // = spread * 2 Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
			_tmdSdf16.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
			texelSize = 1.0f / float2(width, height);
            
			UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf16);
			SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
		}
		else
		{
			SDR = 16.0; // spread * 2. Note: choice of spread is tied to sampling size...pure coincidental that we can derive it from bit depth
			_tmdSdf8.GetDimensions(0, width, height, elements, numberOfLevels); // Get dimensions of mip level 0
			texelSize = 1.0f / float2(width, height);
            
			UnityTexture2DArray sdf = UnityBuildTexture2DArrayStruct(_tmdSdf8);
			SD.x = SAMPLE_TEXTURE2D_ARRAY(sdf, sampler_LinearClamp, uvA.xy, arrayIndex).r;
		}
	}
}
void GenerateUV(float2 inUV, float2 tiling, float2 offset, float2 animSpeed, out float2 outUV)
{
	outUV = inUV * tiling + offset + (animSpeed * _Time.y);
}
void SampleFaceTexture_float(
    float4 vertexColor,
    float2 uvB,
    UnityTexture2D faceTexture,
    float2 faceUVSpeed,
    float2 faceTiling,
    float2 faceOffset,
    out float4 colorOUT)
{
    float2 uvBOUT;
    GenerateUV(uvB, faceTiling, faceOffset, faceUVSpeed, uvBOUT);
    float4 textureColor = SAMPLE_TEXTURE2D(faceTexture, faceTexture.samplerstate, uvBOUT); //sampler_LinearClamp sampler_LinearRepeat
    colorOUT = vertexColor * textureColor;
}
#endif