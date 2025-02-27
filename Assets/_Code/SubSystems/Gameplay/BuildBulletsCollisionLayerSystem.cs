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
    public partial struct BuildBulletsCollisionLayerSystem : ISystem, ISystemNewScene
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

        public void OnNewScene(ref SystemState state) => latiosWorld.sceneBlackboardEntity.AddOrSetCollectionComponentAndDisposeOld(new BulletCollisionLayer());

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            CollisionLayerSettings settings;
            if (latiosWorld.sceneBlackboardEntity.HasComponent<ArenaCollisionSettings>())
                settings = latiosWorld.sceneBlackboardEntity.GetComponentData<ArenaCollisionSettings>().settings;
            else
                settings = BuildCollisionLayerConfig.defaultSettings;

            var query = QueryBuilder().WithAll<Translation, Rotation, Collider, BulletPreviousPosition, BulletTag>().Build();

            var bodies =
                CollectionHelper.CreateNativeArray<ColliderBody>(query.CalculateEntityCount(),
                                                                 state.WorldUnmanaged.UpdateAllocator.ToAllocator,
                                                                 NativeArrayOptions.UninitializedMemory);

            new Job { bodies = bodies }.ScheduleParallel(query);

            state.Dependency = Physics.BuildCollisionLayer(bodies).WithSettings(settings).ScheduleParallel(out CollisionLayer layer, Allocator.Persistent, state.Dependency);
            var bcl          = new BulletCollisionLayer { layer = layer };
            latiosWorld.sceneBlackboardEntity.SetCollectionComponentAndDisposeOld(bcl);
        }

        [BurstCompile]
        partial struct Job : IJobEntity
        {
            public NativeArray<ColliderBody> bodies;

            public void Execute(Entity entity,
                                [EntityInQueryIndex] int entityInQueryIndex,
                                in Translation translation,
                                in Rotation rotation,
                                in Collider collider,
                                in BulletPreviousPosition previousPosition)
            {
                CapsuleCollider capsule     = collider;
                float           tailLength  = math.distance(translation.Value, previousPosition.previousPosition);
                capsule.pointA              = capsule.pointB;
                capsule.pointA.z           -= math.max(tailLength, 1.1920928955078125e-7f);  //Todo: Use math version of this once released.

                bodies[entityInQueryIndex] = new ColliderBody
                {
                    collider  = capsule,
                    entity    = entity,
                    transform = new RigidTransform(rotation.Value, translation.Value)
                };
            }
        }
    }

    public partial class DebugDrawBulletCollisionLayersSystem : SubSystem
    {
        protected override void OnUpdate()
        {
            var layer = sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            CompleteDependency();
            PhysicsDebug.DrawLayer(layer).Run();
            //UnityEngine.Debug.Log("Bullets in layer: " + layer.Count);
        }
    }
}

