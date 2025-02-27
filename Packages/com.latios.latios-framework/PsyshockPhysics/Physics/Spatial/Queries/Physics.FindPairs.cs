﻿using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

//Todo: Stream types, caches, scratchlists, and inflations
namespace Latios.Psyshock
{
    /// <summary>
    /// An interface whose Execute method is invoked for each pair found in a FindPairs operations.
    /// </summary>
    public interface IFindPairsProcessor
    {
        void Execute(in FindPairsResult result);
    }

    [NativeContainer]
    public struct FindPairsResult
    {
        public CollisionLayer layerA => m_layerA;
        public CollisionLayer layerB => m_layerB;
        public ColliderBody bodyA => m_layerA.bodies[indexA];
        public ColliderBody bodyB => m_layerB.bodies[indexB];
        public Collider colliderA => bodyA.collider;
        public Collider colliderB => bodyB.collider;
        public RigidTransform transformA => bodyA.transform;
        public RigidTransform transformB => bodyB.transform;
        public int indexA => m_bodyAIndex;
        public int indexB => m_bodyBIndex;
        public int jobIndex => m_jobIndex;

        private CollisionLayer m_layerA;
        private CollisionLayer m_layerB;
        private int            m_bucketStartA;
        private int            m_bucketStartB;
        private int            m_bodyAIndex;
        private int            m_bodyBIndex;
        private int            m_jobIndex;
        private bool           m_isThreadSafe;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public SafeEntity entityA => new SafeEntity
        {
            entity = new Entity
            {
                Index   = math.select(-bodyA.entity.Index, bodyA.entity.Index, m_isThreadSafe),
                Version = bodyA.entity.Version
            }
        };
        public SafeEntity entityB => new SafeEntity
        {
            entity = new Entity
            {
                Index   = math.select(-bodyB.entity.Index, bodyB.entity.Index, m_isThreadSafe),
                Version = bodyB.entity.Version
            }
        };
#else
        public SafeEntity entityA => new SafeEntity { entity = bodyA.entity };
        public SafeEntity entityB => new SafeEntity { entity = bodyB.entity };
#endif

        public Aabb aabbA
        {
            get {
                var yzminmax = m_layerA.yzminmaxs[m_bodyAIndex];
                var xmin     = m_layerA.xmins[    m_bodyAIndex];
                var xmax     = m_layerA.xmaxs[    m_bodyAIndex];
                return new Aabb(new float3(xmin, yzminmax.xy), new float3(xmax, -yzminmax.zw));
            }
        }

        public Aabb aabbB
        {
            get
            {
                var yzminmax = m_layerB.yzminmaxs[m_bodyBIndex];
                var xmin     = m_layerB.xmins[    m_bodyBIndex];
                var xmax     = m_layerB.xmaxs[    m_bodyBIndex];
                return new Aabb(new float3(xmin, yzminmax.xy), new float3(xmax, -yzminmax.zw));
            }
        }

        internal FindPairsResult(CollisionLayer layerA, CollisionLayer layerB, BucketSlices bucketA, BucketSlices bucketB, int jobIndex, bool isThreadSafe)
        {
            m_layerA       = layerA;
            m_layerB       = layerB;
            m_bucketStartA = bucketA.bucketGlobalStart;
            m_bucketStartB = bucketB.bucketGlobalStart;
            m_jobIndex     = jobIndex;
            m_isThreadSafe = isThreadSafe;
            m_bodyAIndex   = 0;
            m_bodyBIndex   = 0;
        }

        internal static FindPairsResult CreateGlobalResult(CollisionLayer layerA, CollisionLayer layerB, int jobIndex, bool isThreadSafe)
        {
            return new FindPairsResult
            {
                m_layerA       = layerA,
                m_layerB       = layerB,
                m_bucketStartA = 0,
                m_bucketStartB = 0,
                m_jobIndex     = jobIndex,
                m_isThreadSafe = isThreadSafe,
                m_bodyAIndex   = 0,
                m_bodyBIndex   = 0,
            };
        }

        internal void SetBucketRelativePairIndices(int aIndex, int bIndex)
        {
            m_bodyAIndex = aIndex + m_bucketStartA;
            m_bodyBIndex = bIndex + m_bucketStartB;
        }
        //Todo: Shorthands for calling narrow phase distance and manifold queries
    }

