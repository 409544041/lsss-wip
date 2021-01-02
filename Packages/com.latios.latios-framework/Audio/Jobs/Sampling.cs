﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Latios.Audio
{
    internal static class Sampling
    {
        const double ITD_TIME = 0.0007;

        [BurstCompile]
        public struct SampleOneshotClipsJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeArray<ClipFrameLookup> clipFrameLookups;
            [ReadOnly] public NativeArray<Weights>         weights;
            [ReadOnly] public NativeArray<int>             targetListenerIndices;

            [ReadOnly] public NativeArray<ListenerBufferParameters> listenerBufferParameters;
            [ReadOnly] public NativeArray<int2>                     forIndexToListenerAndChannelIndices;

            [NativeDisableParallelForRestriction] public NativeArray<float> outputSamplesMegaBuffer;

            public int sampleRate;
            public int audioFrame;
            public int samplesPerSubframe;

            public void Execute(int forIndex)
            {
                int  listenerIndex            = forIndexToListenerAndChannelIndices[forIndex].x;
                int  channelIndex             = forIndexToListenerAndChannelIndices[forIndex].y;
                var  targetListenerParameters = listenerBufferParameters[listenerIndex];
                int  samplesPerFrame          = samplesPerSubframe * targetListenerParameters.subFramesPerFrame;
                bool isRightChannel           = targetListenerParameters.channelIsRight.IsSet(channelIndex);

                var outputSamples = outputSamplesMegaBuffer.GetSubArray(targetListenerParameters.bufferStart + targetListenerParameters.samplesPerChannel * channelIndex,
                                                                        targetListenerParameters.samplesPerChannel);

                for (int clipIndex = 0; clipIndex < clipFrameLookups.Length; clipIndex++)
                {
                    if (targetListenerIndices[clipIndex] != listenerIndex)
                        continue;

                    ref var clip          = ref clipFrameLookups[clipIndex].clip.Value;
                    int     spawnFrame    = clipFrameLookups[clipIndex].spawnFrameOrOffsetIndex;
                    var     channelWeight = weights[clipIndex].channelWeights[channelIndex];
                    var     itdWeights    = weights[clipIndex].itdWeights;

                    double itdMaxOffset        = clip.sampleRate * ITD_TIME;
                    double clipSampleStride    = clip.sampleRate / (double)sampleRate;
                    double clipSamplesPerFrame = clipSampleStride * samplesPerFrame;

                    for (int itd = 0; itd < itdWeights.Length; itd++)
                    {
                        float weight = itdWeights[itd] * channelWeight;

                        double itdOffset = math.lerp(-itdMaxOffset, itdMaxOffset, itd / (double)(itdWeights.Length - 1));

                        int jumpFrames = audioFrame - spawnFrame;

                        if (clip.sampleRate == sampleRate)
                        {
                            int clipStart = jumpFrames * samplesPerFrame + (int)math.round(itdOffset);
                            SampleMatchedRate(ref clip, clipStart, isRightChannel, weight, outputSamples);
                        }
                        else
                        {
                            double clipStart = jumpFrames * clipSamplesPerFrame + itdOffset;
                            SampleMismatchedRate(ref clip, clipStart, clipSampleStride, isRightChannel, weight, outputSamples);
                        }
                    }
                }
            }

            void SampleMatchedRate(ref AudioClipBlob clip, int clipStart, bool isRightChannel, float weight, NativeArray<float> output)
            {
                int outputStartIndex     = math.min(clipStart, 0);
                int remainingClipSamples = clip.samplesLeftOrMono.Length - clipStart;
                int remainingSamples     = math.min(output.Length, math.select(0, remainingClipSamples, remainingClipSamples > 0));

                if (isRightChannel && clip.isStereo)
                {
                    for (int i = outputStartIndex; i < remainingSamples; i++)
                    {
                        output[i] += clip.samplesRight[clipStart + i] * weight;
                    }
                }
                else
                {
                    for (int i = outputStartIndex; i < remainingSamples; i++)
                    {
                        output[i] += clip.samplesLeftOrMono[clipStart + i] * weight;
                    }
                }
            }

            void SampleMismatchedRate(ref AudioClipBlob clip, double clipStart, double clipSampleStride, bool isRightChannel, float weight, NativeArray<float> output)
            {
                ref var clipSamples = ref (isRightChannel && clip.isStereo ? ref clip.samplesRight : ref clip.samplesLeftOrMono);

                for (int i = 0; i < output.Length; i++)
                {
                    double pos         = clipStart + clipSampleStride * i;
                    int    posLeft     = (int)pos;
                    int    posRight    = posLeft + 1;
                    int    safeLeft    = math.clamp(posLeft, 0, clipSamples.Length - 1);
                    int    safeRight   = math.clamp(posRight, 0, clipSamples.Length - 1);
                    float  leftSample  = math.select(0f, clipSamples[safeLeft], posLeft == safeLeft);
                    float  rightSample = math.select(0f, clipSamples[safeRight], posRight == safeRight);
                    output[i]          = math.lerp(leftSample, rightSample, (float)math.frac(pos)) * weight;
                }
            }
        }

        [BurstCompile]
        public struct SampleLoopedClipsJob : IJobParallelForDefer
        {
            [ReadOnly] public NativeArray<ClipFrameLookup> clipFrameLookups;
            [ReadOnly] public NativeArray<Weights>         weights;
            [ReadOnly] public NativeArray<int>             targetListenerIndices;

            [ReadOnly] public NativeArray<ListenerBufferParameters> listenerBufferParameters;
            [ReadOnly] public NativeArray<int2>                     forIndexToListenerAndChannelIndices;

            [NativeDisableParallelForRestriction] public NativeArray<float> outputSamplesMegaBuffer;

            public int sampleRate;
            public int audioFrame;
            public int samplesPerSubframe;

            public void Execute(int forIndex)
            {
                int  listenerIndex            = forIndexToListenerAndChannelIndices[forIndex].x;
                int  channelIndex             = forIndexToListenerAndChannelIndices[forIndex].y;
                var  targetListenerParameters = listenerBufferParameters[listenerIndex];
                int  samplesPerFrame          = samplesPerSubframe * targetListenerParameters.subFramesPerFrame;
                bool isRightChannel           = targetListenerParameters.channelIsRight.IsSet(channelIndex);

                var outputSamples = outputSamplesMegaBuffer.GetSubArray(targetListenerParameters.bufferStart + targetListenerParameters.samplesPerChannel * channelIndex,
                                                                        targetListenerParameters.samplesPerChannel);

                for (int clipIndex = 0; clipIndex < clipFrameLookups.Length; clipIndex++)
                {
                    if (targetListenerIndices[clipIndex] != listenerIndex)
                        continue;

                    ref var clip          = ref clipFrameLookups[clipIndex].clip.Value;
                    int     loopOffset    = clip.loopedOffsets[clipFrameLookups[clipIndex].spawnFrameOrOffsetIndex];
                    var     channelWeight = weights[clipIndex].channelWeights[channelIndex];
                    var     itdWeights    = weights[clipIndex].itdWeights;

                    double itdMaxOffset     = clip.sampleRate * ITD_TIME;
                    double clipSampleStride = clip.sampleRate / (double)sampleRate;

                    for (int itd = 0; itd < itdWeights.Length; itd++)
                    {
                        float weight = itdWeights[itd] * channelWeight;

                        double itdOffset     = math.lerp(-itdMaxOffset, itdMaxOffset, itd / (double)(itdWeights.Length - 1));
                        ulong  samplesPlayed = (ulong)samplesPerFrame * (ulong)audioFrame;

                        if (clip.sampleRate == sampleRate)
                        {
                            int clipStart = (int)(samplesPlayed % (ulong)clip.samplesLeftOrMono.Length) + (int)math.round(itdOffset);
                            SampleMatchedRate(ref clip, clipStart, isRightChannel, weight, outputSamples);
                        }
                        else
                        {
                            double clipLengthInOutputSamples      = clip.samplesLeftOrMono.Length * clipSampleStride;
                            ulong  clipLengthInOutputSamplesFloor = (ulong)clipLengthInOutputSamples;
                            ulong  loops                          = samplesPlayed / clipLengthInOutputSamplesFloor;
                            ulong  currentLoopSamples             = samplesPlayed - (loops * clipLengthInOutputSamplesFloor);
                            double clipStart                      = currentLoopSamples + loops * math.frac(clipLengthInOutputSamples) + itdOffset + loopOffset;
                            while (clipStart < 0)
                            {
                                clipStart += clipLengthInOutputSamples;
                            }
                            while (clipStart >= clipLengthInOutputSamples)
                            {
                                clipStart -= clipLengthInOutputSamples;
                            }
                            SampleMismatchedRate(ref clip, clipStart, clipSampleStride, isRightChannel, weight, outputSamples);
                        }
                    }
                }
            }

            void SampleMatchedRate(ref AudioClipBlob clip, int clipStart, bool isRightChannel, float weight, NativeArray<float> output)
            {
                if (isRightChannel && clip.isStereo)
                {
                    for (int i = 0; i < output.Length; i++)
                    {
                        int index  = (clipStart + i) % clip.samplesRight.Length;
                        output[i] += clip.samplesRight[index] * weight;
                    }
                }
                else
                {
                    for (int i = 0; i < output.Length; i++)
                    {
                        int index  = (clipStart + i) % clip.samplesLeftOrMono.Length;
                        output[i] += clip.samplesLeftOrMono[index] * weight;
                    }
                }
            }

            void SampleMismatchedRate(ref AudioClipBlob clip, double clipStart, double clipSampleStride, bool isRightChannel, float weight, NativeArray<float> output)
            {
                ref var clipSamples               = ref (isRightChannel && clip.isStereo ? ref clip.samplesRight : ref clip.samplesLeftOrMono);
                double  clipLengthInOutputSamples = clipSamples.Length * clipSampleStride;

                for (int i = 0; i < output.Length; i++)
                {
                    double pos         = clipStart + clipSampleStride * i;
                    int    posLeft     = (int)pos % clipSamples.Length;
                    int    posRight    = (posLeft + 1) % clipSamples.Length;  //This handles wraparound between last sample and first sample
                    float  leftSample  = clipSamples[posLeft];
                    float  rightSample = clipSamples[posRight];
                    output[i]          = math.lerp(leftSample, rightSample, (float)math.frac(pos)) * weight;
                }
            }
        }
    }
}

