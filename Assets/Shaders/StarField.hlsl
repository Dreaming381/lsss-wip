//This took a while to get the look I wanted. 
//I wanted a similar look to the Unity.Physics asteroid skybox but done procedurally rather than with a cubemap.
//I kept running into issues where if I made the stars too small they started flickering.
//If I made all of them big enough to not flicker, they looked like glitter and lost depth.
//Eventually I started hacking the hotspot code until I got small and stable stars.
//The code is not very optimized, but for how much I am getting with zero bandwidth requirements, 
//I am leaving it as is until I find more time.

//The actual starfield is based on this approach here: 
//https://www.overdraw.xyz/blog/2018/7/17/using-cellular-noise-to-generate-procedural-stars
//I originally made this in ShaderGraph, although I had to use a custom function node to get a 3D noise function.
//However, I was hitting the flickering limits with too few stars and thought the 3D noise was the culprit,
//so I looked into a spherical mapping technique. I landed on this approach:
//https://dl.acm.org/doi/abs/10.1145/2816795.2818131
//While it turned out that the culprit wasn't likely the 3D noise,
//the new mapping was a lot easier to visualize what was going on.

//One downside of the spherical fibonacci mapping was that it was no longer trivial to get the boundaries of cells.
//The boundaries are needed to ensure the randomly displaced stars don't cross cell boundaries and get chopped up.
//I solved this by modifying the inverseSF to report an approximate distance to the cell edge by also keeping track
//of the second-closest point in the search loop. However, this generates artifacts at the poles so I kill stars there.
//You can look at the artifacts yourself by mapping the edge distance to a color output.
//If you do, you'll also discover that I am using a z-up implementation in a y-up context. It works fine.
//The artifact regions have a unique shape to them that naturally fits into the star distribution pattern.
//The artifact regions also shrink when you increase the cell count.



float Rand(float index)
{
	return frac(sin(dot(float2(index, index), float2(12.9898, 78.233))) * 43758.5453);
}

float InverseLerp(float a, float b, float t)
{
    return (t - a) / (b - a);
}

float3 RotateAboutAxis(float3 i, float3 axis, float rotation)
{
    float s = sin(rotation);
    float c = cos(rotation);
    float one_minus_c = 1.0 - c;

    axis = normalize(axis);
    float3x3 rot_mat =
    { 
        one_minus_c * axis.x * axis.x + c, one_minus_c * axis.x * axis.y - axis.z * s, one_minus_c * axis.z * axis.x + axis.y * s,
        one_minus_c * axis.x * axis.y + axis.z * s, one_minus_c * axis.y * axis.y + c, one_minus_c * axis.y * axis.z - axis.x * s,
        one_minus_c * axis.z * axis.x - axis.y * s, one_minus_c * axis.y * axis.z + axis.x * s, one_minus_c * axis.z * axis.z + c
    };
    return mul(rot_mat, i);
}

float4 SampleGradient(Gradient gradient, float t)
{
    float3 color = gradient.colors[0].rgb;
    [unroll]
    for (int c = 1; c < 8; c++)
    {
        float colorPos = saturate((t - gradient.colors[c - 1].w) / (gradient.colors[c].w - gradient.colors[c - 1].w)) * step(c, gradient.colorsLength - 1);
        color = lerp(color, gradient.colors[c].rgb, lerp(colorPos, step(0.01, colorPos), gradient.type));
    }
#ifndef UNITY_COLORSPACE_GAMMA
    color = SRGBToLinear(color);
#endif
    float alpha = gradient.alphas[0].x;
    [unroll]
    for (int a = 1; a < 8; a++)
    {
        float alphaPos = saturate((t - gradient.alphas[a - 1].y) / (gradient.alphas[a].y - gradient.alphas[a - 1].y)) * step(a, gradient.alphasLength - 1);
        alpha = lerp(alpha, gradient.alphas[a].x, lerp(alphaPos, step(0.01, alphaPos), gradient.type));
    }
    return float4(color, alpha);
}

