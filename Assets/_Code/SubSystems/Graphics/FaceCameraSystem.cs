﻿using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Lsss
{
    [BurstCompile]
    public partial struct FaceCameraSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // We use NativeList here because NativeReference is broken in ISystem
            var foundCamera = new NativeReference<float3>(state.WorldUnmanaged.UpdateAllocator.ToAllocator);

            new JobA { foundCamera = foundCamera }.Schedule();
            new JobB {foundCamera  = foundCamera}.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(CameraManager))]
        partial struct JobA : IJobEntity
        {
            public NativeReference<float3> foundCamera;

            public void Execute(in Translation translation)
            {
                foundCamera.Value = translation.Value;
            }
        }

        [BurstCompile]
        [WithAll(typeof(FaceCameraTag))]
        partial struct JobB : IJobEntity
        {
            [ReadOnly] public NativeReference<float3> foundCamera;

            public void Execute(ref Rotation rotation, in LocalToWorld ltw)
            {
                var    camPos    = foundCamera.Value;
                float3 direction = math.normalize(camPos - ltw.Position);
                if (math.abs(math.dot(direction, new float3(0f, 1f, 0f))) < 0.9999f)
                {
                    var parentRot  = math.mul(ltw.Rotation, math.inverse(rotation.Value));
                    rotation.Value = math.mul(math.inverse(parentRot), quaternion.LookRotationSafe(direction, new float3(0f, 1f, 0f)));
                }
            }
        }
    }
}

