using Latios;
using Latios.Psyshock;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace Lsss
{
    [MaterialProperty("_Fade", MaterialPropertyFormat.Float)]
    public struct FadeProperty : IComponentData
    {
        public float fade;
    }

    public struct TimeToLiveFadeStart : IComponentData
    {
        public float fadeTimeWindow;
    }

    public struct CameraMountPoint : IComponentData
    {
        public Entity mountPoint;
    }

    public struct FaceCameraTag : IComponentData { }

    public struct SpawnPointAnimationData : IComponentData
    {
        public float growSpins;  //radians
        public float shrinkSpins;  //radians

        //Animation depends on TimeToLive
        public float growStartTime;
        public float growEndTime;
        public float shrinkStartTime;
    }

    public struct IntegratedSpeed : IComponentData
    {
        public float integratedSpeed;
    }

    public struct SpeedEntity : IComponentData
    {
        public Entity entityWithSpeed;
    }

    [MaterialProperty("_Speed", MaterialPropertyFormat.Float)]
    public struct SpeedProperty : IComponentData
    {
        public float speed;
    }

    [MaterialProperty("_IntegratedSpeed", MaterialPropertyFormat.Float)]
    public struct IntegratedSpeedProperty : IComponentData
    {
        public float integratedSpeed;
    }

    public struct GravityWarpZoneTag : IComponentData { }

    public struct GravityWarpZone : IComponentData
    {
        public float swarchschildRadius;
        public float maxW;
    }

    public struct GravityWarpZoneRadius : IComponentData
    {
        public float radius;
    }

    [MaterialProperty("_WarpPositionRadius", MaterialPropertyFormat.Float4)]
    public struct GravityWarpZonePositionRadiusProperty : IComponentData
    {
        public float3 position;
        public float  radius;
    }

    [MaterialProperty("_WarpParams", MaterialPropertyFormat.Float4)]
    public struct GravityWarpZoneParamsProperty : IComponentData
    {
        public float active;
        public float swarchschildRadius;
        public float maxW;
        public float padding;
    }

    public struct BackupRenderBounds : IComponentData
    {
        public RenderBounds bounds;
    }
}

