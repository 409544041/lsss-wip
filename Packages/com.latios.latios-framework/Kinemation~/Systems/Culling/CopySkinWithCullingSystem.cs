using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;

namespace Latios.Kinemation.Systems
{
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct CopySkinWithCullingSystem : ISystem
    {
        EntityQuery m_metaQuery;

        public void OnCreate(ref SystemState state)
        {
            m_metaQuery = state.Fluent().WithAll<ChunkWorldRenderBounds>(true).WithAll<HybridChunkInfo>(true).WithAll<ChunkHeader>(true).WithAll<ChunkPerFrameCullingMask>(true)
                          .WithAll<ChunkCopySkinShaderData>(true).WithAll<ChunkPerCameraCullingMask>(false).UseWriteGroups().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int linearBlendIndex = state.GetWorldBlackboardEntity().GetBuffer<MaterialPropertyComponentType>(true).Reinterpret<ComponentType>().AsNativeArray()
                                   .IndexOf(ComponentType.ReadOnly<LinearBlendSkinningShaderIndex>());
            ulong linearBlendMaterialMaskLower = (ulong)linearBlendIndex >= 64UL ? 0UL : (1UL << linearBlendIndex);
            ulong linearBlendMaterialMaskUpper = (ulong)linearBlendIndex >= 64UL ? (1UL << (linearBlendIndex - 64)) : 0UL;

            int deformIndex = state.GetWorldBlackboardEntity().GetBuffer<MaterialPropertyComponentType>(true).Reinterpret<ComponentType>().AsNativeArray()
                              .IndexOf(ComponentType.ReadOnly<ComputeDeformShaderIndex>());
            ulong deformMaterialMaskLower = (ulong)deformIndex >= 64UL ? 0UL : (1UL << deformIndex);
            ulong deformMaterialMaskUpper = (ulong)deformIndex >= 64UL ? (1UL << (deformIndex - 64)) : 0UL;

            state.Dependency = new CopySkinJob
            {
                hybridChunkInfoHandle          = state.GetComponentTypeHandle<HybridChunkInfo>(true),
                chunkHeaderHandle              = state.GetComponentTypeHandle<ChunkHeader>(true),
                chunkPerFrameMaskHandle        = state.GetComponentTypeHandle<ChunkPerFrameCullingMask>(true),
                referenceHandle                = state.GetComponentTypeHandle<ShareSkinFromEntity>(true),
                sife                           = state.GetEntityStorageInfoLookup(),
                chunkPerCameraMaskHandle       = state.GetComponentTypeHandle<ChunkPerCameraCullingMask>(false),
                chunkMaterialPropertyDirtyMask = state.GetComponentTypeHandle<ChunkMaterialPropertyDirtyMask>(false),
                computeCdfe                    = state.GetComponentLookup<ComputeDeformShaderIndex>(false),
                linearBlendCdfe                = state.GetComponentLookup<LinearBlendSkinningShaderIndex>(false),
                computeDeformHandle            = state.GetComponentTypeHandle<ComputeDeformShaderIndex>(false),
                linearBlendHandle              = state.GetComponentTypeHandle<LinearBlendSkinningShaderIndex>(false),
                linearBlendMaterialMaskLower   = linearBlendMaterialMaskLower,
                linearBlendMaterialMaskUpper   = linearBlendMaterialMaskUpper,
                deformMaterialMaskLower        = deformMaterialMaskLower,
                deformMaterialMaskUpper        = deformMaterialMaskUpper
            }.ScheduleParallel(m_metaQuery, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) {
        }

        [BurstCompile]
        unsafe struct CopySkinJob : IJobEntityBatch
        {
            [ReadOnly] public ComponentTypeHandle<HybridChunkInfo>          hybridChunkInfoHandle;
            [ReadOnly] public ComponentTypeHandle<ChunkHeader>              chunkHeaderHandle;
            [ReadOnly] public ComponentTypeHandle<ChunkPerFrameCullingMask> chunkPerFrameMaskHandle;
            [ReadOnly] public ComponentTypeHandle<ShareSkinFromEntity>      referenceHandle;

            [ReadOnly] public EntityStorageInfoLookup sife;

            public ComponentTypeHandle<ChunkPerCameraCullingMask>      chunkPerCameraMaskHandle;
            public ComponentTypeHandle<ChunkMaterialPropertyDirtyMask> chunkMaterialPropertyDirtyMask;

            [NativeDisableParallelForRestriction] public ComponentLookup<ComputeDeformShaderIndex>       computeCdfe;
            [NativeDisableParallelForRestriction] public ComponentLookup<LinearBlendSkinningShaderIndex> linearBlendCdfe;

            [NativeDisableContainerSafetyRestriction] public ComponentTypeHandle<ComputeDeformShaderIndex>       computeDeformHandle;
            [NativeDisableContainerSafetyRestriction] public ComponentTypeHandle<LinearBlendSkinningShaderIndex> linearBlendHandle;

            public ulong linearBlendMaterialMaskLower;
            public ulong linearBlendMaterialMaskUpper;
            public ulong deformMaterialMaskLower;
            public ulong deformMaterialMaskUpper;

            public void Execute(ArchetypeChunk archetypeChunk, int chunkIndex)
            {
                var hybridChunkInfos        = archetypeChunk.GetNativeArray(hybridChunkInfoHandle);
                var chunkHeaders            = archetypeChunk.GetNativeArray(chunkHeaderHandle);
                var chunkCameraMasks        = archetypeChunk.GetNativeArray(chunkPerCameraMaskHandle);
                var chunkFrameMasks         = archetypeChunk.GetNativeArray(chunkPerFrameMaskHandle);
                var chunkMaterialDirtyMasks = archetypeChunk.GetNativeArray(chunkMaterialPropertyDirtyMask);

                var context = new MaterialContext();
                context.Init(linearBlendCdfe, computeCdfe, linearBlendHandle, computeDeformHandle);

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
                        context.ResetChunk(chunk);

                        var references                 = chunk.GetNativeArray(referenceHandle);
                        var invertedFrameMasks         = chunkFrameMasks[metaIndex];
                        invertedFrameMasks.lower.Value = ~invertedFrameMasks.lower.Value;
                        invertedFrameMasks.upper.Value = ~invertedFrameMasks.upper.Value;

                        var        lodWord = chunkEntityLodEnabled.Enabled[0];
                        BitField64 maskWordLower;
                        maskWordLower.Value = 0;
                        for (int i = math.tzcnt(lodWord); i < 64; lodWord ^= 1ul << i, i = math.tzcnt(lodWord))
                        {
                            bool isIn = IsReferenceVisible(references[i].sourceSkinnedEntity,
                                                           invertedFrameMasks.lower.IsSet(i),
                                                           i,
                                                           ref context);
                            maskWordLower.Value |= math.select(0ul, 1ul, isIn) << i;
                        }
                        lodWord = chunkEntityLodEnabled.Enabled[1];
                        BitField64 maskWordUpper;
                        maskWordUpper.Value = 0;
                        for (int i = math.tzcnt(lodWord); i < 64; lodWord ^= 1ul << i, i = math.tzcnt(lodWord))
                        {
                            bool isIn = IsReferenceVisible(references[i + 64].sourceSkinnedEntity,
                                                           invertedFrameMasks.upper.IsSet(i),
                                                           i + 64,
                                                           ref context);
                            maskWordUpper.Value |= math.select(0ul, 1ul, isIn) << i;
                        }

                        chunkCameraMasks[metaIndex] = new ChunkPerCameraCullingMask { lower = maskWordLower, upper = maskWordUpper };

                        var dirtyMask = chunkMaterialDirtyMasks[metaIndex];
                        if (context.linearBlendDirty)
                        {
                            dirtyMask.lower.Value |= linearBlendMaterialMaskLower;
                            dirtyMask.upper.Value |= linearBlendMaterialMaskUpper;
                        }
                        if (context.computeDeformDirty)
                        {
                            dirtyMask.lower.Value |= deformMaterialMaskLower;
                            dirtyMask.upper.Value |= deformMaterialMaskUpper;
                        }
                        chunkMaterialDirtyMasks[metaIndex] = dirtyMask;
                    }
                }
            }

            bool IsReferenceVisible(Entity reference, bool needsCopy, int entityIndex, ref MaterialContext context)
            {
                if (reference == Entity.Null || !sife.Exists(reference))
                    return false;

                var  info          = sife[reference];
                var  referenceMask = info.Chunk.GetChunkComponentData(chunkPerCameraMaskHandle);
                bool result;
                if (info.IndexInChunk >= 64)
                    result = referenceMask.upper.IsSet(info.IndexInChunk - 64);
                else
                    result = referenceMask.lower.IsSet(info.IndexInChunk);
                if (result && needsCopy)
                {
                    context.CopySkin(entityIndex,
                                     reference);
                }
                return result;
            }

            struct MaterialContext
            {
                bool                                                    newChunk;
                ArchetypeChunk                                          currentChunk;
                NativeArray<LinearBlendSkinningShaderIndex>             linearBlendChunkArray;
                NativeArray<ComputeDeformShaderIndex>                   computeDeformChunkArray;
                bool                                                    hasLinearBlend;
                bool                                                    hasComputeDeform;
                ComponentTypeHandle<LinearBlendSkinningShaderIndex>     copySkinLinearBlendHandle;
                ComponentLookup<LinearBlendSkinningShaderIndex> referenceLinearBlendCdfe;
                ComponentTypeHandle<ComputeDeformShaderIndex>           copySkinComputeDeformHandle;
                ComponentLookup<ComputeDeformShaderIndex>       referenceComputeDeformCdfe;

                public void Init(ComponentLookup<LinearBlendSkinningShaderIndex> lbsCdfe, ComponentLookup<ComputeDeformShaderIndex> cdsCdfe,
                                 ComponentTypeHandle<LinearBlendSkinningShaderIndex> lbsHandle, ComponentTypeHandle<ComputeDeformShaderIndex> cdsHandle)
                {
                    copySkinLinearBlendHandle   = lbsHandle;
                    referenceLinearBlendCdfe    = lbsCdfe;
                    copySkinComputeDeformHandle = cdsHandle;
                    referenceComputeDeformCdfe  = cdsCdfe;
                }

                public void ResetChunk(ArchetypeChunk chunk)
                {
                    newChunk         = true;
                    hasComputeDeform = false;
                    hasLinearBlend   = false;
                    currentChunk     = chunk;
                }

                public bool linearBlendDirty => hasLinearBlend;
                public bool computeDeformDirty => hasComputeDeform;

                public void CopySkin(int entityIndex, Entity reference)
                {
                    if (Hint.Unlikely(newChunk))
                    {
                        newChunk         = false;
                        hasLinearBlend   = currentChunk.Has(copySkinLinearBlendHandle);
                        hasComputeDeform = currentChunk.Has(copySkinComputeDeformHandle);
                        if (hasLinearBlend)
                            linearBlendChunkArray = currentChunk.GetNativeArray(copySkinLinearBlendHandle);
                        if (hasComputeDeform)
                            computeDeformChunkArray = currentChunk.GetNativeArray(copySkinComputeDeformHandle);
                    }

                    if (hasLinearBlend)
                        linearBlendChunkArray[entityIndex] = referenceLinearBlendCdfe[reference];
                    if (hasComputeDeform)
                        computeDeformChunkArray[entityIndex] = referenceComputeDeformCdfe[reference];
                }
            }
        }
    }
}

