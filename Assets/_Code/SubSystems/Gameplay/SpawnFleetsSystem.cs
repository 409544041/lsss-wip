﻿using Latios;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using Debug = UnityEngine.Debug;
using static Unity.Entities.SystemAPI;

namespace Lsss
{
    [BurstCompile]
    public partial struct SpawnFleetsSystem : ISystem, ISystemShouldUpdate
    {
        private struct NewFleetTag : IComponentData { }

        EntityQuery m_playerQuery;
        EntityQuery m_aiQuery;

        NativeList<Entity> m_entityListCache;

        LatiosWorldUnmanaged latiosWorld;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_entityListCache = new NativeList<Entity>(Allocator.Persistent);

            latiosWorld = state.GetLatiosWorldUnmanaged();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_entityListCache.Dispose();
        }

        public bool ShouldUpdateSystem(ref SystemState state)
        {
            var currentScene = latiosWorld.worldBlackboardEntity.GetComponentData<CurrentScene>();
            return currentScene.isFirstFrame;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_playerQuery = QueryBuilder().WithAll<FactionMember, FleetSpawnSlotTag, FleetSpawnPlayerSlotTag, LocalToWorld>().Build();
            m_aiQuery     = QueryBuilder().WithAll<FactionMember, FleetSpawnSlotTag, LocalToWorld>().WithNone<FleetSpawnPlayerSlotTag>().Build();

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            foreach ((var factionRef, var entity) in Query<RefRO<FleetSpawnSlotFactionReference> >().WithEntityAccess())
            {
                ecb.AddSharedComponent(entity, new FactionMember { factionEntity = factionRef.ValueRO.factionEntity });
            }
            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            var factionEntities = QueryBuilder().WithAll<FactionTag>().WithAllRW<Faction>().Build().ToEntityArray(Allocator.Temp);

            foreach (var entity in factionEntities)
            {
                var factionMember = new FactionMember { factionEntity = entity };
                m_aiQuery.SetSharedComponentFilter(factionMember);
                m_playerQuery.SetSharedComponentFilter(factionMember);
                var faction = GetComponent<Faction>(entity);

                if (faction.playerPrefab != Entity.Null && !m_playerQuery.IsEmpty)
                {
                    var newPlayerShip = state.EntityManager.Instantiate(faction.playerPrefab);
                    AddSharedComponentDataToLinkedGroup(newPlayerShip, factionMember, ref state);
                    SpawnPlayer(newPlayerShip, ref state);
                    faction.remainingReinforcements--;
                }

                {
                    int spawnCount    = m_aiQuery.CalculateEntityCount();
                    var newShipPrefab = state.EntityManager.Instantiate(faction.aiPrefab);
                    state.EntityManager.AddComponent<NewFleetTag>(newShipPrefab);
                    AddSharedComponentDataToLinkedGroup(newShipPrefab, factionMember, ref state);
                    var newShips = state.EntityManager.Instantiate(newShipPrefab, spawnCount, Allocator.TempJob);
                    state.EntityManager.DestroyEntity(newShipPrefab);
                    SpawnAi(newShips, ref state);
                    newShips.Dispose();
                    faction.remainingReinforcements -= spawnCount;
                }

                SetComponent(entity, faction);

                m_aiQuery.ResetFilter();
                m_playerQuery.ResetFilter();
            }

            state.EntityManager.RemoveComponent<NewFleetTag>(m_aiQuery);
        }

        void SpawnPlayer(Entity newPlayerShip, ref SystemState state)
        {
            var ltws                                            = m_playerQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            var ltw                                             = ltws[0];
            var rotation                                        = quaternion.LookRotationSafe(ltw.Forward, ltw.Up);
            SetComponent(newPlayerShip, new Rotation { Value    = rotation });
            SetComponent(newPlayerShip, new Translation { Value = ltw.Position });
        }

        void SpawnAi(NativeArray<Entity> newShips, ref SystemState state)
        {
            var ltws = m_aiQuery.ToComponentDataArray<LocalToWorld>(Allocator.Temp);
            int i    = 0;
            foreach (var ltw in ltws)
            {
                if (i >= newShips.Length)
                    break;

                var ship                                   = newShips[i];
                var rotation                               = quaternion.LookRotationSafe(ltw.Forward, new float3(0f, 1f, 0f));
                SetComponent(ship, new Rotation { Value    = rotation });
                SetComponent(ship, new Translation { Value = ltw.Position });
            }
        }

        void AddSharedComponentDataToLinkedGroup<T>(Entity root, T sharedComponent, ref SystemState state) where T : unmanaged, ISharedComponentData
        {
            m_entityListCache.Clear();
            m_entityListCache.AddRange(GetBuffer<LinkedEntityGroup>(root).Reinterpret<Entity>().AsNativeArray());
            state.EntityManager.AddSharedComponent(m_entityListCache.AsArray(), sharedComponent);
        }
    }
}

