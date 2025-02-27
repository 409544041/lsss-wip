using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

// Todo: If this gets slow, try sweeping through the culled skeleton buffers
// and a chunk index chunkComponent from the skinned meshes to write to a per-thread
// mask buffer.
namespace Latios.Kinemation.Systems
{
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct FrustumCullSkinnedEntitiesSystem : ISystem
    {
        EntityQuery m_metaQuery;

        public void OnCreate(ref SystemState state)
        {
            m_metaQuery = state.Fluent().WithAll<ChunkWorldRenderBounds>(true).WithAll<HybridChunkInfo>(true).WithAll<ChunkHeader>(true).WithAll<ChunkPerFrameCullingMask>(true)
                          .WithAny<ChunkComputeDeformMemoryMetadata>(true).WithAny<ChunkLinearBlendSkinningMemoryMetadata>(true).WithAll<ChunkPerCameraCullingMask>(false)
                          .UseWriteGroups().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new SkinnedCullingJob
            {
                hybridChunkInfoHandle   = state.GetComponentTypeHandle<HybridChunkInfo>(true),
                chunkHeaderHandle       = state.GetComponentTypeHandle<ChunkHeader>(true),
                dependentHandle         = state.GetComponentTypeHandle<SkeletonDependent>(true),
                chunkSkeletonMaskHandle = state.GetComponentTypeHandle<ChunkPerCameraSkeletonCullingMask>(true),
                sife                    = state.GetEntityStorageInfoLookup(),
                chunkMaskHandle         = state.GetComponentTypeHandle<ChunkPerCameraCullingMask>(false)
            }.ScheduleParallel(m_metaQuery, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }

        [BurstCompile]
        unsafe struct SkinnedCullingJob : IJobEntityBatch
        {
            [ReadOnly] public ComponentTypeHandle<HybridChunkInfo>                   hybridChunkInfoHandle;
            [ReadOnly] public ComponentTypeHandle<ChunkHeader>                       chunkHeaderHandle;
            [ReadOnly] public ComponentTypeHandle<SkeletonDependent>                 dependentHandle;
            [ReadOnly] public ComponentTypeHandle<ChunkPerCameraSkeletonCullingMask> chunkSkeletonMaskHandle;

            [ReadOnly] public EntityStorageInfoLookup sife;

            public ComponentTypeHandle<ChunkPerCameraCullingMask> chunkMaskHandle;

            public void Execute(ArchetypeChunk archetypeChunk, int chunkIndex)
            {
                var hybridChunkInfos = archetypeChunk.GetNativeArray(hybridChunkInfoHandle);
                var chunkHeaders     = archetypeChunk.GetNativeArray(chunkHeaderHandle);
                var chunkMasks       = archetypeChunk.GetNativeArray(chunkMaskHandle);

                for (var metaIndex = 0; metaIndex < archetypeChunk.Count; metaIndex++)
                {
                    var hybridChunkInfo = hybridChunkInfos[metaIndex];
                    if (!hybridChunkInfo.Valid)
                        continue;

                    var chunkHeader = chunkHeaders[metaIndex];

                    ref var chunkCullingData = ref hybridChunkInfo.CullingData;

                    var chunkInstanceCount    = chunkHeader.ArchetypeChunk.Count;
                    var chunkEntityLodEnabled = chunkCullingData.InstanceLodEnableds;
                    var anyLodEnabled         = (chunkEntityLodEnabled.Enabled[0] | chunkEntityLodEnabled.Enabled[1]) != 0;

                    if (anyLodEnabled)
                    {
                        // Todo: Throw error if not per-instance?
                        //var perInstanceCull = 0 != (chunkCullingData.Flags & HybridChunkCullingData.kFlagInstanceCulling);

                        var chunk = chunkHeader.ArchetypeChunk;

                        if (!chunk.Has(dependentHandle))
                            continue;

                        var rootRefs = chunk.GetNativeArray(dependentHandle);

                        var        lodWord = chunkEntityLodEnabled.Enabled[0];
                        BitField64 maskWordLower;
                        maskWordLower.Value = 0;
                        for (int i = math.tzcnt(lodWord); i < 64; lodWord ^= 1ul << i, i = math.tzcnt(lodWord))
                        {
                            bool isIn            = IsSkeletonVisible(rootRefs[i].root);
                            maskWordLower.Value |= math.select(0ul, 1ul, isIn) << i;
                        }
                        lodWord = chunkEntityLodEnabled.Enabled[1];
                        BitField64 maskWordUpper;
                        maskWordUpper.Value = 0;
                        for (int i = math.tzcnt(lodWord); i < 64; lodWord ^= 1ul << i, i = math.tzcnt(lodWord))
                        {
                            bool isIn            = IsSkeletonVisible(rootRefs[i + 64].root);
                            maskWordUpper.Value |= math.select(0ul, 1ul, isIn) << i;
                        }

                        chunkMasks[metaIndex] = new ChunkPerCameraCullingMask { lower = maskWordLower, upper = maskWordUpper };
                    }
                }
            }

            bool IsSkeletonVisible(Entity root)
            {
                if (root == Entity.Null || !sife.Exists(root))
                    return false;

                var info         = sife[root];
                var skeletonMask = info.Chunk.GetChunkComponentData(chunkSkeletonMaskHandle);
                if (info.IndexInChunk >= 64)
                    return skeletonMask.upper.IsSet(info.IndexInChunk - 64);
                else
                    return skeletonMask.lower.IsSet(info.IndexInChunk);
            }
        }
    }
}

