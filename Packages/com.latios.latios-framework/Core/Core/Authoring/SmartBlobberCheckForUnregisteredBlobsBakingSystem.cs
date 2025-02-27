using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Latios.Authoring.Systems
{
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(SmartBlobberCleanupBakingGroup), OrderLast = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [BurstCompile]
    public partial struct SmartBlobberCheckForUnregisteredBlobsBakingSystem : ISystem
    {
        EntityQuery                                         m_query;
        ComponentTypeHandle<SmartBlobberTrackingData>       m_trackingDataHandle;
        ComponentTypeHandle<SmartBlobberResult>             m_resultHandle;
        SharedComponentTypeHandle<SmartBlobberBlobTypeHash> m_hashHandle;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_query = new EntityQueryBuilder(Allocator.Temp).WithAllRW<SmartBlobberResult>().WithOptions(EntityQueryOptions.IncludePrefab).Build(ref state);
            state.RequireForUpdate(m_query);

            m_trackingDataHandle = state.GetComponentTypeHandle<SmartBlobberTrackingData>(true);
            m_resultHandle       = state.GetComponentTypeHandle<SmartBlobberResult>(false);
            m_hashHandle         = state.GetSharedComponentTypeHandle<SmartBlobberBlobTypeHash>();
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_trackingDataHandle.Update(ref state);
            m_resultHandle.Update(ref state);
            m_hashHandle.Update(ref state);

            state.Dependency = new Job
            {
                hashHandle         = m_hashHandle,
                resultHandle       = m_resultHandle,
                trackingDataHandle = m_trackingDataHandle
            }.ScheduleParallel(m_query, state.Dependency);
        }

        [BurstCompile]
        struct Job : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<SmartBlobberTrackingData>       trackingDataHandle;
            public ComponentTypeHandle<SmartBlobberResult>                        resultHandle;
            [ReadOnly] public SharedComponentTypeHandle<SmartBlobberBlobTypeHash> hashHandle;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!chunk.Has(trackingDataHandle))
                {
                    UnityEngine.Debug.LogError(
                        "A SmartBlobberResult was detected without a tracking handle. Do not add SmartBlobberResult manually. Instead, use the RequestCreateBlobAsset API to have it added.");
                    return;
                }

                if (!chunk.Has(hashHandle))
                {
                    UnityEngine.Debug.LogError("Where did the SmartBlobberBlobTypeHash go?");
                }

                var trackingDataArray = chunk.GetNativeArray(trackingDataHandle);
                if (trackingDataArray[0].isFinalized)
                    return;

                var hash = chunk.GetSharedComponent(hashHandle).hash;

                UnityEngine.Debug.LogError(
                    $"A SmartBlobberResult was detected that was not post-processed. Please ensure to register the Smart Blobber blob type with SmartBlobberTools.Register(). The offending blobs will be disposed. Blob type hash: BurstRuntime.GetHashCode64() = {hash}.");

                var resultArray = chunk.GetNativeArray(resultHandle);
                for (int i = 0; i < chunk.ChunkEntityCount; i++)
                {
                    if (trackingDataArray[i].isNull)
                        continue;

                    resultArray[i].blob.Dispose();
                    resultArray[i] = default;
                }
            }
        }
    }
}