    public static partial class Physics
    {
        /// <summary>
        /// Request a FindPairs broadphase operation to report pairs within the layer.
        /// This is the start of a fluent expression.
        /// </summary>
        /// <param name="layer">The layer in which pairs should be detected</param>
        /// <param name="processor">The job-like struct which should process each pair found</param>
        public static FindPairsLayerSelfConfig<T> FindPairs<T>(in CollisionLayer layer, in T processor) where T : struct, IFindPairsProcessor
        {
            return new FindPairsLayerSelfConfig<T>
            {
                processor                = processor,
                layer                    = layer,
                disableEntityAliasChecks = false
            };
        }

        /// <summary>
        /// Request a FindPairs broadphase operation to report pairs between the two layers.
        /// Only pairs containing one element from layerA and one element from layerB will be reported.
        /// This is the start of a fluent expression.
        /// </summary>
        /// <param name="layerA">The first layer in which pairs should be detected</param>
        /// <param name="layerB">The second layer in which pairs should be detected</param>
        /// <param name="processor">The job-like struct which should process each pair found</param>
        public static FindPairsLayerLayerConfig<T> FindPairs<T>(in CollisionLayer layerA, in CollisionLayer layerB, in T processor) where T : struct, IFindPairsProcessor
        {
            CheckLayersAreCompatible(layerA, layerB);
            return new FindPairsLayerLayerConfig<T>
            {
                processor                = processor,
                layerA                   = layerA,
                layerB                   = layerB,
                disableEntityAliasChecks = false
            };
        }

        /// <summary>
        /// Request a FindPairs broadphase operation to report pairs within the layer.
        /// This is the start of a fluent expression.
        /// </summary>
        /// <param name="layer">The layer in which pairs should be detected</param>
        /// <param name="processor">The job-like struct which should process each pair found</param>
        internal static FindPairsLayerSelfConfigUnrolled<T> FindPairsUnrolled<T>(in CollisionLayer layer, in T processor) where T : struct, IFindPairsProcessor
        {
            return new FindPairsLayerSelfConfigUnrolled<T>
            {
                processor                = processor,
                layer                    = layer,
                disableEntityAliasChecks = false
            };
        }

        /// <summary>
        /// Request a FindPairs broadphase operation to report pairs between the two layers.
        /// Only pairs containing one element from layerA and one element from layerB will be reported.
        /// This is the start of a fluent expression.
        /// </summary>
        /// <param name="layerA">The first layer in which pairs should be detected</param>
        /// <param name="layerB">The second layer in which pairs should be detected</param>
        /// <param name="processor">The job-like struct which should process each pair found</param>
        internal static FindPairsLayerLayerConfigUnrolled<T> FindPairsUnrolled<T>(in CollisionLayer layerA, in CollisionLayer layerB, in T processor) where T : struct,
        IFindPairsProcessor
        {
            CheckLayersAreCompatible(layerA, layerB);
            return new FindPairsLayerLayerConfigUnrolled<T>
            {
                processor                = processor,
                layerA                   = layerA,
                layerB                   = layerB,
                disableEntityAliasChecks = false
            };
        }

