﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     TextTransform Physics/Utilities/Physics.ColliderCastDispatch.tt
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using Unity.Mathematics;

namespace Latios.Psyshock
{
    public static partial class Physics
    {
        public static bool ColliderCast(Collider colliderToCast,
                                        RigidTransform castStart,
                                        float3 castEnd,
                                        SphereCollider targetSphere,
                                        RigidTransform targetSphereTransform,
                                        out ColliderCastResult result)
        {
            switch (colliderToCast.type)
            {
                case ColliderType.Sphere:
                {
                    SphereCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetSphere, targetSphereTransform, out result);
                }
                case ColliderType.Capsule:
                {
                    CapsuleCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetSphere, targetSphereTransform, out result);
                }
                case ColliderType.Box:
                {
                    BoxCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetSphere, targetSphereTransform, out result);
                }
                case ColliderType.Compound:
                {
                    CompoundCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetSphere, targetSphereTransform, out result);
                }
                default:
                    result = default;
                    return false;
            }
        }

        public static bool ColliderCast(SphereCollider sphereToCast,
                                        RigidTransform castStart,
                                        float3 castEnd,
                                        Collider targetCollider,
                                        RigidTransform targetTransform,
                                        out ColliderCastResult result)
        {
            switch (targetCollider.type)
            {
                case ColliderType.Sphere:
                {
                    SphereCollider col = targetCollider;
                    return ColliderCast(sphereToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Capsule:
                {
                    CapsuleCollider col = targetCollider;
                    return ColliderCast(sphereToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Box:
                {
                    BoxCollider col = targetCollider;
                    return ColliderCast(sphereToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Compound:
                {
                    CompoundCollider col = targetCollider;
                    return ColliderCast(sphereToCast, castStart, castEnd, col, targetTransform, out result);
                }
                default:
                    result = default;
                    return false;
            }
        }
        /*public static bool ColliderCast(Collider colliderToCast, RigidTransform castStart, float3 castEnd, CapsuleCollider targetCapsule, RigidTransform targetCapsuleTransform, out ColliderCastResult result)
           {
            switch (colliderToCast.type)
            {
                case ColliderType.Sphere:
                {
                    SphereCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetCapsule, targetCapsuleTransform, out result);
                }
                case ColliderType.Capsule:
                {
                    CapsuleCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetCapsule, targetCapsuleTransform, out result);
                }
                case ColliderType.Box:
                {
                    BoxCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetCapsule, targetCapsuleTransform, out result);
                }
                case ColliderType.Compound:
                {
                    CompoundCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetCapsule, targetCapsuleTransform, out result);
                }
                default:
                    result = default;
                    return false;
            }
           }

           public static bool ColliderCast(CapsuleCollider capsuleToCast, RigidTransform castStart, float3 castEnd, Collider targetCollider, RigidTransform targetTransform, out ColliderCastResult result)
           {
            switch (targetCollider.type)
            {
                case ColliderType.Sphere:
                {
                    SphereCollider col = targetCollider;
                    return ColliderCast(capsuleToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Capsule:
                {
                    CapsuleCollider col = targetCollider;
                    return ColliderCast(capsuleToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Box:
                {
                    BoxCollider col = targetCollider;
                    return ColliderCast(capsuleToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Compound:
                {
                    CompoundCollider col = targetCollider;
                    return ColliderCast(capsuleToCast, castStart, castEnd, col, targetTransform, out result);
                }
                default:
                    result = default;
                    return false;
            }
           }
           public static bool ColliderCast(Collider colliderToCast, RigidTransform castStart, float3 castEnd, BoxCollider targetBox, RigidTransform targetBoxTransform, out ColliderCastResult result)
           {
            switch (colliderToCast.type)
            {
                case ColliderType.Sphere:
                {
                    SphereCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetBox, targetBoxTransform, out result);
                }
                case ColliderType.Capsule:
                {
                    CapsuleCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetBox, targetBoxTransform, out result);
                }
                case ColliderType.Box:
                {
                    BoxCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetBox, targetBoxTransform, out result);
                }
                case ColliderType.Compound:
                {
                    CompoundCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetBox, targetBoxTransform, out result);
                }
                default:
                    result = default;
                    return false;
            }
           }

           public static bool ColliderCast(BoxCollider boxToCast, RigidTransform castStart, float3 castEnd, Collider targetCollider, RigidTransform targetTransform, out ColliderCastResult result)
           {
            switch (targetCollider.type)
            {
                case ColliderType.Sphere:
                {
                    SphereCollider col = targetCollider;
                    return ColliderCast(boxToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Capsule:
                {
                    CapsuleCollider col = targetCollider;
                    return ColliderCast(boxToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Box:
                {
                    BoxCollider col = targetCollider;
                    return ColliderCast(boxToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Compound:
                {
                    CompoundCollider col = targetCollider;
                    return ColliderCast(boxToCast, castStart, castEnd, col, targetTransform, out result);
                }
                default:
                    result = default;
                    return false;
            }
           }
           public static bool ColliderCast(Collider colliderToCast, RigidTransform castStart, float3 castEnd, CompoundCollider targetCompound, RigidTransform targetCompoundTransform, out ColliderCastResult result)
           {
            switch (colliderToCast.type)
            {
                case ColliderType.Sphere:
                {
                    SphereCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetCompound, targetCompoundTransform, out result);
                }
                case ColliderType.Capsule:
                {
                    CapsuleCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetCompound, targetCompoundTransform, out result);
                }
                case ColliderType.Box:
                {
                    BoxCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetCompound, targetCompoundTransform, out result);
                }
                case ColliderType.Compound:
                {
                    CompoundCollider col = colliderToCast;
                    return ColliderCast(col, castStart, castEnd, targetCompound, targetCompoundTransform, out result);
                }
                default:
                    result = default;
                    return false;
            }
           }

           public static bool ColliderCast(CompoundCollider compoundToCast, RigidTransform castStart, float3 castEnd, Collider targetCollider, RigidTransform targetTransform, out ColliderCastResult result)
           {
            switch (targetCollider.type)
            {
                case ColliderType.Sphere:
                {
                    SphereCollider col = targetCollider;
                    return ColliderCast(compoundToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Capsule:
                {
                    CapsuleCollider col = targetCollider;
                    return ColliderCast(compoundToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Box:
                {
                    BoxCollider col = targetCollider;
                    return ColliderCast(compoundToCast, castStart, castEnd, col, targetTransform, out result);
                }
                case ColliderType.Compound:
                {
                    CompoundCollider col = targetCollider;
                    return ColliderCast(compoundToCast, castStart, castEnd, col, targetTransform, out result);
                }
                default:
                    result = default;
                    return false;
            }
           }

           public static bool ColliderCast(Collider colliderToCast, RigidTransform castStart, float3 castEnd, Collider targetCollider, RigidTransform targetTransform, out ColliderCastResult result)
           {
            switch (colliderToCast.type)
            {
                case ColliderType.Sphere:
                {
                    SphereCollider colA = colliderA;
                    return ColliderCast(colA, castStart, castEnd, targetCollider, targetTransform, out result);
                }
                case ColliderType.Capsule:
                {
                    CapsuleCollider colA = colliderA;
                    return ColliderCast(colA, castStart, castEnd, targetCollider, targetTransform, out result);
                }
                case ColliderType.Box:
                {
                    BoxCollider colA = colliderA;
                    return ColliderCast(colA, castStart, castEnd, targetCollider, targetTransform, out result);
                }
                case ColliderType.Compound:
                {
                    CompoundCollider colA = colliderA;
                    return ColliderCast(colA, castStart, castEnd, targetCollider, targetTransform, out result);
                }
                default:
                    result = default;
                    return false;
            }
           }*/
    }
}

