using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using JetBrains.Annotations;
using nadena.dev.ndmf.preview;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Anatawa12.AvatarOptimizer.EditModePreview
{
    internal class RemoveMeshInBoxRendererFilter : IRenderFilter
    {
        public static RemoveMeshInBoxRendererFilter Instance { get; } = new();

        public ImmutableList<RenderGroup> GetTargetGroups(ComputeContext ctx)
        {
            // currently remove meshes are only supported
            var rmByMask = ctx.GetComponentsByType<RemoveMeshInBox>();

            var targets = new HashSet<Renderer>();

            foreach (var component in rmByMask)
            {
                if (component.GetComponent<MergeSkinnedMesh>())
                {
                    // the component applies to MergeSkinnedMesh, which is not supported for now
                    // TODO: rollup the remove operation to source renderers of MergeSkinnedMesh
                    continue;
                }

                var renderer = component.GetComponent<SkinnedMeshRenderer>();
                if (renderer == null) continue;
                if (renderer.sharedMesh == null) continue;

                targets.Add(renderer);
            }

            return targets.Select(RenderGroup.For).ToImmutableList();
        }

        public async Task<IRenderFilterNode> Instantiate(RenderGroup group, IEnumerable<(Renderer, Renderer)> proxyPairs, ComputeContext context)
        {
            var pair = proxyPairs.Single();
            if (!(pair.Item1 is SkinnedMeshRenderer original)) return null;
            if (!(pair.Item2 is SkinnedMeshRenderer proxy)) return null;

            // we modify the mesh so we need to clone the mesh

            var rmByMask = context.Observe(context.GetComponent<RemoveMeshInBox>(original.gameObject));

            var node = new RemoveMeshInBoxRendererNode();

            await node.Process(original, proxy, rmByMask, context);

            return node;
        }
    }

    internal class RemoveMeshInBoxRendererNode : IRenderFilterNode
    {
        private Mesh _duplicated;

        public RenderAspects Reads => RenderAspects.Mesh | RenderAspects.Shapes;
        public RenderAspects WhatChanged => RenderAspects.Mesh | RenderAspects.Shapes;

        public async Task Process(
            SkinnedMeshRenderer original,
            SkinnedMeshRenderer proxy,
            [NotNull] RemoveMeshInBox rmInBox,
            ComputeContext context)
        {
            UnityEngine.Profiling.Profiler.BeginSample($"RemoveMeshInBoxRendererNode.Process({original.name})");

            var duplicated = Object.Instantiate(proxy.sharedMesh);
            duplicated.name = proxy.sharedMesh.name + " (AAO Generated)";

            UnityEngine.Profiling.Profiler.BeginSample("BakeMesh");
            var tempMesh = new Mesh();
            proxy.BakeMesh(tempMesh);
            UnityEngine.Profiling.Profiler.EndSample();

            using var realPosition = new NativeArray<Vector3>(tempMesh.vertices, Allocator.TempJob);

            using var vertexIsInBox = new NativeArray<bool>(duplicated.vertexCount, Allocator.TempJob);

            UnityEngine.Profiling.Profiler.BeginSample("CollectVertexData");
            {
                using var boxes = new NativeArray<RemoveMeshInBox.BoundingBox>(rmInBox.boxes, Allocator.TempJob);

                new CheckRemoveVertexJob
                {
                    boxes = boxes,
                    vertexPosition = realPosition,
                    vertexIsInBox = vertexIsInBox,
                }.Schedule(duplicated.vertexCount, 32).Complete();
            }
            UnityEngine.Profiling.Profiler.EndSample();

            var uv = duplicated.uv;
            using var uvJob = new NativeArray<Vector2>(uv, Allocator.TempJob);

            for (var subMeshI = 0; subMeshI < duplicated.subMeshCount; subMeshI++)
            {
                var subMesh = duplicated.GetSubMesh(subMeshI);
                int vertexPerPrimitive;
                switch (subMesh.topology)
                {
                    case MeshTopology.Triangles:
                        vertexPerPrimitive = 3;
                        break;
                    case MeshTopology.Quads:
                        vertexPerPrimitive = 4;
                        break;
                    case MeshTopology.Lines:
                        vertexPerPrimitive = 2;
                        break;
                    case MeshTopology.Points:
                        vertexPerPrimitive = 1;
                        break;
                    case MeshTopology.LineStrip:
                    default:
                        // unsupported topology
                        continue;
                }

                var triangles = duplicated.GetTriangles(subMeshI);
                var primitiveCount = triangles.Length / vertexPerPrimitive;

                using var trianglesJob = new NativeArray<int>(triangles, Allocator.TempJob);
                using var shouldRemove = new NativeArray<bool>(primitiveCount, Allocator.TempJob);
                UnityEngine.Profiling.Profiler.BeginSample("JobLoop");
                var job = new ShouldRemovePrimitiveJob
                {
                    vertexPerPrimitive = vertexPerPrimitive,
                    triangles = trianglesJob,
                    vertexIsInBox = vertexIsInBox,
                    shouldRemove = shouldRemove,
                };
                job.Schedule(primitiveCount, 32).Complete();
                UnityEngine.Profiling.Profiler.EndSample();

                var modifiedTriangles = new List<int>(triangles.Length);

                UnityEngine.Profiling.Profiler.BeginSample("Inner Main Loop");
                for (var primitiveI = 0; primitiveI < primitiveCount; primitiveI++)
                    if (!shouldRemove[primitiveI])
                        for (var vertexI = 0; vertexI < vertexPerPrimitive; vertexI++)
                            modifiedTriangles.Add(triangles[primitiveI * vertexPerPrimitive + vertexI]);
                UnityEngine.Profiling.Profiler.EndSample();

                duplicated.SetTriangles(modifiedTriangles, subMeshI);
            }

            proxy.sharedMesh = duplicated;
            _duplicated = duplicated;

            UnityEngine.Profiling.Profiler.EndSample();
        }

        [BurstCompile]
        struct CheckRemoveVertexJob : IJobParallelFor
        {
            // ReSharper disable InconsistentNaming
            [ReadOnly]
            public NativeArray<RemoveMeshInBox.BoundingBox> boxes;
            [ReadOnly]
            public NativeArray<Vector3> vertexPosition;
            public NativeArray<bool> vertexIsInBox;
            // ReSharper restore InconsistentNaming

            public void Execute(int vertexIndex)
            {
                var inBox = false;

                var position = vertexPosition[vertexIndex];
                foreach (var box in boxes)
                {
                    if (box.ContainsVertex(position))
                    {
                        inBox = true;
                        break;
                    }
                }

                vertexIsInBox[vertexIndex] = inBox;
            }
        }

        [BurstCompile]
        struct ShouldRemovePrimitiveJob : IJobParallelFor
        {
            // ReSharper disable InconsistentNaming
            public int vertexPerPrimitive;
            [ReadOnly]
            public NativeArray<int> triangles;
            [ReadOnly]
            public NativeArray<bool> vertexIsInBox;
            [WriteOnly]
            public NativeArray<bool> shouldRemove;
            // ReSharper restore InconsistentNaming

            public void Execute(int primitiveIndex)
            {
                var baseIndex = primitiveIndex * vertexPerPrimitive;
                var indices = triangles.Slice(baseIndex, vertexPerPrimitive);

                var result = true;
                foreach (var index in indices)
                {
                    if (!vertexIsInBox[index])
                    {
                        result = false;
                        break;
                    }
                }

                shouldRemove[primitiveIndex] = result;
            }
        }

        public void OnFrame(Renderer original, Renderer proxy)
        {
            if (_duplicated == null) return;
            if (proxy is SkinnedMeshRenderer skinnedMeshProxy)
                skinnedMeshProxy.sharedMesh = _duplicated;
        }

        public void Dispose()
        {
            if (_duplicated != null)
            {
                Object.DestroyImmediate(_duplicated);
                _duplicated = null;
            }
        }
    }
}
