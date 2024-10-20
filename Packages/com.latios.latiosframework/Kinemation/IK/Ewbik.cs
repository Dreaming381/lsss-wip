using System;
using System.Collections.Generic;
using Latios.Transforms;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Latios.Kinemation
{
    public static class Ewbik
    {
        public struct Target
        {
            public quaternion rootRelativeRotation;
            public float3     rootRelativePosition;
            public float3     boneLocalPositionOffsetToMatchTargetPosition;
            public short      boneIndex;
            public ushort     targetUserId;
            public float      rotationWeight;
            public float      positionWeight;
        }

        public interface IConstraintSolver
        {
            // Bones that are fixed to parent will skip being solved in a chain
            public bool IsFixedToParent(OptimizedBone bone);
            // Allows for any constraint solver setup before an iteration. Return true if another iteration is needed.
            public bool NeedsSkeletonIteration(OptimizedSkeletonAspect skeleton, ReadOnlySpan<Target> sortedTargets, int iterationsPerformedSoFar);
            // Allows the constraint solver to specify on a per-bone per-innerloop basis to specify whether the solver should propose a translation.
            // Otherwise, it will optimize purely for rotation, which may allow for faster convergence when the bone cannot move or scale easily.
            public bool UseTranslationInSolve(OptimizedBone bone, int iterationsSoFarForThisBone);
            // The constraint solver is responsible for applying the proposed new orientation and translation (translation could be applied as stretch instead)
            // as well as any damping. Return true to request another immediate solve iteration on this same bone.
            public bool ApplyConstraintsToBone(OptimizedBone bone, in RigidTransform proposedTransformDelta, in BoneSolveState boneSolveState);
        }

        public ref struct BoneSolveState
        {
            internal Span<float3> currentPoints;
            internal Span<float3> targetPoints;
            internal Span<float>  weights;
            internal int          boneIterations;
            internal int          skeletonIterations;

            // Always 1 or greater
            public int iterationsSoFarForThisBone => boneIterations;
            public int iterationsCompletedForSkeleton => skeletonIterations;

            public float MeanSquareDistanceFrom(TransformQvvs deltaTransformToApply)
            {
                float msd         = 0f;
                float totalWeight = 0f;
                for (int i = 0; i < currentPoints.Length; i++)
                {
                    msd         += weights[i] * math.distancesq(qvvs.TransformPoint(deltaTransformToApply, currentPoints[i]), targetPoints[i]);
                    totalWeight += weights[i];
                }
                return msd / totalWeight;
            }
        }

        public static unsafe void Solve<T>(ref OptimizedSkeletonAspect skeleton, ref Span<Target> targets, ref T constraintSolver) where T : unmanaged, IConstraintSolver
        {
            // Our setup is a bit complicated. We need to figure out the bone solve order, which is simply iterating the bones backwards,
            // except we skip bones that don't have targets on themselves or descendants or are fixed to their parent.
            // In addition, we build a list of indices into our sorted targets (we sort them by bone for performance) per bone, such that
            // each bone knows all targets that influence both itself and its descendants.
            fixed (Target* p = &targets[0])
            {
                NativeSortExtension.Sort(p, targets.Length, new TargetSorter());
            }
            Span<SolveBoneItem> solveList            = stackalloc SolveBoneItem[skeleton.boneCount];
            Span<short>         indexInSolveList     = stackalloc short[skeleton.boneCount];
            int                 expandedTargetsCount = 0;
            {
                Span<short> targetCountsByBone = stackalloc short[skeleton.boneCount];
                targetCountsByBone.Clear();
                int currentTargetIndex = targets.Length - 1;
                for (int i = targetCountsByBone.Length - 1; i >= 0; i--)
                {
                    short count = 0;
                    while (currentTargetIndex >= 0 && targets[currentTargetIndex].boneIndex > i)
                        currentTargetIndex--;
                    for (; currentTargetIndex >= 0 && targets[currentTargetIndex].boneIndex == i; currentTargetIndex--)
                    {
                        count++;
                    }
                    targetCountsByBone[i] += count;
                    var optimizedBone      = skeleton.bones[i];
                    if (optimizedBone.index > 0)
                        targetCountsByBone[optimizedBone.parentIndex] += targetCountsByBone[i];
                    if (constraintSolver.IsFixedToParent(optimizedBone))
                        targetCountsByBone[i] = 0;
                }

                short solveListLength = 0;
                int   runningStart    = 0;

                for (short i = (short)(indexInSolveList.Length - 1); i >= 0; i--)
                {
                    if (targetCountsByBone[i] == 0)
                        indexInSolveList[i] = -1;
                    else
                    {
                        indexInSolveList[i]        = solveListLength;
                        solveList[solveListLength] = new SolveBoneItem
                        {
                            targetsByBoneStart = runningStart,
                            targetsByBoneCount = 0,
                            boneIndex          = i
                        };
                        runningStart += targetCountsByBone[i];
                        solveListLength++;
                    }
                }
                solveList            = solveList.Slice(0, solveListLength);
                expandedTargetsCount = runningStart;
            }
            Span<short> targetIndicesByBone = stackalloc short[expandedTargetsCount];
            {
                int   currentIndexInSolveList = 0;
                short currentTargetIndex      = (short)(targets.Length - 1);
                for (int boneIndex = solveList[0].boneIndex; boneIndex >= 0; boneIndex--)
                {
                    while (currentIndexInSolveList < solveList.Length && solveList[currentIndexInSolveList].boneIndex > boneIndex)
                        currentIndexInSolveList++;

                    while (currentTargetIndex >= 0 && targets[currentTargetIndex].boneIndex > boneIndex)
                        currentTargetIndex--;

                    if (currentIndexInSolveList < solveList.Length && solveList[currentIndexInSolveList].boneIndex == boneIndex)
                    {
                        ref var solveItem = ref solveList[currentIndexInSolveList];
                        for (; currentTargetIndex >= 0 && targets[currentTargetIndex].boneIndex == boneIndex; currentTargetIndex--)
                        {
                            targetIndicesByBone[solveItem.targetsByBoneStart + solveItem.targetsByBoneCount] = currentTargetIndex;
                            solveItem.targetsByBoneCount++;
                        }

                        var parentIndex = skeleton.bones[boneIndex].parentIndex;
                        while (parentIndex > 0 && indexInSolveList[parentIndex] == -1)
                            parentIndex = skeleton.bones[parentIndex].parentIndex;
                        if (parentIndex > 0)
                        {
                            ref var parentItem = ref solveList[indexInSolveList[parentIndex]];
                            for (int i = 0; i < solveItem.targetsByBoneCount; i++)
                            {
                                targetIndicesByBone[parentItem.targetsByBoneStart + parentItem.targetsByBoneCount] = targetIndicesByBone[solveItem.targetsByBoneStart + i];
                                parentItem.targetsByBoneCount++;
                            }
                        }
                    }
                    else
                    {
                        // A target could influence a bone that is fixed to its parent.
                        var parentIndex = skeleton.bones[boneIndex].parentIndex;
                        while (parentIndex > 0 && indexInSolveList[parentIndex] == -1)
                            parentIndex = skeleton.bones[parentIndex].parentIndex;
                        if (parentIndex > 0)
                        {
                            ref var parentItem = ref solveList[indexInSolveList[parentIndex]];

                            for (; currentTargetIndex >= 0 && targets[currentTargetIndex].boneIndex == boneIndex; currentTargetIndex--)
                            {
                                targetIndicesByBone[parentItem.targetsByBoneStart + parentItem.targetsByBoneCount] = currentTargetIndex;
                                parentItem.targetsByBoneCount++;
                            }
                        }
                    }
                }
            }

            // Next step, we can start solving.
            int skeletonIterations = 0;
            while (constraintSolver.NeedsSkeletonIteration(skeleton, targets, skeletonIterations))
            {
                for (int solveItemIndex = 0; solveItemIndex < solveList.Length; solveItemIndex++)
                {
                    var          solveItem             = solveList[solveItemIndex];
                    var          bone                  = skeleton.bones[solveItem.boneIndex];
                    var          boneTargetIndices     = targetIndicesByBone.Slice(solveItem.targetsByBoneStart, solveItem.targetsByBoneCount);
                    var          conservativePairCount = 7 * solveItem.targetsByBoneCount;
                    Span<float3> currentPoints         = stackalloc float3[conservativePairCount];
                    Span<float3> targetPoints          = stackalloc float3[conservativePairCount];
                    Span<float>  weights               = stackalloc float[conservativePairCount];
                    int          boneSolveIterations   = 0;
                    var          boneTransform         = bone.rootTransform;
                    bool         repeat                = false;

                    do
                    {
                        int pairCount = 0;
                        for (int i = 0; i < boneTargetIndices.Length; i++)
                        {
                            ref var target                  = ref targets[boneTargetIndices[i]];
                            var     targetedBoneTransform   = skeleton.bones[target.boneIndex].rootTransform;
                            targetedBoneTransform.position -= boneTransform.position;
                            var targetPosition              = target.rootRelativePosition - boneTransform.position;
                            var boneOffsetPosition          = qvvs.TransformPoint(targetedBoneTransform, target.boneLocalPositionOffsetToMatchTargetPosition);
                            if (target.positionWeight > 0f)
                            {
                                currentPoints[pairCount] = boneOffsetPosition;
                                targetPoints[pairCount]  = targetPosition;
                                weights[pairCount]       = target.positionWeight;
                                pairCount++;
                            }
                            if (target.rotationWeight > 0f)
                            {
                                var matrixCurrent = new float3x3(targetedBoneTransform.rotation);
                                var matrixTarget  = new float3x3(target.rootRelativeRotation);

                                currentPoints[pairCount] = boneOffsetPosition + matrixCurrent.c0;
                                targetPoints[pairCount]  = targetPosition + matrixTarget.c0;
                                weights[pairCount]       = target.rotationWeight;
                                pairCount++;
                                currentPoints[pairCount] = boneOffsetPosition - matrixCurrent.c0;
                                targetPoints[pairCount]  = targetPosition - matrixTarget.c0;
                                weights[pairCount]       = target.rotationWeight;
                                pairCount++;

                                currentPoints[pairCount] = boneOffsetPosition + matrixCurrent.c1;
                                targetPoints[pairCount]  = targetPosition + matrixTarget.c1;
                                weights[pairCount]       = target.rotationWeight;
                                pairCount++;
                                currentPoints[pairCount] = boneOffsetPosition - matrixCurrent.c1;
                                targetPoints[pairCount]  = targetPosition - matrixTarget.c1;
                                weights[pairCount]       = target.rotationWeight;
                                pairCount++;

                                currentPoints[pairCount] = boneOffsetPosition + matrixCurrent.c2;
                                targetPoints[pairCount]  = targetPosition + matrixTarget.c2;
                                weights[pairCount]       = target.rotationWeight;
                                pairCount++;
                                currentPoints[pairCount] = boneOffsetPosition - matrixCurrent.c2;
                                targetPoints[pairCount]  = targetPosition - matrixTarget.c2;
                                weights[pairCount]       = target.rotationWeight;
                                pairCount++;
                            }
                        }

                        var useTranslation = constraintSolver.UseTranslationInSolve(bone, boneSolveIterations);
                        boneSolveIterations++;
                        var boneSolveState = new BoneSolveState
                        {
                            currentPoints      = currentPoints.Slice(0, pairCount),
                            targetPoints       = targetPoints.Slice(0, pairCount),
                            weights            = weights.Slice(0, pairCount),
                            boneIterations     = boneSolveIterations,
                            skeletonIterations = skeletonIterations,
                        };
                        var proposedDelta = Qcp.Solve(boneSolveState.currentPoints, boneSolveState.targetPoints, boneSolveState.weights, useTranslation);
                        repeat            = constraintSolver.ApplyConstraintsToBone(bone, in proposedDelta, in boneSolveState);
                    }
                    while (repeat);
                }
                skeletonIterations++;
            }
        }

        struct SolveBoneItem
        {
            public int   targetsByBoneStart;
            public short targetsByBoneCount;
            public short boneIndex;
        }

        struct TargetSorter : IComparer<Target>
        {
            public int Compare(Target a, Target b)
            {
                return a.boneIndex.CompareTo(b.boneIndex);
            }
        }
    }
}

