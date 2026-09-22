#include "QvvsHelpers.hlsl"

// VFX Graph allows a Custom HLSL function at most four input slots. VFXExpression refuses more
// than four parents, and exceeding it throws while the node's slots resolve, which aborts the
// whole graph's compilation rather than flagging the node. A QVVS is three float4 slots, so any
// operator taking two of them routes them through a float4x4 instead. The fourth row is ignored.

float4x4 PackQvvs(float4 qvvsA, float4 qvvsB, float4 qvvsC)
{
	return float4x4(qvvsA, qvvsB, qvvsC, float4(0.0, 0.0, 0.0, 1.0));
}

void UnpackQvvs(float4x4 packedQvvs, out float4 qvvsA, out float4 qvvsB, out float4 qvvsC)
{
	qvvsA = packedQvvs[0];
	qvvsB = packedQvvs[1];
	qvvsC = packedQvvs[2];
}

void GetQvvsProperties(float4 qvvsA, float4 qvvsB, float4 qvvsC, out bool isAlive, out bool isEnabled, out float4 quaternion, out float3 position, out float scale, out float3 stretch, out int context32WithoutFlags, out float3 forward, out float3 up, out float3 right, out float4x4 toMatrix)
{
	TransformQvvs transform = ConvertToTransformQvvs(qvvsA, qvvsB, qvvsC);
	isAlive = (transform.context32 & 0x80000000) != 0;
	isEnabled = (transform.context32 & 0x40000000) != 0;
	quaternion = transform.rotation.value;
	position = transform.position;
	scale = transform.scale;
	stretch = transform.stretch;
	context32WithoutFlags = transform.context32 & 0x3fffffff;
	forward = rotate(transform.rotation, float3(0.0, 0.0, 1.0));
	up = rotate(transform.rotation, float3(0.0, 1.0, 0.0));
	right = rotate(transform.rotation, float3(1.0, 0.0, 0.0));
	toMatrix = transform.ToMatrix4x4();
}

// scaleAndStretch is (scale, stretch.xyz), packed so this stays within four inputs.
void ConstructQvvs(float3 position, float4 rotation, float4 scaleAndStretch, int context32, out float4 qvvsA, out float4 qvvsB, out float4 qvvsC)
{
	TransformQvvs transform = new_TransformQvvs(position, new_quaternion(rotation), scaleAndStretch.x, scaleAndStretch.yzw, context32);
	ConvertToVfxQvvs(transform, qvvsA, qvvsB, qvvsC);
}

void TransformByQvvs(float4 qvvsA, float4 qvvsB, float4 qvvsC, float3 v, out float3 p, out float3 inversePoint, out float3 direction, out float3 directionWithStretch, out float3 directionScaledAndStretched, out float3 inverseDirection, out float3 inverseDirectionWithStretch, out float3 inverseDirectionScaledAndStretched)
{
	TransformQvvs transform = ConvertToTransformQvvs(qvvsA, qvvsB, qvvsC);
	p = TransformPoint(transform, v);
	inversePoint = InverseTransformPoint(transform, v);
	direction = TransformDirection(transform, v);
	directionWithStretch = TransformDirectionWithStretch(transform, v);
	directionScaledAndStretched = TransformDirectionScaledAndStretched(transform, v);
	inverseDirection = InverseTransformDirection(transform, v);
	inverseDirectionWithStretch = InverseTransformDirectionWithStretch(transform, v);
	inverseDirectionScaledAndStretched = InverseTransformDirectionScaledAndStretched(transform, v);
}

void MulQvvs(float4x4 packedA, float4x4 packedB, out float4x4 packedAB, out float4x4 packedInverseAB)
{
	TransformQvvs a = ConvertToTransformQvvs(packedA[0], packedA[1], packedA[2]);
	TransformQvvs b = ConvertToTransformQvvs(packedB[0], packedB[1], packedB[2]);
	TransformQvvs ab = mul(a, b);
	TransformQvvs iab = inversemulqvvs(a, b);
	float4 abA, abB, abC, iabA, iabB, iabC;
	ConvertToVfxQvvs(ab, abA, abB, abC);
	ConvertToVfxQvvs(iab, iabA, iabB, iabC);
	packedAB = PackQvvs(abA, abB, abC);
	packedInverseAB = PackQvvs(iabA, iabB, iabC);
}

void RotateAbout(float4x4 packedQvvs, float4 rotation, float3 pivot, out float4 resultA, out float4 resultB, out float4 resultC)
{
	TransformQvvs transform = ConvertToTransformQvvs(packedQvvs[0], packedQvvs[1], packedQvvs[2]);
	TransformQvvs result = RotateAbout(transform, new_quaternion(rotation), pivot);
	ConvertToVfxQvvs(result, resultA, resultB, resultC);
}

// Quaternion

float4 MulQuat(float4 a, float4 b)
{
	return mul(new_quaternion(a), new_quaternion(b)).value;
}

float3 RotatePointByQuat(float4 quat, float3 p)
{
	return rotate(new_quaternion(quat), p);
}

float4 AxisAngleQuat(float3 axis, float angle)
{
	float3 fixedAxis = normalizesafe(axis);
	if (all(fixedAxis == 0.0))
		return float4(0.0, 0.0, 0.0, 1.0);
	return AxisAngle(fixedAxis, angle).value;
}

void LookRotationQuat(float3 forward, float3 up, out float4 quat, out float4 quatSafe)
{
	quat = LookRotation(forward, up).value;
	quatSafe = LookRotationSafe(forward, up).value;
}
