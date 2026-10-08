// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System.Collections.Generic;
using UnityEditor;
using LeTai.Common.Editor;
using UnityEngine;

namespace LeTai.Paraform.Editor
{
[CustomEditor(typeof(FormAsset))]
public class FormAssetEditor : UnityEditor.Editor
{
    static readonly EditorCustom.Fold<FormAssetGenerationParams> GENERATION_FOLD = new("FormAsset");

    static readonly int DISTANCE_RANGE = Shader.PropertyToID("_DistanceRange");
    static readonly int GRADIENT_MORPH = Shader.PropertyToID("_GradientMorph");
    static readonly int SRC_BLEND      = Shader.PropertyToID("_SrcBlend");
    static readonly int DST_BLEND      = Shader.PropertyToID("_DstBlend");

    static readonly HashSet<FormAsset> PENDING_PREVIEW_ANIMATIONS = new();

    const float ALPHA_INITIAL_DELAY  = .1f;
    const float ALPHA_STAGE_DURATION = .35f;
    const float GRADIENT_DURATION    = .45f;

    static readonly float ALPHA_DURATION             = FormSpriteAlphaMorphAnimation.GetDuration(ALPHA_INITIAL_DELAY, ALPHA_STAGE_DURATION);
    static readonly float GRADIENT_DELAY             = ALPHA_DURATION + -.1f;
    static readonly float PREVIEW_ANIMATION_DURATION = GRADIENT_DELAY + GRADIENT_DURATION;

    Material           previewMaterial;
    SerializedProperty generationParams;
    SerializedProperty source;
    double             previewAnimationStart = double.NegativeInfinity;
    bool               animateAlpha;
    bool               previewAnimationFinished = true;

    void OnEnable()
    {
        generationParams = serializedObject.FindProperty(nameof(FormAsset.generationParams));
        source           = generationParams.FindPropertyRelative(nameof(FormAssetGenerationParams.source));
    }

    void OnDisable()
    {
        if (previewMaterial)
            DestroyImmediate(previewMaterial);
    }

    internal static void RequestPreviewAnimation(FormAsset form)
    {
        PENDING_PREVIEW_ANIMATIONS.Add(form);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var previousSource = source.objectReferenceValue;

        EditorGUI.BeginChangeCheck();
        DrawGenerationParams();

        var changed       = EditorGUI.EndChangeCheck();
        var sourceChanged = changed && source.objectReferenceValue != previousSource;
        serializedObject.ApplyModifiedProperties();

        bool save;
        bool saveReadonly;
        using (new EditorGUILayout.HorizontalScope())
        {
            save         = GUILayout.Button("Save");
            saveReadonly = GUILayout.Button("Save Readonly");
        }

        var form = (FormAsset)serializedObject.targetObject;
        if (changed || save)
            FormAssetEditorUtil.Generate(form);
        if (sourceChanged)
        {
            RequestPreviewAnimation(form);
            Repaint();
        }
        if (changed && !save && !saveReadonly)
            FormAssetEditorUtil.ScheduleSave(form);
        if (save)
            FormAssetEditorUtil.Save(form);
        if (saveReadonly)
            FormAssetEditorUtil.SaveReadonly(form);

        using (new EditorGUI.DisabledScope(!form.generationParams.source))
            if (GUILayout.Button("Match Source Name"))
                FormAssetEditorUtil.MatchSourceName(form);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(
                "Most Sprites should work well with little tuning. " +
                "If you have a Sprite that's producing objectionable artifacts, let me know",
                EditorStyles.wordWrappedLabel);
            if (EditorGUILayout.LinkButton("Report a bug ↗"))
                Application.OpenURL("https://leloctai.com/support/");
        }

        EditorGUILayout.HelpBox(
            "When packing a Form Sprite into an Atlas the following settings are required:\n" +
            "Tight Packing: Off\n" +
            "Alpha Dilation: Off\n" +
            "Generate Mip Maps: Off\n" +
            "sRGB: Off\n" +
            "Filter Mode: Bilinear\n" +
            "Compression: None\n" +
            "Read/Write is required if you want to Use Precise Hit Test",
            MessageType.None);
    }

    void DrawGenerationParams()
    {
        using var property      = generationParams.Copy();
        var       depth         = property.depth;
        var       enterChildren = true;
        while (property.NextVisible(enterChildren) && property.depth > depth)
        {
            enterChildren = false;
            if (!GENERATION_FOLD.DrawHeader(property.name))
                break;
            EditorGUILayout.PropertyField(property, true);
        }
    }