        #region SafetyChecks
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void CheckLayersAreCompatible(in CollisionLayer layerA, in CollisionLayer layerB)
        {
            if (math.any(layerA.worldMin != layerB.worldMin | layerA.worldAxisStride != layerB.worldAxisStride | layerA.worldSubdivisionsPerAxis !=
                         layerB.worldSubdivisionsPerAxis))
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                throw new InvalidOperationException(
                    "The two layers used in the FindPairs operation are not compatible. Please ensure the layers were constructed with identical settings.");
#endif
            }
        }
        #endregion
    }

    public partial struct FindPairsLayerSelfConfig<T> where T : struct, IFindPairsProcessor
    {
        internal T processor;

        internal CollisionLayer layer;

        internal bool disableEntityAliasChecks;

        #region Settings
        /// <summary>
        /// Disables entity aliasing checks on parallel jobs when safety checks are enabled. Use this only when entities can be aliased but body indices must be thread-safe.
        /// </summary>
        public FindPairsLayerSelfConfig<T> WithoutEntityAliasingChecks()
        {
            disableEntityAliasChecks = true;
            return this;
        }

        /// <summary>
        /// Enables usage of a cache for pairs involving the cross bucket.
        /// This increases processing time and memory usage, but may decrease latency.
        /// </summary>
        /// <param name="cacheAllocator">The type of allocator to use for the cache</param>
        public FindPairsLayerSelfWithCrossCacheConfig<T> WithCrossCache(Allocator cacheAllocator = Allocator.TempJob)
        {
            return new FindPairsLayerSelfWithCrossCacheConfig<T>
            {
                layer                    = layer,
                disableEntityAliasChecks = disableEntityAliasChecks,
                processor                = processor,
                allocator                = cacheAllocator
            };
        }
        #endregion

        #region Schedulers
        /// <summary>
        /// Run the FindPairs operation without using a job. This method can be invoked from inside a job.
        /// </summary>
        public void RunImmediate()
        {
            FindPairsInternal.RunImmediate(layer, processor);
        }

        /// <summary>
        /// Run the FindPairs operation on the main thread using a Bursted job.
        /// </summary>
        public void Run()
        {
            new FindPairsInternal.LayerSelfSingle
            {
                layer     = layer,
                processor = processor
            }.Run();
        }

        /// <summary>
        /// Run the FindPairs operation on a single worker thread.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>A JobHandle for the scheduled job</returns>
        public JobHandle ScheduleSingle(JobHandle inputDeps = default)
        {
            return new FindPairsInternal.LayerSelfSingle
            {
                layer     = layer,
                processor = processor
            }.Schedule(inputDeps);
        }

        /// <summary>
        /// Run the FindPairs operation using multiple worker threads in multiple phases.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>The final JobHandle for the scheduled jobs</returns>
        public JobHandle ScheduleParallel(JobHandle inputDeps = default)
        {
            JobHandle jh = new FindPairsInternal.LayerSelfPart1
            {
                layer     = layer,
                processor = processor
            }.Schedule(layer.BucketCount, 1, inputDeps);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (disableEntityAliasChecks)
            {
                jh = new FindPairsInternal.LayerSelfPart2
                {
                    layer     = layer,
                    processor = processor
                }.Schedule(jh);
            }
            else
            {
                jh = new FindPairsInternal.LayerSelfPart2_WithSafety
                {
                    layer     = layer,
                    processor = processor
                }.ScheduleParallel(2, 1, jh);
            }
#else
            jh = new FindPairsInternal.LayerSelfPart2
            {
                layer     = layer,
                processor = processor
            }.Schedule(jh);
#endif
            return jh;
        }

        /// <summary>
        /// Run the FindPairs operation using multiple worker threads all at once without entity or body index thread-safety.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>A JobHandle for the scheduled job</returns>
        public JobHandle ScheduleParallelUnsafe(JobHandle inputDeps = default)
        {
            return new FindPairsInternal.LayerSelfParallelUnsafe
            {
                layer     = layer,
                processor = processor
            }.ScheduleParallel(2 * layer.BucketCount - 1, 1, inputDeps);
        }
        #endregion Schedulers
    }

    public partial struct FindPairsLayerSelfWithCrossCacheConfig<T>
    {
        internal T processor;

        internal CollisionLayer layer;

        internal bool disableEntityAliasChecks;

        internal Allocator allocator;

        #region Settings
        /// <summary>
        /// Disables entity aliasing checks on parallel jobs when safety checks are enabled. Use this only when entities can be aliased but body indices must be thread-safe.
        /// </summary>
        public FindPairsLayerSelfWithCrossCacheConfig<T> WithoutEntityAliasingChecks()
        {
            disableEntityAliasChecks = true;
            return this;
        }
        #endregion

        #region Schedulers
        /// <summary>
        /// Run the FindPairs operation using multiple worker threads in multiple phases.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>The final JobHandle for the scheduled jobs</returns>
        public JobHandle ScheduleParallel(JobHandle inputDeps = default)
        {
            var       cache = new NativeStream(layer.BucketCount - 1, allocator);
            JobHandle jh    = new FindPairsInternal.LayerSelfPart1
            {
                layer     = layer,
                processor = processor,
                cache     = cache.AsWriter()
            }.Schedule(2 * layer.BucketCount - 1, 1, inputDeps);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (disableEntityAliasChecks)
            {
                jh = new FindPairsInternal.LayerSelfPart2
                {
                    layer     = layer,
                    processor = processor,
                    cache     = cache.AsReader()
                }.Schedule(jh);
            }
            else
            {
                jh = new FindPairsInternal.LayerSelfPart2_WithSafety
                {
                    layer     = layer,
                    processor = processor,
                    cache     = cache.AsReader()
                }.ScheduleParallel(2, 1, jh);
            }
#else
            jh = new FindPairsInternal.LayerSelfPart2
            {
                layer     = layer,
                processor = processor,
                cache     = cache.AsReader()
            }.Schedule(jh);
#endif
            jh = cache.Dispose(jh);
            return jh;
        }
        #endregion
    }

    public partial struct FindPairsLayerLayerConfig<T> where T : struct, IFindPairsProcessor
    {
        internal T processor;

        internal CollisionLayer layerA;
        internal CollisionLayer layerB;

        internal bool disableEntityAliasChecks;

        #region Settings
        /// <summary>
        /// Disables entity aliasing checks on parallel jobs when safety checks are enabled. Use this only when entities can be aliased but body indices must be thread-safe.
        /// </summary>
        public FindPairsLayerLayerConfig<T> WithoutEntityAliasingChecks()
        {
            disableEntityAliasChecks = true;
            return this;
        }

        /// <summary>
        /// Enables usage of a cache for pairs involving the cross bucket.
        /// This increases processing time and memory usage, but may decrease latency.
        /// </summary>
        /// <param name="cacheAllocator">The type of allocator to use for the cache</param>
        public FindPairsLayerLayerWithCrossCacheConfig<T> WithCrossCache(Allocator cacheAllocator = Allocator.TempJob)
        {
            return new FindPairsLayerLayerWithCrossCacheConfig<T>
            {
                layerA                   = layerA,
                layerB                   = layerB,
                disableEntityAliasChecks = disableEntityAliasChecks,
                processor                = processor,
                allocator                = cacheAllocator
            };
        }
        #endregion

        #region Schedulers
        /// <summary>
        /// Run the FindPairs operation without using a job. This method can be invoked from inside a job.
        /// </summary>
        public void RunImmediate()
        {
            FindPairsInternal.RunImmediate(layerA, layerB, processor);
        }

        /// <summary>
        /// Run the FindPairs operation on the main thread using a Bursted job.
        /// </summary>
        public void Run()
        {
            new FindPairsInternal.LayerLayerSingle
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor
            }.Run();
        }

        /// <summary>
        /// Run the FindPairs operation on a single worker thread.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>A JobHandle for the scheduled job</returns>
        public JobHandle ScheduleSingle(JobHandle inputDeps = default)
        {
            return new FindPairsInternal.LayerLayerSingle
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor
            }.Schedule(inputDeps);
        }

        /// <summary>
        /// Run the FindPairs operation using multiple worker threads in multiple phases.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>The final JobHandle for the scheduled jobs</returns>
        public JobHandle ScheduleParallel(JobHandle inputDeps = default)
        {
            JobHandle jh = new FindPairsInternal.LayerLayerPart1
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor
            }.Schedule(layerB.BucketCount, 1, inputDeps);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (disableEntityAliasChecks)
            {
                jh = new FindPairsInternal.LayerLayerPart2
                {
                    layerA    = layerA,
                    layerB    = layerB,
                    processor = processor
                }.Schedule(2, 1, jh);
            }
            else
            {
                jh = new FindPairsInternal.LayerLayerPart2_WithSafety
                {
                    layerA    = layerA,
                    layerB    = layerB,
                    processor = processor
                }.Schedule(3, 1, jh);
            }
