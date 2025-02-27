using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

// This system uses PreviousParent in all cases because it is guaranteed to be updated
// (ParentSystem just ran) and it is updated when the entity is enabled so change filters
// work correctly.
namespace Latios.Systems
{
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(ExtremeParentSystem))]
    [UpdateBefore(typeof(ExtremeLocalToParentSystem))]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct ExtremeChildDepthsSystem : ISystem
    {
        EntityQuery m_query;

        ComponentTypeHandle<PreviousParent> m_previousParentHandle;
        BufferTypeHandle<Child>             m_childHandle;
        ComponentTypeHandle<Depth>          m_depthHandleRW;
        ComponentTypeHandle<Depth>          m_depthHandleRO;
        ComponentTypeHandle<ChunkDepthMask> m_chunkDepthMaskHandle;

        // For a 32-bit depth mask, the upper 16 bits are used as a scratch list if updates are needed.
        const int kMaxDepthIterations = 16;

        public void OnCreate(ref SystemState state)
        {
            m_query = state.Fluent().WithAll<Parent>(true).WithAll<Depth>(false).WithAll<ChunkDepthMask>(false, true).Build();

            m_previousParentHandle = state.GetComponentTypeHandle<PreviousParent>(true);
            m_childHandle          = state.GetBufferTypeHandle<Child>(true);
            m_depthHandleRW        = state.GetComponentTypeHandle<Depth>(false);
            m_depthHandleRO        = state.GetComponentTypeHandle<Depth>(true);
            m_chunkDepthMaskHandle = state.GetComponentTypeHandle<ChunkDepthMask>(false);
        }

        [BurstCompile] public void OnDestroy(ref SystemState state) {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            m_previousParentHandle.Update(ref state);
            m_childHandle.Update(ref state);
            m_depthHandleRW.Update(ref state);
            m_depthHandleRO.Update(ref state);
            m_chunkDepthMaskHandle.Update(ref state);

            state.Dependency = new UpdateDepthsJob
            {
                parentHandle      = m_previousParentHandle,
                parentLookup      = SystemAPI.GetComponentLookup<PreviousParent>(true),
                childHandle       = m_childHandle,
                childLookup       = SystemAPI.GetBufferLookup<Child>(true),
                depthLookup       = SystemAPI.GetComponentLookup<Depth>(false),
                depthHandle       = m_depthHandleRW,
                lastSystemVersion = state.LastSystemVersion
            }.ScheduleParallel(m_query, state.Dependency);

            state.Dependency = new UpdateChunkDepthMasksJob
            {
                depthHandle          = m_depthHandleRO,
                chunkDepthMaskHandle = m_chunkDepthMaskHandle,
                lastSystemVersion    = state.LastSystemVersion
            }.ScheduleParallel(m_query, state.Dependency);
        }

        // The way this job works is for each child with a dirty parent chunk,
        // it walks up its ancestry to see if any ancestor has a dirty parent chunk.
        // If so, the child is skipped as the ancestor will update it.
        // If not, then it is responsible for walking all the way down the hierarchy
        // and updating all depths after capturing the depth from its walk upward.
        // In the case of new hierarchies, all but the first-level children will see
        // a dirty ancestry just one level up and stop walking upwards. This is as
        // efficient as it can get for chunk-granular change tracking.
        //
        // Todo: We could however capture the list of changed entities from ExtremeParentSystem
        // and using either a bit array or a hashset run this algorithm with entity granularity.
        [BurstCompile]
        struct UpdateDepthsJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<PreviousParent>                   parentHandle;
            [ReadOnly] public ComponentLookup<PreviousParent>                       parentLookup;
            [ReadOnly] public BufferTypeHandle<Child>                               childHandle;
            [ReadOnly] public BufferLookup<Child>                                   childLookup;
            [NativeDisableContainerSafetyRestriction] public ComponentLookup<Depth> depthLookup;
            public ComponentTypeHandle<Depth>                                       depthHandle;

            public uint lastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (!chunk.DidChange(parentHandle, lastSystemVersion))
                    return;

                var parents = chunk.GetNativeArray(parentHandle);

                BufferAccessor<Child> childAccess         = default;
                bool                  hasChildrenToUpdate = chunk.Has(childHandle);
                if (hasChildrenToUpdate)
                    childAccess           = chunk.GetBufferAccessor(childHandle);
                NativeArray<Depth> depths = default;

                for (int i = 0; i < chunk.Count; i++)
                {
                    if (IsDepthChangeRoot(parents[i].Value, out var depth))
                    {
                        if (!depths.IsCreated)
                            depths = chunk.GetNativeArray(depthHandle);

                        var startDepth = new Depth { depth = depth };
                        depths[i]                          = startDepth;
                        startDepth.depth++;

                        if (hasChildrenToUpdate)
                        {
                            foreach (var child in childAccess[i])
                            {
                                WriteDepthAndRecurse(child.Value, startDepth);
                            }
                        }
                    }
                }
            }

            bool IsDepthChangeRoot(Entity parent, out byte depth)
            {
                var current = parent;
                depth       = 0;
                while (parentLookup.HasComponent(current))
                {
                    if (parentLookup.DidChange(current, lastSystemVersion))
                    {
                        return false;
                    }
                    depth++;
                    current = parentLookup[current].Value;
                }
                return true;
            }

            void WriteDepthAndRecurse(Entity child, Depth depth)
            {
                depthLookup[child] = depth;
                depth.depth++;
                if (childLookup.HasBuffer(child))
                {
                    foreach (var c in childLookup[child])
                    {
                        WriteDepthAndRecurse(c.Value, depth);
                    }
                }
            }
        }

        [BurstCompile]
        struct UpdateChunkDepthMasksJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<Depth> depthHandle;
            public ComponentTypeHandle<ChunkDepthMask>   chunkDepthMaskHandle;
            public uint                                  lastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                if (chunk.DidChange(depthHandle, lastSystemVersion) || chunk.DidOrderChange(lastSystemVersion))
                {
                    BitField32 depthMask = default;
                    var        depths    = chunk.GetNativeArray(depthHandle);
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        if (depths[i].depth < kMaxDepthIterations)
                            depthMask.SetBits(depths[i].depth, true);
                    }

                    chunk.SetChunkComponentData(chunkDepthMaskHandle, new ChunkDepthMask { chunkDepthMask = depthMask });
                }
            }
        }
    }
}

