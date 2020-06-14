//I'm not crazy into the results this generates, but don't feel like spending more time on it.
//It uses much of the starfield algorithm, but then fills the pixels with high-detail noise to
//get that dust-field look. In the future, layering the different elements with different radii
//will likely get better results. I still need better control over the noise shapes though.

//Step 1: Draw the red dust everywhere
float3 DrawNebulaDust(float3 viewDirWS)
{
    float mask = cnoise(viewDirWS * 2);
    
    float dust = cnoise(viewDirWS * 300);
    float colorControl = cnoise(viewDirWS * 10);
    for (uint i = 0; i < 4; i++)
    {
        dust = cnoise(viewDirWS + dust);
        mask *= cnoise(viewDirWS * i + 0.5);
    }
    return saturate(dust * dust * mask) * 0.1 * lerp(float3(255, 0, 0), float3(255, 35, 0), colorControl);
}

//Step 2: Draw the emission nebulas
float3 DrawEmissionNebulas(float3 viewDirWS, float nebulaCellCount, float nebulaFieldDensity, float nebulaBrightness)
{
    float n1 = cnoise(viewDirWS * 10);
    float n2 = cnoise(viewDirWS.xzy * 2);
    float n3 = cnoise(viewDirWS.yzx * 300);
    float n4 = cnoise(viewDirWS.zxy * 40);
    float3 combo = float3(n1, n2, n3);
    float n5 = cnoise(combo + float3(1.0, 2.0, 3.0));
    float n6 = cnoise(combo.zyx + float3(54.0, 45.8, 2.35));
    float n7 = cnoise(combo.zxy + n5);

    for (uint i = 0; i < 3; i++)
    {
        n6 = cnoise(combo + n6);
        n7 = cnoise(combo + n7);
    }

    //float nebulaCellCount = 40;
    //float nebulafieldDensity = 4.0;
    float nebulaRadius = 50000.9;
    // nebulaBrightness = 100;

    float resolution = sqrt(2 * PI) / nebulaCellCount;

    float3 nearestPoint;
    float pointIndex;
    float distanceToEdge;
    InverseSphericalFibonacci(viewDirWS, nebulaCellCount, nearestPoint, pointIndex, distanceToEdge);

    //return distanceToEdge;

    //While InverseSphericalFibonacci gives us much better resolution and accuracy near the pole, it still runs into corruption in the degenerate region.
    //In the cases where we fall into this region, just exit.
    if (pointIndex % nebulaCellCount < 5 || nebulaCellCount - pointIndex < 5)
        return float3(0, 0, 0);

    float3 arbitraryPointWrtCell = SphericalFibonacci((pointIndex + 1) % nebulaCellCount, nebulaCellCount);
    float3 arbitraryTangentWrtCell = cross(nearestPoint, arbitraryPointWrtCell);
    float3 displacementDirection = RotateAboutAxis(arbitraryTangentWrtCell, nearestPoint, Rand(pointIndex) * 2 * PI);
    float displacementMagnitude = Rand(pointIndex + nebulaCellCount) * clamp(rcp(nebulaFieldDensity), 0, 10000);
    float3 nebulaPosition = normalize(nearestPoint + displacementDirection * displacementMagnitude);

    InverseSphericalFibonacci(nebulaPosition, nebulaCellCount, nearestPoint, pointIndex, distanceToEdge);

    nebulaRadius = min(distanceToEdge, nebulaRadius * resolution);

    float distanceToStar = distance(viewDirWS, nebulaPosition);
    float nebulaFalloff = saturate(InverseLerp(0, nebulaRadius, distanceToStar));
    nebulaFalloff = 1 - nebulaFalloff;
    nebulaFalloff = nebulaFalloff * nebulaFalloff;
    float nebulaHotSpotColor = nebulaFalloff * nebulaFalloff * nebulaBrightness;

    if (distanceToEdge < nebulaRadius * resolution + 0.000001)
    {
        //Nebula is small and faint, turn off hotspot
        nebulaHotSpotColor = 0;
    }

    Gradient g = NewGradient(
        0,
        7,
        2,
        float4(1, 0.01370208, 0.009721218, 0),
        float4(1, 0.1333333, 0, 0.1353018),
        float4(0.1921569, 0, 0.172549, 0.3558862),
        float4(0.292711, 0, 0.8584906, 0.5647059),
        float4(0, 0.01007949, 0.3207547, 0.6705883),
        float4(0.0710217, 0.3663538, 0.7924528, 0.7735256),
        float4(0, 0.5450981, 0.4887907, 0.8911726),
        float4(0, 0, 0, 0),
        float2(1, 0),
        float2(1, 1),
        float2(0, 0),
        float2(0, 0),
        float2(0, 0),
        float2(0, 0),
        float2(0, 0),
        float2(0, 0));

    float3 nebulaColor = SampleGradient(g, Rand(pointIndex + 4 * nebulaCellCount));
    nebulaColor += saturate(SampleGradient(g, Rand(n4)));
    nebulaColor += 2 * SampleGradient(g, nebulaFalloff);
    return lerp(nebulaColor, nebulaColor + 1, nebulaFalloff) * nebulaFalloff * (n7 + 0.5) * (n6 + 0.5) * n2; // +nebulaHotSpotColor;
}

float3 DrawAllNebulas(float3 viewDirWS, float nebulaCellCount, float nebulaDensity, float nebulaBrightness)
{
    float3 dust1 = DrawNebulaDust(viewDirWS);
    float3 dust2 = DrawNebulaDust(viewDirWS * 1.1);
    float3 dust3 = DrawNebulaDust(viewDirWS * 1.2);

    float3 emissionNebula = DrawEmissionNebulas(viewDirWS, nebulaCellCount, nebulaDensity, nebulaBrightness);

    return dust1 + dust2 + dust3 + emissionNebula;
    return DrawNebulaDust(viewDirWS);
}

float3 NebulaDebug(float3 viewDirWS)
{
    float3 dust1 = DrawNebulaDust(viewDirWS);
    float3 dust2 = DrawNebulaDust(viewDirWS * 1.1);
    float3 dust3 = DrawNebulaDust(viewDirWS * 1.2);

    float3 emissionNebula = DrawEmissionNebulas(viewDirWS, 40, 4, 100);
    
    return dust1 + dust2 + dust3 + emissionNebula;
    return DrawNebulaDust(viewDirWS);
}