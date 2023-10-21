using System;
using System.Collections.Generic;
using System.Linq;
using Anatawa12.AvatarOptimizer.APIInternal;
using Anatawa12.AvatarOptimizer.ErrorReporting;
using Anatawa12.AvatarOptimizer.Processors.SkinnedMeshes;
using JetBrains.Annotations;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Anatawa12.AvatarOptimizer.Processors.TraceAndOptimizes
{
    /// <summary>
    /// This class collects ALL dependencies of each component
    /// </summary>
    class ComponentDependencyCollector
    {
        private readonly bool _preserveEndBone;
        private readonly BuildContext _session;
        private readonly ActivenessCache  _activenessCache;
        private readonly GCComponentInfoHolder _componentInfos;

        public ComponentDependencyCollector(BuildContext session, bool preserveEndBone, ActivenessCache activenessCache,
            GCComponentInfoHolder componentInfos)
        {
            _preserveEndBone = preserveEndBone;
            _session = session;
            _activenessCache = activenessCache;
            _componentInfos = componentInfos;
        }


        public void CollectAllUsages()
        {
            var collector = new Collector(this, _activenessCache);
            // second iteration: process parsers
            foreach (var (component, componentInfo) in _componentInfos.AllInformation)
            {
                BuildReport.ReportingObject(component, () =>
                {
                    // component requires GameObject.
                    collector.Init(componentInfo);
                    if (ComponentInfoRegistry.TryGetInformation(component.GetType(), out var information))
                    {
                        information.CollectDependencyInternal(component, collector);
                    }
                    else
                    {
                        BuildReport.LogWarning("TraceAndOptimize:warn:unknown-type", component.GetType().Name);

                        FallbackDependenciesParser(component, collector);
                    }

                    collector.FinalizeForComponent();
                });
            }
        }

        private void FallbackDependenciesParser(Component component, API.ComponentDependencyCollector collector)
        {
            // fallback dependencies: All References are Always Dependencies.
            collector.MarkEntrypoint();
            using (var serialized = new SerializedObject(component))
            {
                foreach (var property in serialized.ObjectReferenceProperties())
                {
                    if (property.objectReferenceValue is GameObject go)
                        collector.AddDependency(go.transform).EvenIfDependantDisabled();
                    else if (property.objectReferenceValue is Component com)
                        collector.AddDependency(com).EvenIfDependantDisabled();
                }
            }
        }

        internal class Collector : API.ComponentDependencyCollector
        {
            private readonly ComponentDependencyCollector _collector;
            private GCComponentInfo _info;
            [NotNull] private readonly ComponentDependencyInfo _dependencyInfoSharedInstance;

            public Collector(ComponentDependencyCollector collector, ActivenessCache activenessCache)
            {
                _collector = collector;
                _dependencyInfoSharedInstance = new ComponentDependencyInfo(activenessCache);
            }
            
            public void Init(GCComponentInfo info)
            {
                Debug.Assert(_info == null, "Init on not finished");
                _info = info;
            }

            public bool PreserveEndBone => _collector._preserveEndBone;

            public MeshInfo2 GetMeshInfoFor(SkinnedMeshRenderer renderer) =>
                _collector._session.GetMeshInfoFor(renderer);

            public override void MarkEntrypoint() => _info.EntrypointComponent = true;

            private API.ComponentDependencyInfo AddDependencyInternal(
                [NotNull] GCComponentInfo info,
                [CanBeNull] Component dependency,
                GCComponentInfo.DependencyType type = GCComponentInfo.DependencyType.Normal)
            {
                _dependencyInfoSharedInstance.Finish();
                _dependencyInfoSharedInstance.Init(info.Component, info.Dependencies, dependency, type);
                return _dependencyInfoSharedInstance;
            }

            public override API.ComponentDependencyInfo AddDependency(Component dependant, Component dependency) =>
                AddDependencyInternal(_collector._componentInfos.GetInfo(dependant), dependency);

            public override API.ComponentDependencyInfo AddDependency(Component dependency) =>
                AddDependencyInternal(_info, dependency);

            public void AddParentDependency(Transform component) =>
                AddDependencyInternal(_info, component.parent, GCComponentInfo.DependencyType.Parent)
                    .EvenIfDependantDisabled();

            public void AddBoneDependency(Transform bone) =>
                AddDependencyInternal(_info, bone, GCComponentInfo.DependencyType.Bone);

            public void FinalizeForComponent()
            {
                _dependencyInfoSharedInstance.Finish();
                _info = null;
            }

            private class ComponentDependencyInfo : API.ComponentDependencyInfo
            {
                private readonly ActivenessCache _activenessCache;

                [NotNull] private Dictionary<Component, GCComponentInfo.DependencyType> _dependencies;
                [CanBeNull] private Component _dependency;
                private Component _dependant;
                private GCComponentInfo.DependencyType _type;

                private bool _evenIfTargetIsDisabled;
                private bool _evenIfThisIsDisabled;

                // ReSharper disable once NotNullOrRequiredMemberIsNotInitialized
                public ComponentDependencyInfo(ActivenessCache activenessCache)
                {
                    _activenessCache = activenessCache;
                }

                internal void Init(Component dependant,
                    [NotNull] Dictionary<Component, GCComponentInfo.DependencyType> dependencies,
                    [CanBeNull] Component component,
                    GCComponentInfo.DependencyType type = GCComponentInfo.DependencyType.Normal)
                {
                    Debug.Assert(_dependency == null, "Init on not finished");
                    _dependencies = dependencies;
                    _dependency = component;
                    _dependant = dependant;
                    _evenIfTargetIsDisabled = true;
                    _evenIfThisIsDisabled = false;
                    _type = type;
                }

                internal void Finish()
                {
                    if (_dependency == null) return;
                    SetToDictionary();
                    _dependency = null;
                }

                private void SetToDictionary()
                {
                    Debug.Assert(_dependency != null, nameof(_dependency) + " != null");

                    if (!_evenIfThisIsDisabled)
                    {
                        // dependant must can be able to be enable
                        if (_activenessCache.GetActiveness(_dependant) == false) return;
                    }
                    
                    if (!_evenIfTargetIsDisabled)
                    {
                        // dependency must can be able to be enable
                        if (_activenessCache.GetActiveness(_dependency) == false) return;
                    }

                    _dependencies.TryGetValue(_dependency, out var type);
                    _dependencies[_dependency] = type | _type;
                }

                public override API.ComponentDependencyInfo EvenIfDependantDisabled()
                {
                    _evenIfThisIsDisabled = true;
                    return this;
                }

                public override API.ComponentDependencyInfo OnlyIfTargetCanBeEnable()
                {
                    _evenIfTargetIsDisabled = false;
                    return this;
                }
            }
        }
    }
}

