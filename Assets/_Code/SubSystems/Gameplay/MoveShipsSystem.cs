﻿using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    public partial struct MoveShipsSystem : ISystem
    {
        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            latiosWorld = state.GetLatiosWorldUnmanaged();
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt          = Time.DeltaTime;
            var arenaRadius = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaRadius>().radius;

            new Job { dt = dt, arenaRadius = arenaRadius }.ScheduleParallel();
        }

        [BurstCompile]
        [WithAll(typeof(ShipTag))]
        partial struct Job : IJobEntity
        {
            public float dt;
            public float arenaRadius;

            public void Execute(ref Translation translation,
                                ref Rotation rotation,
                                ref Speed speed,
                                ref ShipBoostTank boostTank,
                                in ShipSpeedStats stats,
                                in ShipDesiredActions desiredActions)
            {
                //Rotation
                var oldRotation = rotation.Value;
                var turn        = desiredActions.turn * stats.turnSpeed * dt;
                turn.y          = -turn.y;
                float3 up       = math.mul(oldRotation, new float3(0f, 1f, 0f));
                turn.x          = math.select(turn.x, -turn.x, up.y < 0f);
                var xAxisRot    = quaternion.Euler(turn.y, 0f, 0f);
                var yAxisRot    = quaternion.Euler(0f, turn.x, 0f);
                var newRotation = math.mul(oldRotation, xAxisRot);
                newRotation     = math.mul(yAxisRot, newRotation);
                rotation.Value  = newRotation;

                //Speed
                bool isBoosting = desiredActions.boost && boostTank.boost > 0f;

                speed.speed = Physics.StepVelocityWithInput(desiredActions.gas,
                                                            speed.speed,
                                                            math.select(stats.acceleration, stats.boostAcceleration, isBoosting),
                                                            stats.deceleration,
                                                            math.select(stats.topSpeed, stats.boostSpeed, isBoosting),
                                                            stats.acceleration,
                                                            stats.deceleration,
                                                            stats.reverseSpeed,
                                                            dt);

                //Translation
                translation.Value      += math.forward(newRotation) * speed.speed * dt;
                float distanceToOrigin  = math.length(translation.Value);
                translation.Value       = math.select(translation.Value, arenaRadius / distanceToOrigin * translation.Value, distanceToOrigin > arenaRadius);

                //Boost Tank
                boostTank.boost += math.select(stats.boostRechargeRate, -stats.boostDepleteRate, isBoosting) * dt;
                boostTank.boost  = math.min(boostTank.boost, stats.boostCapacity);
            }
        }
    }
}

