using System.Collections;
using System.Collections.Generic;
using BoxCollider     = Latios.PhysicsEngine.BoxCollider;
using CapsuleCollider = Latios.PhysicsEngine.CapsuleCollider;
using Collider        = Latios.PhysicsEngine.Collider;
using Latios.PhysicsEngine;
using Physics = Latios.PhysicsEngine.Physics;
using TMPro;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[ExecuteInEditMode]
public class BoxDistanceBetween : MonoBehaviour
{
    public GameObject otherBox;
    public GameObject otherCapsule;
    public TMP_Text   text;

    // Update is called once per frame
    void Update()
    {
        var boxTransform = new RigidTransform(transform.rotation, transform.position);
        var boxCollider  = new BoxCollider(transform.GetChild(0).localPosition, transform.GetChild(0).localScale * 0.5f);

        if (otherBox != null)
        {
            var otherBoxTransform = new RigidTransform(otherBox.transform.rotation, otherBox.transform.position);
            var otherBoxCollider  = new BoxCollider(otherBox.transform.GetChild(0).localPosition, otherBox.transform.GetChild(0).localScale * 0.5f);
            var job               = new DrawDistances
            {
                colliderA  = boxCollider,
                transformA = boxTransform,
                colorA     = Color.red,
                colliderB  = otherBoxCollider,
                transformB = otherBoxTransform,
                colorB     = Color.green
            };
            job.Run();
        }

        if (otherCapsule != null)
        {
            var otherCapsuleTransform = new RigidTransform(otherCapsule.transform.rotation, otherCapsule.transform.position);
            var otherCapsuleCollider  = new CapsuleCollider(new float3(0f, -0.5f, 0f), new float3(0f, 0.5f, 0f), 0.5f);
            var job                   = new DrawDistances
            {
                colliderA  = boxCollider,
                transformA = boxTransform,
                colorA     = Color.yellow,
                colliderB  = otherCapsuleCollider,
                transformB = otherCapsuleTransform,
                colorB     = Color.blue
            };
            job.Run();
        }
    }

    [BurstCompile]
    struct DrawDistances : IJob
    {
        public Collider       colliderA;
        public RigidTransform transformA;
        public Color          colorA;
        public Collider       colliderB;
        public RigidTransform transformB;
        public Color          colorB;

        bool  hit;
        float distance;

        public void Execute()
        {
            hit      = Physics.DistanceBetween(colliderA, transformA, colliderB, transformB, 0f, out var result);
            distance = result.distance;
            Debug.DrawRay(result.hitpointA, result.normalA * 3, colorA, 0, false);
            //Debug.DrawRay(result.hitpointA, result.normalA * (-0.7f), colorA, 0, false);
            Debug.DrawRay(result.hitpointB, result.normalB * 3, colorB, 0, false);
            //Debug.DrawRay(result.hitpointB, result.normalB * (-0.7f), colorB, 0, false);
        }
    }
}