#else
            jh = new FindPairsInternal.LayerLayerPart2
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor
            }.Schedule(2, 1, jh);
#endif
            return jh;
        }

        /// <summary>
        /// Run the FindPairs operation using multiple worker threads all at once without entity or body index thread-safety.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>A JobHandle for the scheduled job</returns>
        public JobHandle ScheduleParallelUnsafe(JobHandle inputDeps = default)
        {
            return new FindPairsInternal.LayerLayerParallelUnsafe
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor
            }.ScheduleParallel(3 * layerA.BucketCount - 2, 1, inputDeps);
        }
        #endregion Schedulers
    }

    public partial struct FindPairsLayerLayerWithCrossCacheConfig<T> where T : struct, IFindPairsProcessor
    {
        internal T processor;

        internal CollisionLayer layerA;
        internal CollisionLayer layerB;

        internal bool disableEntityAliasChecks;

        internal Allocator allocator;

        #region Settings
        /// <summary>
        /// Disables entity aliasing checks on parallel jobs when safety checks are enabled. Use this only when entities can be aliased but body indices must be thread-safe.
        /// </summary>
        public FindPairsLayerLayerWithCrossCacheConfig<T> WithoutEntityAliasingChecks()
        {
            disableEntityAliasChecks = true;
            return this;
        }
        #endregion

        #region Schedulers
        /// <summary>
        /// Run the FindPairs operation using multiple worker threads in multiple phases.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>The final JobHandle for the scheduled jobs</returns>
        public JobHandle ScheduleParallel(JobHandle inputDeps = default)
        {
            var cache = new NativeStream(layerA.BucketCount * 2 - 2, allocator);

            JobHandle jh = new FindPairsInternal.LayerLayerPart1
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor,
                cache     = cache.AsWriter()
            }.Schedule(3 * layerB.BucketCount - 2, 1, inputDeps);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (disableEntityAliasChecks)
            {
                jh = new FindPairsInternal.LayerLayerPart2
                {
                    layerA    = layerA,
                    layerB    = layerB,
                    processor = processor,
                    cache     = cache.AsReader()
                }.Schedule(2, 1, jh);
            }
            else
            {
                jh = new FindPairsInternal.LayerLayerPart2_WithSafety
                {
                    layerA    = layerA,
                    layerB    = layerB,
                    processor = processor,
                    cache     = cache.AsReader()
                }.Schedule(3, 1, jh);
            }
#else
            jh = new FindPairsInternal.LayerLayerPart2
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor,
                cache     = cache.AsReader()
            }.Schedule(2, 1, jh);
