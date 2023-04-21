using System;
using System.Linq;
using CustomLocalization4EditorExtension;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Anatawa12.AvatarOptimizer
{
    [CustomEditor(typeof(MergePhysBone))]
    internal class MergePhysBoneEditor : AvatarTagComponentEditorBase
    {
        private static class Style
        {
            public static readonly GUIStyle ErrorStyle = new GUIStyle
            {
                normal = { textColor = Color.red },
                wordWrap = false,
            };

            public static readonly GUIStyle WarningStyle = new GUIStyle
            {
                normal = { textColor = Color.yellow },
                wordWrap = false,
            };
        }

        private SerializedObject _mergedPhysBone;
        [CanBeNull] private SerializedObject _sourcePhysBone;
        private SerializedProperty _makeParent;
        private SerializedProperty _forcesProp;
        private SerializedProperty _pullProp;
        private SerializedProperty _springProp;
        private SerializedProperty _stiffnessProp;
        private SerializedProperty _gravityProp;
        private SerializedProperty _gravityFalloffProp;
        private SerializedProperty _immobileProp;
        private SerializedProperty _limitsProp;
        private SerializedProperty _maxAngleXProp;
        private SerializedProperty _limitRotationProp;
        private SerializedProperty _maxAngleZProp;
        private SerializedProperty _radiusProp;
        private SerializedProperty _allowCollisionProp;
        private SerializedProperty _collidersProp;
        private SerializedProperty _allowGrabbingProp;
        private SerializedProperty _grabMovementProp;
        private SerializedProperty _allowPosingProp;
        private SerializedProperty _maxStretchProp;
        private SerializedProperty _snapToHandProp;
        private SerializedProperty _isAnimatedProp;
        private SerializedProperty _resetWhenDisabledProp;
        private SerializedProperty _componentsSetProp;
        private PrefabSafeSet.EditorUtil<VRCPhysBoneBase> _componentsSetEditorUtil;

        private void OnEnable()
        {
            var nestCount = PrefabSafeSet.PrefabSafeSetUtil.PrefabNestCount(serializedObject.targetObject);
            _mergedPhysBone =
                new SerializedObject(((Component)serializedObject.targetObject).GetComponent<VRCPhysBone>());
            _makeParent = serializedObject.FindProperty("makeParent");
            _forcesProp = serializedObject.FindProperty("forces");
            _pullProp = serializedObject.FindProperty("pull");
            _springProp = serializedObject.FindProperty("spring");
            _stiffnessProp = serializedObject.FindProperty("stiffness");
            _gravityProp = serializedObject.FindProperty("gravity");
            _gravityFalloffProp = serializedObject.FindProperty("gravityFalloff");
            _immobileProp = serializedObject.FindProperty("immobile");
            _limitsProp = serializedObject.FindProperty("limits");
            _maxAngleXProp = serializedObject.FindProperty("maxAngleX");
            _limitRotationProp = serializedObject.FindProperty("limitRotation");
            _maxAngleZProp = serializedObject.FindProperty("maxAngleZ");
            _radiusProp = serializedObject.FindProperty("radius");
            _allowCollisionProp = serializedObject.FindProperty("allowCollision");
            _collidersProp = serializedObject.FindProperty("colliders");
            _allowGrabbingProp = serializedObject.FindProperty("allowGrabbing");
            _grabMovementProp = serializedObject.FindProperty("grabMovement");
            _allowPosingProp = serializedObject.FindProperty("allowPosing");
            _maxStretchProp = serializedObject.FindProperty("maxStretch");
            _snapToHandProp = serializedObject.FindProperty("snapToHand");
            _isAnimatedProp = serializedObject.FindProperty("isAnimated");
            _resetWhenDisabledProp = serializedObject.FindProperty("resetWhenDisabled");
            _componentsSetProp = serializedObject.FindProperty(nameof(MergePhysBone.componentsSet));
            _componentsSetEditorUtil = PrefabSafeSet.EditorUtil<VRCPhysBoneBase>.Create(
                _componentsSetProp, nestCount, x => (VRCPhysBoneBase)x.objectReferenceValue,
                (x, v) => x.objectReferenceValue = v);
        }

        protected override void OnInspectorGUIInner()
        {
            _mergedPhysBone.Update();
            EditorGUILayout.PropertyField(_makeParent);
            if (_makeParent.boolValue && ((Component)target).transform.childCount != 0)
                EditorGUILayout.HelpBox(CL4EE.Tr("MergePhysBone:error:makeParentWithChildren"), MessageType.Error);

            var sourcePysBone = _componentsSetEditorUtil.Values.FirstOrDefault();
            _sourcePhysBone = sourcePysBone == null ? null : new SerializedObject(sourcePysBone);

            if (_sourcePhysBone == null)
            {
                EditorGUILayout.HelpBox(CL4EE.Tr("MergePhysBone:error:noSources"), MessageType.Error);
            }
            else
            {
                // == Forces ==
                EditorGUILayout.LabelField("Forces", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                PbProp("Integration Type", "integrationType", _forcesProp);
                var isSimplified = (_forcesProp.boolValue ? _mergedPhysBone : _sourcePhysBone)
                    .FindProperty("integrationType")
                    .enumValueIndex == 0;
                PbCurveProp("Pull", "pull", "pullCurve", _pullProp, _forcesProp);
                if (isSimplified)
                {
                    PbCurveProp("Spring", "spring", "springCurve", _springProp, _forcesProp);
                }
                else
                {
                    PbCurveProp("Momentum", "spring", "springCurve", _springProp, _forcesProp);
                    PbCurveProp("Stiffness", "stiffness", "stiffnessCurve", _stiffnessProp, _forcesProp);
                }
                PbCurveProp("Gravity", "gravity", "gravityCurve", _gravityProp, _forcesProp);
                PbCurveProp("Gravity Falloff", "gravityFalloff", "gravityFalloffCurve", _gravityFalloffProp,
                    _forcesProp);
                PbCurveProp("Immobile", "immobile", "immobileCurve", _immobileProp, _forcesProp);
                EditorGUI.indentLevel--;
                // == Limits ==
                EditorGUILayout.LabelField("Limits", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                PbProp("Limit Type", "limitType", _limitsProp);
                var limitType =
                    (VRCPhysBoneBase.LimitType)(_limitsProp.boolValue ? _mergedPhysBone : _sourcePhysBone)
                    .FindProperty("limitType").enumValueIndex;
                
                switch (limitType)
                {
                    case VRCPhysBoneBase.LimitType.None:
                        break;
                    case VRCPhysBoneBase.LimitType.Angle:
                    case VRCPhysBoneBase.LimitType.Hinge:
                        PbCurveProp("Max Angle", "maxAngleX", "maxAngleXCurve", _maxAngleXProp, _limitsProp);
                        Pb3DCurveProp("Rotation", "limitRotation", 
                            "Pitch", "limitRotationXCurve", 
                            "Roll", "limitRotationYCurve", 
                            "Yaw", "limitRotationZCurve",
                            _limitRotationProp, _limitsProp);
                        break;
                    case VRCPhysBoneBase.LimitType.Polar:
                        PbCurveProp("Max Angle X", "maxAngleX", "maxAngleXCurve", _maxAngleXProp, _limitsProp);
                        PbCurveProp("Max Angle Z", "maxAngleZ", "maxAngleZCurve", _maxAngleZProp, _limitsProp);
                        Pb3DCurveProp("Rotation", "limitRotation", 
                            "Pitch", "limitRotationXCurve", 
                            "Roll", "limitRotationYCurve", 
                            "Yaw", "limitRotationZCurve",
                            _limitRotationProp, _limitsProp);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                EditorGUI.EndDisabledGroup();
                EditorGUI.indentLevel--;
                // == Collision ==
                EditorGUILayout.LabelField("Collision", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_radiusProp);
                EditorGUILayout.PropertyField(_allowCollisionProp);
                EditorGUILayout.PropertyField(_collidersProp);
                EditorGUI.indentLevel--;
                // == Grab & Pose ==
                EditorGUILayout.LabelField("Collision", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_allowGrabbingProp);
                EditorGUILayout.PropertyField(_grabMovementProp);
                EditorGUILayout.PropertyField(_allowPosingProp);
                EditorGUILayout.PropertyField(_maxStretchProp);
                EditorGUILayout.PropertyField(_snapToHandProp);
                EditorGUI.indentLevel--;
                // == Others ==
                EditorGUILayout.LabelField("Others", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_isAnimatedProp);
                EditorGUILayout.PropertyField(_resetWhenDisabledProp);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(_componentsSetProp);

            serializedObject.ApplyModifiedProperties();

            var differs = Processors.MergePhysBoneProcessor.CollectDifferentProps((MergePhysBone)target,
                ((MergePhysBone)target).componentsSet.GetAsSet());
            if (differs.Count != 0)
            {
                GUILayout.Label("The following properies are different", Style.ErrorStyle);
                foreach (var differ in differs)
                    GUILayout.Label($"  {differ}", Style.ErrorStyle);
            }

            _mergedPhysBone.ApplyModifiedProperties();
        }

        const float OverrideWidth = 48f;
        const float CurveButtonWidth = 20f;

        private (Rect restRect, Rect fixedRect) SplitRect(Rect propRect, float width)
        {
            var restRect = propRect;
            restRect.width -= EditorGUIUtility.standardVerticalSpacing + width;
            var fixedRect = propRect;
            fixedRect.x = restRect.xMax + EditorGUIUtility.standardVerticalSpacing;
            fixedRect.width = width;
            return (restRect, fixedRect);
        }

        private (Rect restRect, Rect fixedRect0, Rect fixedRect1) SplitRect(Rect propRect, float width0, float width1)
        {
            var (tmp, fixedRect1) = SplitRect(propRect, width1);
            var (restRect, fixedRect0) = SplitRect(tmp, width0);
            return (restRect, fixedRect0, fixedRect1);
        }

        
        bool IsCurveWithValue(SerializedProperty prop) =>
            prop.animationCurveValue != null && prop.animationCurveValue.length > 0;


        private void PbProp([NotNull] string label, 
            [NotNull] string pbPropName, 
            [NotNull] SerializedProperty overridePropName,
            [ItemNotNull] [NotNull] params SerializedProperty[] overrides)
        {
            PbPropImpl(label, overridePropName, overrides, 
                (valueRect, obj, labelContent) => EditorGUI.PropertyField(valueRect, obj.FindProperty(pbPropName), labelContent) );
        }

        private void DrawCurveFieldWithButton(SerializedProperty curveProp, Rect buttonRect, Func<Rect> curveRect)
        {
            if (IsCurveWithValue(curveProp))
            {
                if (GUI.Button(buttonRect, "X"))
                {
                    curveProp.animationCurveValue = new AnimationCurve();
                }

                var rect = curveRect();
                EditorGUI.BeginProperty(rect, null, curveProp);
                EditorGUI.BeginChangeCheck();
                var cur = EditorGUI.CurveField(rect, " ", curveProp.animationCurveValue, Color.cyan,
                    new Rect(0.0f, 0.0f, 1f, 1f));
                if (EditorGUI.EndChangeCheck())
                    curveProp.animationCurveValue = cur;
                EditorGUI.EndProperty();
            }
            else
            {
                if (GUI.Button(buttonRect, "C"))
                {
                    var curve = new AnimationCurve();
                    curve.AddKey(new Keyframe(0.0f, 1f));
                    curve.AddKey(new Keyframe(1f, 1f));
                    curveProp.animationCurveValue = curve;
                }
            }
        }

        private void PbCurveProp([NotNull] string label,
            [NotNull] string pbPropName,
            [NotNull] string pbCurvePropName,
            [NotNull] SerializedProperty overridePropName,
            [ItemNotNull] [NotNull] params SerializedProperty[] overrides)
        {
            PbPropImpl(label, overridePropName, overrides, (rect, obj, labelContent) =>
            {
                var (valueRect, buttonRect) = SplitRect(rect, CurveButtonWidth);

                var valueProp = obj.FindProperty(pbPropName);
                var curveProp = obj.FindProperty(pbCurvePropName);

                EditorGUI.PropertyField(valueRect, valueProp, labelContent);
                DrawCurveFieldWithButton(curveProp, buttonRect, 
                    () => SplitRect(EditorGUILayout.GetControlRect(), OverrideWidth).restRect);
            });
        }

        private void Pb3DCurveProp([NotNull] string label,
            [NotNull] string pbPropName,
            [NotNull] string pbXCurveLabel, [NotNull] string pbXCurvePropName,
            [NotNull] string pbYCurveLabel, [NotNull] string pbYCurvePropName,
            [NotNull] string pbZCurveLabel, [NotNull] string pbZCurvePropName,
            [NotNull] SerializedProperty overridePropName,
            [ItemNotNull] [NotNull] params SerializedProperty[] overrides)
        {
            PbPropImpl(label, overridePropName, overrides, (rect, obj, labelContent) =>
            {
                var (valueRect, buttonRect) = SplitRect(rect, CurveButtonWidth);

                var valueProp = obj.FindProperty(pbPropName);
                var xCurveProp = obj.FindProperty(pbXCurvePropName);
                var yCurveProp = obj.FindProperty(pbYCurvePropName);
                var zCurveProp = obj.FindProperty(pbZCurvePropName);

                void DrawCurve(string curveLabel, SerializedProperty curveProp)
                {
                    var (curveRect, curveButtonRect, _) = SplitRect(EditorGUILayout.GetControlRect(true),
                        CurveButtonWidth, OverrideWidth);

                    EditorGUI.LabelField(curveRect, curveLabel, " ");
                    DrawCurveFieldWithButton(curveProp, curveButtonRect, () => curveRect);
                }

                if (IsCurveWithValue(xCurveProp) || IsCurveWithValue(yCurveProp) || IsCurveWithValue(zCurveProp))
                {
                    // with curve
                    EditorGUI.PropertyField(valueRect, valueProp, labelContent);
                    DrawCurve(pbXCurveLabel, xCurveProp);
                    DrawCurve(pbYCurveLabel, yCurveProp);
                    DrawCurve(pbZCurveLabel, zCurveProp);
                }
                else
                {
                    // without curve: constant
                    EditorGUI.PropertyField(valueRect, valueProp, labelContent);
                    
                    if (GUI.Button(buttonRect, "C"))
                    {
                        var curve = new AnimationCurve();
                        curve.AddKey(new Keyframe(0.0f, 1f));
                        curve.AddKey(new Keyframe(1f, 1f));
                        xCurveProp.animationCurveValue = curve;
                        yCurveProp.animationCurveValue = curve;
                        zCurveProp.animationCurveValue = curve;
                    }
                }
            });
        }

        private static readonly string[] CopyOverride = { "Copy", "Override" };

        private void PbPropImpl([NotNull] string label, 
            [NotNull] SerializedProperty overrideProp, 
            [ItemNotNull] [NotNull] SerializedProperty[] overrides, 
            [NotNull] Action<Rect, SerializedObject, GUIContent> renderer)
        {
            var labelContent = new GUIContent(label);
            var forceOverride = overrides.Any(x => x.boolValue);

            var (valueRect, overrideRect) =
                SplitRect(EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight), OverrideWidth);

            if (forceOverride || overrideProp.boolValue)
            {
                // Override mode

                renderer(valueRect, _mergedPhysBone, labelContent);

                if (forceOverride)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    PopupNoIndent(overrideRect, 1, CopyOverride);
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    EditorGUI.BeginProperty(overrideRect, null, overrideProp);
                    var selected = PopupNoIndent(overrideRect, 1, CopyOverride);
                    if (selected != 1)
                        overrideProp.boolValue = false;
                    EditorGUI.EndProperty();
                }
            }
            else
            {
                // Copy mode
                EditorGUI.BeginDisabledGroup(true);
                renderer(valueRect, _sourcePhysBone, labelContent);
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginProperty(overrideRect, null, overrideProp);
                var selected = PopupNoIndent(overrideRect, 0, CopyOverride);
                if (selected != 0)
                    overrideProp.boolValue = true;
                EditorGUI.EndProperty();

                // TODO: warn if value differs between pbs
            }
        }

        private static int PopupNoIndent(Rect position, int selectedIndex, string[] displayedOptions)
        {
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var result = EditorGUI.Popup(position, selectedIndex, displayedOptions);
            EditorGUI.indentLevel = indent;
            return result;
        }
    }
}
