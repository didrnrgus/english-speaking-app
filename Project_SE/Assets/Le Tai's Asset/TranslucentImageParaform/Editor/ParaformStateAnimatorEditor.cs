using System;
using System.Collections.Generic;
using LeTai.Paraform.Scaffold;
using LeTai.Paraform.Scaffold.Editor;
using UnityEditor;
using UnityEngine;

namespace LeTai.Paraform.Editor
{
[CustomEditor(typeof(ParaformStateAnimator))]
public class ParaformStateAnimatorEditor : UnityEditor.Editor
{
    sealed class OverrideEditor
    {
        public SerializedProperty customStateName;
        public SerializedProperty overrideForm;
        public ParaformEditor     config;
        public bool               expanded;
        public string             foldoutKey;
    }

    readonly List<OverrideEditor> customEditors = new();

    OverrideEditor normalEditor;
    OverrideEditor hoverEditor;
    OverrideEditor selectedEditor;
    OverrideEditor clickedEditor;
    OverrideEditor disabledEditor;

    SerializedProperty durationProperty;
    SerializedProperty easeProperty;
    SerializedProperty autoDeselectProperty;
    SerializedProperty interactionTargetProperty;
    SerializedProperty customOverridesProperty;

    void OnEnable()
    {
        durationProperty          = serializedObject.FindProperty(nameof(ParaformStateAnimator.duration));
        easeProperty              = serializedObject.FindProperty(nameof(ParaformStateAnimator.ease));
        autoDeselectProperty      = serializedObject.FindProperty(nameof(ParaformStateAnimator.autoDeselect));
        interactionTargetProperty = serializedObject.FindProperty(nameof(ParaformStateAnimator.interactionTarget));
        customOverridesProperty   = serializedObject.FindProperty(nameof(ParaformStateAnimator.customOverrides));
        BuildEditors();
    }

    void BuildEditors()
    {
        normalEditor   = BuildEditor(serializedObject.FindProperty(nameof(ParaformStateAnimator.normalOverride)));
        hoverEditor    = BuildEditor(serializedObject.FindProperty(nameof(ParaformStateAnimator.hoverOverride)));
        selectedEditor = BuildEditor(serializedObject.FindProperty(nameof(ParaformStateAnimator.selectedOverride)));
        clickedEditor  = BuildEditor(serializedObject.FindProperty(nameof(ParaformStateAnimator.clickedOverride)));
        disabledEditor = BuildEditor(serializedObject.FindProperty(nameof(ParaformStateAnimator.disabledOverride)));
        customEditors.Clear();
        for (var index = 0; index < customOverridesProperty.arraySize; index++)
            customEditors.Add(BuildEditor(customOverridesProperty.GetArrayElementAtIndex(index)));
    }

