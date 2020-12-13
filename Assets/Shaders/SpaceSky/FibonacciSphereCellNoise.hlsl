//Source: https://www.shadertoy.com/view/lllXz4
//Source 2: https://dl.acm.org/doi/abs/10.1145/2816795.2818131

#define PHI (sqrt(5) * 0.5 + 0.5)

#define madfrac(A, B) mad((A), (B), -floor((A) * (B)))

float2x2 Inverse(float2x2 f2x2)
{
	return float2x2(f2x2[1][1], -f2x2[0][1], -f2x2[1][0], f2x2[0][0]) / determinant(f2x2);
}

void InverseSphericalFibonacci(float3 positionOnSphere, float numberOfPoints, out float3 nearestPoint, out float pointIndex, out float distanceToEdge)
{
	float phi = min(atan2(positionOnSphere.y, positionOnSphere.x), PI);
	float cosTheta = positionOnSphere.z;

	float k = max(2, floor(log(numberOfPoints * PI * sqrt(5) * (1 - cosTheta * cosTheta)) / log(PHI * PHI)));

	float Fk = pow(PHI, k) / sqrt(5);
	float F0 = round(Fk);
	float F1 = round(Fk * PHI);

	float2x2 B = float2x2(
		2 * PI * madfrac(F0 + 1, PHI - 1) - 2 * PI * (PHI - 1),
		2 * PI * madfrac(F1 + 1, PHI - 1) - 2 * PI * (PHI - 1),
		-2 * F0 / numberOfPoints,
		-2 * F1 / numberOfPoints);
	float2x2 invB = Inverse(B);
	float2 c = floor(mul(invB, float2(phi, cosTheta - (1 - 1 / numberOfPoints))));

	float d = 1024.0;
	float d2 = d;
	nearestPoint = float3(0, 0, 0);
	pointIndex = 0;

	for (uint s = 0; s < 4; s++)
	{
		float cosThetaLoop = dot(B[1], float2(s % 2, s / 2) + c) + (1 - 1 / numberOfPoints);
		cosThetaLoop = clamp(cosThetaLoop, -1, 1) * 2 - cosThetaLoop;

		float i = floor(numberOfPoints * 0.5 - cosThetaLoop * numberOfPoints * 0.5);
		float phiLoop = 2 * PI * madfrac(i, PHI - 1);
		cosThetaLoop = 1 - (2 * i + 1) * rcp(numberOfPoints);
		float sinThetaLoop = sqrt(1 - cosThetaLoop * cosThetaLoop);

		float3 q = float3(cos(phiLoop) * sinThetaLoop, sin(phiLoop) * sinThetaLoop, cosThetaLoop);

		float squareDistance = dot(q - positionOnSphere, q - positionOnSphere);
		if (squareDistance < d)
		{
			d2 = d;
			d = squareDistance;
			nearestPoint = q;
			pointIndex = i;
		}
		else if (squareDistance < d2)
		{
			d2 = squareDistance;
		}
	}

	distanceToEdge = (sqrt(d2) - sqrt(d)) * 0.5;
}

float3 SphericalFibonacci(float index, float numberOfPoints)
{
	float phi = 2 * PI * madfrac(index, PHI - 1);
	float cosTheta = 1 - (2 * index + 1) * rcp(numberOfPoints);
	float sinTheta = sqrt(saturate(1 - cosTheta * cosTheta));
	return float3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
}


float4 FibonacciSphereCellNoiseDebug(float3 viewDirWS)
{
	float pointCount = 1500;

	viewDirWS = normalize(viewDirWS.xzy);

	float3 nearestPoint;
	float pointIndex;
	float distanceToEdge;
	InverseSphericalFibonacci(viewDirWS, pointCount, nearestPoint, pointIndex, distanceToEdge);
	//return float4(1.0, 1.0, 1.0, 1.0) * pointIndex / pointCount;
	//return float4(nearestPoint, 1.0);

	float val = dot(viewDirWS - nearestPoint, viewDirWS - nearestPoint);
	float val2 = distanceToEdge;
	val = sqrt(val) * 2;
	return float4(0, val2, 0.0, 1.0) ;
}