﻿using Latios;
using Latios.Psyshock;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.Profiling;

using static Unity.Entities.SystemAPI;

//The IFindPairsProcessors only force safeToSpawn from true to false.
//Because of this, it is safe to use the Unsafe parallel schedulers.
//However, if the logic is ever modified, this decision needs to be re-evaluated.

namespace Lsss
{
    [RequireMatchingQueriesForUpdate]
    [BurstCompile]
    public partial struct CheckSpawnPointIsSafeSystem : ISystem
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

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new SpawnPointResetFlagsJob().ScheduleParallel();

            var processor = new SpawnPointIsNotSafeProcessor
            {
                safeToSpawnLookup = GetComponentLookup<SafeToSpawn>()
            };

            var closeProcessor = new SpawnPointsAreTooCloseProcessor
            {
                safeToSpawnLookup = processor.safeToSpawnLookup
            };

            var spawnLayer   = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<SpawnPointCollisionLayer>(true).layer;
            state.Dependency = Physics.FindPairs(spawnLayer, closeProcessor).ScheduleParallelUnsafe(state.Dependency);

            var wallLayer    = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<WallCollisionLayer>(true).layer;
            state.Dependency = Physics.FindPairs(spawnLayer, wallLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var bulletLayer  = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<BulletCollisionLayer>(true).layer;
            state.Dependency = Physics.FindPairs(spawnLayer, bulletLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var explosionLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<ExplosionCollisionLayer>(true).layer;
            state.Dependency   = Physics.FindPairs(spawnLayer, explosionLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var wormholeLayer = latiosWorld.sceneBlackboardEntity.GetCollectionComponent<WormholeCollisionLayer>(true).layer;
            state.Dependency  = Physics.FindPairs(spawnLayer, wormholeLayer, processor).ScheduleParallelUnsafe(state.Dependency);

            var factionEntities = QueryBuilder().WithAll<Faction, FactionTag>().Build().ToEntityArray(Allocator.Temp);
            foreach (var entity in factionEntities)
            {
                var shipLayer    = latiosWorld.GetCollectionComponent<FactionShipsCollisionLayer>(entity, true).layer;
                state.Dependency = Physics.FindPairs(spawnLayer, shipLayer, processor).ScheduleParallelUnsafe(state.Dependency);
            }
        }

        [BurstCompile]
        partial struct SpawnPointResetFlagsJob : IJobEntity
        {
            public void Execute(ref SafeToSpawn safeToSpawn) => safeToSpawn.safe = true;
        }

        //Assumes A is SpawnPoint
        struct SpawnPointIsNotSafeProcessor : IFindPairsProcessor
        {
            public PhysicsComponentLookup<SafeToSpawn> safeToSpawnLookup;

            public void Execute(in FindPairsResult result)
            {
                // No need to check narrow phase. AABB check is good enough
                safeToSpawnLookup[result.entityA] = new SafeToSpawn { safe = false };
            }
        }

        struct SpawnPointsAreTooCloseProcessor : IFindPairsProcessor
        {
            public PhysicsComponentLookup<SafeToSpawn> safeToSpawnLookup;

            public void Execute(in FindPairsResult result)
            {
                safeToSpawnLookup[result.entityA] = new SafeToSpawn { safe = false };
                safeToSpawnLookup[result.entityB]                          = new SafeToSpawn { safe = false };
            }
        }
    }
}