#endif
            jh = cache.Dispose(jh);
            return jh;
        }
        #endregion
    }

    internal partial struct FindPairsLayerSelfConfigUnrolled<T> where T : struct, IFindPairsProcessor
    {
        internal T processor;

        internal CollisionLayer layer;

        internal bool disableEntityAliasChecks;

        #region Settings
        /// <summary>
        /// Disables entity aliasing checks on parallel jobs when safety checks are enabled. Use this only when entities can be aliased but body indices must be thread-safe.
        /// </summary>
        public FindPairsLayerSelfConfigUnrolled<T> WithoutEntityAliasingChecks()
        {
            disableEntityAliasChecks = true;
            return this;
        }
        #endregion

        #region Schedulers
        /// <summary>
        /// Run the FindPairs operation without using a job. This method can be invoked from inside a job.
        /// </summary>
        public void RunImmediate()
        {
            FindPairsInternalUnrolled.RunImmediate(layer, processor);
        }

        /// <summary>
        /// Run the FindPairs operation on the main thread using a Bursted job.
        /// </summary>
        public void Run()
        {
            new FindPairsInternalUnrolled.LayerSelfSingle
            {
                layer     = layer,
                processor = processor
            }.Run();
        }

        /// <summary>
        /// Run the FindPairs operation on a single worker thread.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>A JobHandle for the scheduled job</returns>
        public JobHandle ScheduleSingle(JobHandle inputDeps = default)
        {
            return new FindPairsInternalUnrolled.LayerSelfSingle
            {
                layer     = layer,
                processor = processor
            }.Schedule(inputDeps);
        }

        /// <summary>
        /// Run the FindPairs operation using multiple worker threads in multiple phases.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>The final JobHandle for the scheduled jobs</returns>
        public JobHandle ScheduleParallel(JobHandle inputDeps = default)
        {
            JobHandle jh = new FindPairsInternalUnrolled.LayerSelfPart1
            {
                layer     = layer,
                processor = processor
            }.Schedule(layer.BucketCount, 1, inputDeps);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (disableEntityAliasChecks)
            {
                jh = new FindPairsInternalUnrolled.LayerSelfPart2
                {
                    layer     = layer,
                    processor = processor
                }.Schedule(jh);
            }
            else
            {
                jh = new FindPairsInternalUnrolled.LayerSelfPart2_WithSafety
                {
                    layer     = layer,
                    processor = processor
                }.ScheduleParallel(2, 1, jh);
            }
#else
            jh = new FindPairsInternalUnrolled.LayerSelfPart2
            {
                layer     = layer,
                processor = processor
            }.Schedule(jh);
#endif
            return jh;
        }

        /// <summary>
        /// Run the FindPairs operation using multiple worker threads all at once without entity or body index thread-safety.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>A JobHandle for the scheduled job</returns>
        public JobHandle ScheduleParallelUnsafe(JobHandle inputDeps = default)
        {
            return new FindPairsInternalUnrolled.LayerSelfParallelUnsafe
            {
                layer     = layer,
                processor = processor
            }.ScheduleParallel(2 * layer.BucketCount - 1, 1, inputDeps);
        }
        #endregion Schedulers
    }

    internal partial struct FindPairsLayerLayerConfigUnrolled<T> where T : struct, IFindPairsProcessor
    {
        internal T processor;

        internal CollisionLayer layerA;
        internal CollisionLayer layerB;

        internal bool disableEntityAliasChecks;

        #region Settings
        /// <summary>
        /// Disables entity aliasing checks on parallel jobs when safety checks are enabled. Use this only when entities can be aliased but body indices must be thread-safe.
        /// </summary>
        public FindPairsLayerLayerConfigUnrolled<T> WithoutEntityAliasingChecks()
        {
            disableEntityAliasChecks = true;
            return this;
        }
        #endregion

        #region Schedulers
        /// <summary>
        /// Run the FindPairs operation without using a job. This method can be invoked from inside a job.
        /// </summary>
        public void RunImmediate()
        {
            FindPairsInternalUnrolled.RunImmediate(layerA, layerB, processor);
        }

        /// <summary>
        /// Run the FindPairs operation on the main thread using a Bursted job.
        /// </summary>
        public void Run()
        {
            new FindPairsInternalUnrolled.LayerLayerSingle
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor
            }.Run();
        }

        /// <summary>
        /// Run the FindPairs operation on a single worker thread.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>A JobHandle for the scheduled job</returns>
        public JobHandle ScheduleSingle(JobHandle inputDeps = default)
        {
            return new FindPairsInternalUnrolled.LayerLayerSingle
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor
            }.Schedule(inputDeps);
        }

        /// <summary>
        /// Run the FindPairs operation using multiple worker threads in multiple phases.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>The final JobHandle for the scheduled jobs</returns>
        public JobHandle ScheduleParallel(JobHandle inputDeps = default)
        {
            JobHandle jh = new FindPairsInternalUnrolled.LayerLayerPart1
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor
            }.Schedule(layerB.BucketCount, 1, inputDeps);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            if (disableEntityAliasChecks)
            {
                jh = new FindPairsInternalUnrolled.LayerLayerPart2
                {
                    layerA    = layerA,
                    layerB    = layerB,
                    processor = processor
                }.Schedule(2, 1, jh);
            }
            else
            {
                jh = new FindPairsInternalUnrolled.LayerLayerPart2_WithSafety
                {
                    layerA    = layerA,
                    layerB    = layerB,
                    processor = processor
                }.Schedule(3, 1, jh);
            }
#else
            jh = new FindPairsInternalUnrolled.LayerLayerPart2
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor
            }.Schedule(2, 1, jh);
#endif
            return jh;
        }

        /// <summary>
        /// Run the FindPairs operation using multiple worker threads all at once without entity or body index thread-safety.
        /// </summary>
        /// <param name="inputDeps">The input dependencies for any layers or processors used in the FindPairs operation</param>
        /// <returns>A JobHandle for the scheduled job</returns>
        public JobHandle ScheduleParallelUnsafe(JobHandle inputDeps = default)
        {
            return new FindPairsInternalUnrolled.LayerLayerParallelUnsafe
            {
                layerA    = layerA,
                layerB    = layerB,
                processor = processor
            }.ScheduleParallel(3 * layerA.BucketCount - 2, 1, inputDeps);
        }
        #endregion Schedulers
    }
}

