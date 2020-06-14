using Latios;
using Latios.PhysicsEngine;
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
}