    OverrideEditor BuildEditor(SerializedProperty entry)
    {
        var config       = entry.FindPropertyRelative(nameof(ParaformStateAnimator.ParaformOverride.config));
        var propertyPath = entry.propertyPath;
        return new OverrideEditor {
            customStateName = entry.FindPropertyRelative(nameof(ParaformStateAnimator.ParaformOverride.name)),
            overrideForm    = entry.FindPropertyRelative(nameof(ParaformStateAnimator.ParaformOverride.overrideForm)),
            expanded        = EditorPrefs.GetBool($"{nameof(ParaformStateAnimatorEditor)}.{propertyPath}"),
            foldoutKey      = $"{nameof(ParaformStateAnimatorEditor)}.{propertyPath}",
            config = new ParaformEditor(config, "Effect Overrides (match by name)",
                                        (rect, _) => ShowEffectMenu(propertyPath, rect))
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            var saveDuringPlay = PlayModeStateSave.IsSaved((ParaformStateAnimator)target);
            var saveSelected   = GUILayout.Toggle(saveDuringPlay, "Save Play Mode changes", EditorStyles.miniButton);
            if (saveSelected != saveDuringPlay)
                PlayModeStateSave.Toggle((ParaformStateAnimator)target);
        }

        EditorGUILayout.PropertyField(durationProperty);
        EditorGUILayout.PropertyField(easeProperty);
        EditorGUILayout.PropertyField(interactionTargetProperty);
        if (!interactionTargetProperty.objectReferenceValue)
            EditorGUILayout.PropertyField(autoDeselectProperty);

        if (customEditors.Count != customOverridesProperty.arraySize)
            BuildEditors();

        DrawOverride(normalEditor,   "Normal");
        DrawOverride(hoverEditor,    "Hover");
        DrawOverride(selectedEditor, "Selected");
        DrawOverride(clickedEditor,  "Clicked");
        if (interactionTargetProperty.objectReferenceValue)
            DrawOverride(disabledEditor, "Disabled");

        for (var index = 0; index < customEditors.Count; index++)
            if (DrawCustomOverride(customEditors[index], index))
                break;

        if (GUILayout.Button("Add Custom Override"))
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(target, "Add Custom Override");
            ((ParaformStateAnimator)target).customOverrides.Add(new ParaformStateAnimator.ParaformOverride());
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
            serializedObject.Update();
            BuildEditors();
        }

        if (serializedObject.ApplyModifiedProperties())
            BuildEditors();
    }

    bool DrawCustomOverride(OverrideEditor entry, int index)
    {
        EditorGUILayout.Space();
        using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);

        bool moveUp;
        bool moveDown;
        bool remove;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(entry.customStateName);
            moveUp   = GUILayout.Button("↑", EditorStyles.miniButton, GUILayout.Width(20));
            moveDown = GUILayout.Button("↓", EditorStyles.miniButton, GUILayout.Width(20));
            remove   = GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(24));
        }
        if (moveUp && index > 0)
        {
            customOverridesProperty.MoveArrayElement(index, index - 1);
            return true;
        }
        if (moveDown && index < customEditors.Count - 1)
        {
            customOverridesProperty.MoveArrayElement(index, index + 1);
            return true;
        }
        if (remove)
        {
            customOverridesProperty.DeleteArrayElementAtIndex(index);
            return true;
        }

        DrawOverrideContents(entry);
        return false;
    }

    void DrawOverride(OverrideEditor entry, string title)
    {
        EditorGUILayout.Space();
        using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
        bool      clickedHeader;
        bool      expanded;
        using (new EditorGUILayout.HorizontalScope())
        {
            clickedHeader = GUILayout.Button(title, EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(16)))
            {
                GUILayout.Space(3);
                expanded = GUILayout.Toggle(entry.expanded, GUIContent.none, EditorStyles.foldout, GUILayout.Width(16));
            }
        }
        EditorGUILayout.Space(3);
        if (clickedHeader)
            expanded = !entry.expanded;
        if (expanded != entry.expanded)
        {
            entry.expanded = expanded;
            EditorPrefs.SetBool(entry.foldoutKey, expanded);
        }
        if (entry.expanded)
            DrawOverrideContents(entry);
    }

    void DrawOverrideContents(OverrideEditor entry)
    {
        EditorGUILayout.PropertyField(entry.overrideForm);
        if (entry.overrideForm.boolValue)
            entry.config.DrawShapeControls(false);

        entry.config.DrawEffects();
    }

    void ShowEffectMenu(string propertyPath, Rect buttonRect)
    {
        var host             = ((Component)target).GetComponent<IParaformHost>();
        var effects          = host.ParaformConfig.Effects;
        var menu             = new GenericMenu();
        var hasUnnamedEffect = false;
        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            if (effect == null)
                continue;

            if (string.IsNullOrWhiteSpace(effect.name))
            {
                hasUnnamedEffect = true;
                continue;
            }

            menu.AddItem(new GUIContent(effect.name), false, () => AddEffect(propertyPath, effect));
        }

        if (hasUnnamedEffect)
            menu.AddDisabledItem(new GUIContent("Unnamed effects can't be overwritten"));

        menu.DropDown(buttonRect);
    }

    void AddEffect(string propertyPath, Effect effect)
    {
        var serialized = new SerializedObject(target);
        var effects = serialized.FindProperty(propertyPath)
                                .FindPropertyRelative(nameof(ParaformStateAnimator.ParaformOverride.config))
                                .FindPropertyRelative(nameof(ParaformConfig.effects));
        effects.arraySize++;
        effects.GetArrayElementAtIndex(effects.arraySize - 1).managedReferenceValue = effect.Clone();
        serialized.ApplyModifiedProperties();
    }
}