    public override bool HasPreviewGUI()
    {
        return ((FormAsset)target).FormSprite;
    }

    public override void OnPreviewGUI(Rect rect, GUIStyle background)
    {
        var form    = (FormAsset)target;
        var texture = FormAssetEditorUtil.OwnedTexture(form);

        if (!texture)
            return;

        if (PENDING_PREVIEW_ANIMATIONS.Remove(form))
            previewAnimationStart = EditorApplication.timeSinceStartup;

        previewAnimationFinished = ConfigurePreview(form, texture, false) >= 1;
        EditorGUI.DrawPreviewTexture(rect, texture, previewMaterial, ScaleMode.ScaleToFit);
    }

    float ConfigurePreview(FormAsset form, Texture texture, bool staticPreview)
    {
        EnsurePreviewMaterial();

        var source = form.generationParams.source;
        animateAlpha = source && (source.width < texture.width || source.height < texture.height);
        var animationElapsed = staticPreview ? PREVIEW_ANIMATION_DURATION : PreviewAnimationElapsed;
        previewMaterial.SetInt(SRC_BLEND, (int)(staticPreview ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
        previewMaterial.SetInt(DST_BLEND, (int)(staticPreview ? UnityEngine.Rendering.BlendMode.Zero : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        var gradientDelay = animateAlpha ? GRADIENT_DELAY : 0;
        var sourceSize = source
            ? new Vector2(source.width,  source.height)
            : new Vector2(texture.width, texture.height);
        var alphaElapsed = animateAlpha ? animationElapsed : ALPHA_DURATION;
        previewMaterial.SetFloat(DISTANCE_RANGE, form.generationParams.distanceRange);
        FormSpriteAlphaMorphAnimation.Apply(previewMaterial,
                                            sourceSize,
                                            alphaElapsed,
                                            ALPHA_INITIAL_DELAY,
                                            ALPHA_STAGE_DURATION);
        var gradientMorph = StageMorph(animationElapsed, gradientDelay, GRADIENT_DURATION);
        previewMaterial.SetFloat(GRADIENT_MORPH, gradientMorph);
        return gradientMorph;
    }

    public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
    {
        var form    = (FormAsset)target;
        var texture = FormAssetEditorUtil.OwnedTexture(form);

        if (!texture)
            return null;

        ConfigurePreview(form, texture, true);

        var previousActive    = RenderTexture.active;
        var previousSRGBWrite = GL.sRGBWrite;
        var renderTexture = RenderTexture.GetTemporary(width, height, 0,
                                                       RenderTextureFormat.ARGB32,
                                                       RenderTextureReadWrite.sRGB);
        GL.PushMatrix();
        try
        {
            var rect = new Rect(0, 0, width, height);
            RenderTexture.active = renderTexture;
            GL.sRGBWrite         = QualitySettings.activeColorSpace == ColorSpace.Linear;
            GL.Clear(true, true, Color.clear);

            var scale = Mathf.Min((float)width / texture.width, (float)height / texture.height);
            rect.width  = texture.width * scale;
            rect.height = texture.height * scale;
            rect.center = new Vector2(width, height) * .5f;

            GL.LoadPixelMatrix(0, width, height, 0);
            Graphics.DrawTexture(rect, texture, previewMaterial);

            var preview = new Texture2D(width, height, TextureFormat.RGBA32, false);
            preview.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            preview.Apply();
            return preview;
        }
        finally
        {
            GL.PopMatrix();
            GL.sRGBWrite         = previousSRGBWrite;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    public override bool RequiresConstantRepaint()
    {
        return !previewAnimationFinished;
    }

    static float StageMorph(float elapsed, float delay, float duration)
    {
        return Mathf.Clamp01((elapsed - delay) / duration);
    }

    float PreviewAnimationElapsed => Mathf.Clamp(
        (float)(EditorApplication.timeSinceStartup - previewAnimationStart),
        0,
        PreviewAnimationDuration
    );

    float PreviewAnimationDuration => animateAlpha ? PREVIEW_ANIMATION_DURATION : GRADIENT_DURATION;

    void EnsurePreviewMaterial()
    {
        if (previewMaterial)
            return;

        previewMaterial = new Material(Shader.Find("Hidden/Paraform/Form-Sprite-Preview")) {
            hideFlags = HideFlags.HideAndDontSave
        };
    }
}
}
