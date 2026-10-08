// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace LeTai.Paraform.Editor
{
public static class FormAssetEditorUtil
{
    const           string             DEFAULT_FILE_NAME = "New Form Asset.paraform.asset";
    const           string             ASSET_NAME_SUFFIX = ".paraform";
    static readonly HashSet<FormAsset> PENDING_SAVES     = new();

    [MenuItem("Assets/Create/Paraform/Form Asset", priority = 100)]
    static void BeginCreate()
    {
        var action = ScriptableObject.CreateInstance<CreateFormAsset>();
        action.source = SelectedSprite();

        var fileName = action.source
            ? $"{FormatAssetName(action.source.name)}.asset"
            : DEFAULT_FILE_NAME;

#if UNITY_6000_4_OR_NEWER
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(EntityId.None, action, fileName, null, null);
#else
        ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, action, fileName, null, null);
#endif
    }

    static Sprite SelectedSprite()
    {
        if (Selection.activeObject is Sprite sprite)
            return sprite;

        var path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static FormAsset Create(string path, Sprite source = null)
    {
        var form = ScriptableObject.CreateInstance<FormAsset>();
        if (source)
            // TODO: actually render the sprite to handle atlas, instead of the whole texture .
            form.generationParams.source = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(source));

        AssetDatabase.CreateAsset(form, AssetDatabase.GenerateUniqueAssetPath(path));
        Generate(form);
        FormAssetEditor.RequestPreviewAnimation(form);
        Save(form);
        return form;
    }

    public static void Generate(FormAsset form)
    {
        if (!form || !form.generationParams.source)
            return;

        var prep        = FormAssetGenerator.Prepare(in form.generationParams);
        var size        = prep.targetSize;
        var destination = RenderTexture.GetTemporary(size.x, size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        try
        {
            FormAssetGenerator.Generate(in form.generationParams, in prep, destination);
            ApplyGenerated(form, in prep, destination);
        }
        finally
        {
            RenderTexture.ReleaseTemporary(destination);
        }
    }

    public static void ApplyGenerated(FormAsset form, in FormAssetGenerator.GenerationPrep prep, RenderTexture generated)
    {
        var formSprite      = form.FormSprite;
        var previousTexture = OwnedTexture(form);
        var isNewTexture    = !previousTexture || !previousTexture.isReadable;
        var texture = isNewTexture
            ? new Texture2D(generated.width, generated.height, TextureFormat.RGBA32, false, true)
            : previousTexture;
        var replaceFormSprite = !formSprite || isNewTexture;

        if (!isNewTexture && (texture.width != generated.width || texture.height != generated.height))
        {
            texture.Reinitialize(generated.width, generated.height);
            replaceFormSprite = true;
        }

        texture.wrapMode = TextureWrapMode.Clamp;

        var prevActive = RenderTexture.active;
        try
        {
            RenderTexture.active = generated;
            texture.ReadPixels(new Rect(0, 0, generated.width, generated.height), 0, 0);
            texture.Apply();
        }
        finally
        {
            RenderTexture.active = prevActive;
        }

        if (replaceFormSprite)
        {
            if (formSprite)
            {
                AssetDatabase.RemoveObjectFromAsset(formSprite);
                Object.DestroyImmediate(formSprite, true);
            }
            if (previousTexture && isNewTexture)
            {
                AssetDatabase.RemoveObjectFromAsset(previousTexture);
                Object.DestroyImmediate(previousTexture, true);
            }

            formSprite = Sprite.Create(texture,
                                       new Rect(0, 0, generated.width, generated.height),
                                       new Vector2(.5f, .5f),
                                       100,
                                       0,
                                       SpriteMeshType.FullRect);
            if (isNewTexture)
                AssetDatabase.AddObjectToAsset(texture, form);
            AssetDatabase.AddObjectToAsset(formSprite, form);
        }

        texture.name    = form.name;
        formSprite.name = form.name;
        form.SetGenerated(formSprite, prep.contentSize, prep.targetSize);

        EditorUtility.SetDirty(texture);
        EditorUtility.SetDirty(formSprite);
        EditorUtility.SetDirty(form);
    }

    internal static Texture2D OwnedTexture(FormAsset form)
    {
        var assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(form));
        foreach (var asset in assets)
            if (asset is Texture2D texture)
                return texture;

        return null;
    }

    public static void ScheduleSave(FormAsset form)
    {
        PENDING_SAVES.Add(form);
        EditorApplication.delayCall -= SavePending;
        EditorApplication.delayCall += SavePending;
    }

    public static void Save(FormAsset form)
    {
        PENDING_SAVES.Remove(form);
        RemoveOrphanedSubassets(form);
        AssetDatabase.SaveAssetIfDirty(form);
    }

    static void RemoveOrphanedSubassets(FormAsset form)
    {
        var sprite = form.FormSprite;
        if (!sprite)
            return;

        var texture = sprite.texture;
        if (!texture)
            return;

        var assets      = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(form));
        var ownsTexture = false;
        foreach (var asset in assets)
            if (asset == texture)
            {
                ownsTexture = true;
                break;
            }
        if (!ownsTexture)
            return;

        var removedAny = false;
        foreach (var asset in assets)
        {
            if (asset == sprite || asset == texture || asset is not (Sprite or Texture2D))
                continue;

            AssetDatabase.RemoveObjectFromAsset(asset);
            Object.DestroyImmediate(asset, true);
            removedAny = true;
        }
        if (removedAny)
            EditorUtility.SetDirty(form);
    }

    public static void SaveReadonly(FormAsset form)
    {
        var texture = OwnedTexture(form);
        if (texture && texture.isReadable)
        {
            texture.Apply(false, true);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssetIfDirty(texture);
        }
        Save(form);
    }

    public static void MatchSourceName(FormAsset form)
    {
        if (!form.generationParams.source)
            return;

        ObjectNames.SetNameSmart(form, FormatAssetName(form.generationParams.source.name));
    }

    static string FormatAssetName(string name)
    {
        return name.EndsWith(ASSET_NAME_SUFFIX) ? name : $"{name}{ASSET_NAME_SUFFIX}";
    }

    static void SavePending()
    {
        foreach (var form in PENDING_SAVES)
            if (form)
                AssetDatabase.SaveAssetIfDirty(form);
        PENDING_SAVES.Clear();
    }

#if UNITY_6000_4_OR_NEWER
    class CreateFormAsset : AssetCreationEndAction
#else
    class CreateFormAsset : EndNameEditAction
#endif
    {
        public Sprite source;

#if UNITY_6000_4_OR_NEWER
        public override void Action(EntityId entityId, string pathName, string resourceFile)
#else
        public override void Action(int instanceId, string pathName, string resourceFile)
#endif
        {
            var form = Create(pathName, source);
            ProjectWindowUtil.ShowCreatedAsset(form);
        }
    }
}
}