class PlayModeStateSave : ScriptableSingleton<PlayModeStateSave>
{
    [Serializable]
    sealed class SavedComponent
    {
        public ParaformStateAnimator source;
        public string                objectName;
        public string                globalId;
        public string                json;
    }

    [SerializeField] List<SavedComponent> savedComponents = new();

    [InitializeOnLoadMethod]
    static void Initialize()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static bool IsSaved(ParaformStateAnimator animator)
    {
        var saved = instance.savedComponents;
        for (var i = 0; i < saved.Count; i++)
            if (saved[i].source == animator)
                return true;
        return false;
    }

    public static void Toggle(ParaformStateAnimator animator)
    {
        var saved = instance.savedComponents;
        for (var i = 0; i < saved.Count; i++)
            if (saved[i].source == animator)
            {
                saved.RemoveAt(i);
                return;
            }

        saved.Add(new SavedComponent {
            source     = animator,
            objectName = animator.name,
            globalId   = GlobalObjectId.GetGlobalObjectIdSlow(animator).ToString()
        });
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            foreach (var saved in instance.savedComponents)
            {
                if (saved.json != null)
                    continue;

                var animator = saved.source;
                if (animator)
                {
                    try
                    {
                        saved.json = EditorJsonUtility.ToJson(animator, true);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"Could not capture Play mode changes for {saved.objectName}: {exception}");
                    }
                }
                else
                    Debug.LogWarning($"Could not capture Play mode changes for {saved.objectName}: the component could not be found.");
            }
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            var saved = instance.savedComponents;
            for (var index = saved.Count - 1; index >= 0; index--)
            {
                var entry = saved[index];
                if (entry.json == null)
                {
                    WarnRestoreFailure(entry, "no Play mode changes were captured");
                    continue;
                }

                var animator = Resolve(entry);
                if (!animator)
                {
                    WarnRestoreFailure(entry, "the component could not be found");
                    continue;
                }

                try
                {
                    Apply(entry, animator);
                    saved.RemoveAt(index);
                }
                catch (Exception exception)
                {
                    WarnRestoreFailure(entry, exception.ToString());
                }
            }
        }
    }

    static void WarnRestoreFailure(SavedComponent saved, string reason)
    {
        Debug.LogWarning($"Could not restore Play mode changes for {saved.objectName}: {reason}. Component data:\n{saved.json}");
    }

    static void Apply(SavedComponent saved, ParaformStateAnimator animator)
    {
        Undo.RecordObject(animator, "Save State Animator Play Changes");
        EditorJsonUtility.FromJsonOverwrite(saved.json, animator);
        PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        EditorUtility.SetDirty(animator);
    }

    static ParaformStateAnimator Resolve(SavedComponent saved)
    {
        if (!ReferenceEquals(saved.source, null))
        {
#if UNITY_6000_3_OR_NEWER
            var animator = EditorUtility.EntityIdToObject(saved.source.GetEntityId()) as ParaformStateAnimator;
#else
            var animator = EditorUtility.InstanceIDToObject(saved.source.GetInstanceID()) as ParaformStateAnimator;
#endif
            if (animator)
                return animator;
        }

        return GlobalObjectId.TryParse(saved.globalId, out var globalId)
            ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as ParaformStateAnimator
            : null;
    }
}
}