float3 DrawStarField(float3 viewDirWS, float starCellCount, float starRadius, float starBrightness, float starfieldDensity, float3 adjacentPixelViewDirWS[8])
{
    float resolution = sqrt(2 * PI) / starCellCount;
    
    float3 nearestPoint;
	float pointIndex;
	float distanceToEdge;
	InverseSphericalFibonacci(viewDirWS, starCellCount, nearestPoint, pointIndex, distanceToEdge);

    //While InverseSphericalFibonacci gives us much better resolution and accuracy near the pole, it still runs into corruption in the degenerate region.
    //In the cases where we fall into this region, just exit.
    if (pointIndex % starCellCount < 5 || starCellCount - pointIndex < 5)
        return float3(0, 0, 0);
    //if (distanceToEdge < 0.002)
    //    return float3(0, 0, 0);

	float3 arbitraryPointWrtCell = SphericalFibonacci((pointIndex + 1) % starCellCount, starCellCount);
	float3 arbitraryTangentWrtCell = cross(nearestPoint, arbitraryPointWrtCell);
    float3 displacementDirection = RotateAboutAxis(arbitraryTangentWrtCell, nearestPoint, Rand(pointIndex) * 2 * PI);
    float displacementMagnitude = Rand(pointIndex + starCellCount) * clamp(rcp(starfieldDensity), 0, 10000);
    float3 starPosition = normalize(nearestPoint + displacementDirection * displacementMagnitude);

    InverseSphericalFibonacci(starPosition, starCellCount, nearestPoint, pointIndex, distanceToEdge);

    starRadius = min(distanceToEdge, starRadius * resolution);

    float distanceToStar = distance(viewDirWS, starPosition);
    float starFalloff = saturate(InverseLerp(0, starRadius, distanceToStar));
    starFalloff = 1 - starFalloff;
    starFalloff = starFalloff * starFalloff;
    float starHotSpotColor = starFalloff * starFalloff * starBrightness;

    //There's an issue where stars like to flicker when the camera moves. This attempts to resolve it by adding a nearest-neighbor step.
    float minDistance = distanceToStar * distanceToStar;
    float hotSpotContribution = 0;
    for (uint i = 0; i < 8; i++)
    {
        float3 dif = starPosition - adjacentPixelViewDirWS[i];
        float newDistance = dot(dif, dif);
        minDistance = min(minDistance, newDistance);
        if (newDistance < starRadius * starRadius)
        {
            hotSpotContribution += 0.125;
        }
    }
    if (minDistance >= distanceToStar * distanceToStar - 0.000001)
    {
        starFalloff = max(starFalloff * hotSpotContribution, 0.1);
        starHotSpotColor = max(starHotSpotColor, hotSpotContribution * 1.0 * starBrightness);
    }
    
    
    if (distanceToEdge < starRadius * resolution + 0.000001)
    {
        //Star is small and faint, turn off hotspot
        starHotSpotColor = 0;
    }

    Gradient g;
    g.type = 0;
    g.colorsLength = 6;
    g.alphasLength = 2;
    g.colors[0] = float4(1, 0, 0, 0.1029374);
    g.colors[1] = float4(1, 0.9456214, 0.09803921, 0.4441138);
    g.colors[2] = float4(1, 1, 1, 0.6558785);
    g.colors[3] = float4(1, 1, 1, 0.7647059);
    g.colors[4] = float4(0.2392157, 0.7058824, 1, 0.8823529);
    g.colors[5] = float4(0, 0.2091522, 1, 1);
    g.colors[6] = float4(0, 0, 0, 0);
    g.colors[7] = float4(0, 0, 0, 0);
    g.alphas[0] = float2(1, 0);
    g.alphas[1] = float2(1, 1);
    g.alphas[2] = float2(0, 0);
    g.alphas[3] = float2(0, 0);
    g.alphas[4] = float2(0, 0);
    g.alphas[5] = float2(0, 0);
    g.alphas[6] = float2(0, 0);
    g.alphas[7] = float2(0, 0);

    float3 starColor = SampleGradient(g, Rand(pointIndex + 4 * starCellCount));
    return lerp(starColor, 1, starFalloff) * starFalloff; // +starHotSpotColor;
}

float3 Draw4StarFields(float3 viewDirWS, float4 starCellCount, float4 starRadius, float4 starBrightness, float4 starFieldDensity, float3 adjacentPixelViewDirWS[8])
{
    float3 field1 = DrawStarField(viewDirWS, starCellCount.x, starRadius.x, starBrightness.x, starFieldDensity.x, adjacentPixelViewDirWS);
    float3 field2 = DrawStarField(viewDirWS, starCellCount.y, starRadius.y, starBrightness.y, starFieldDensity.y, adjacentPixelViewDirWS);
    float3 field3 = DrawStarField(viewDirWS, starCellCount.z, starRadius.z, starBrightness.z, starFieldDensity.z, adjacentPixelViewDirWS);
    float3 field4 = DrawStarField(viewDirWS, starCellCount.w, starRadius.w, starBrightness.w, starFieldDensity.w, adjacentPixelViewDirWS);
    return field1 + field2 + field3 + field4;
}

float3 StarFieldDebug(float3 viewDirWS, float3 viewDirWSAdjacent[8])
{
    float3 field1 = DrawStarField(viewDirWS, 2000, 0.8, 2.0, 4.8, viewDirWSAdjacent);
    float3 field2 = DrawStarField(viewDirWS, 1000, 0.9, 3.0, 3.0, viewDirWSAdjacent);
    float3 field3 = DrawStarField(viewDirWS, 500, 0.8, 4.0, 0.4, viewDirWSAdjacent);
    float3 field4 = DrawStarField(viewDirWS, 10000, 0.9, 0.5, 25.0, viewDirWSAdjacent);
    return field1 + field2 + field3 + field4;
}