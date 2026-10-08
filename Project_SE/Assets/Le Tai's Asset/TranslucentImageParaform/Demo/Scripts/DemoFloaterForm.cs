// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using System.Collections;
using System.Collections.Generic;
using LeTai.Asset.TranslucentImage;
using LeTai.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LeTai.Paraform.Demo
{
public class DemoFloaterForm : MonoBehaviour
{
    static readonly int DISTANCE_RANGE = Shader.PropertyToID("_DistanceRange");
    static readonly int SPRITE_UV_RECT = Shader.PropertyToID("_SpriteUVRect");
    static readonly int SPRITE_SIZE    = Shader.PropertyToID("_SpriteSize");

    RectTransform      _rectTransform;
    Vector3            _authoredScale;
    Image[]            _alphaPreviews;
    TranslucentImage[] _finalImages;
    Material[]         _alphaMaterials;
    Color[]            _authoredAlphaColors;
    Vector3[]          _authoredAlphaPreviewScales;
    float[]            _authoredRingThicknesses;
    float[][]          _authoredEffectAlphas;

    static readonly Action<DemoFloaterForm, Vector3> SET_SCALE = static (form, scale) =>
    {
        form._rectTransform.localScale = scale;
    };

    public void Initialize(Shader alphaMorphShader)
    {
        _rectTransform = (RectTransform)transform;
        _authoredScale = _rectTransform.localScale;

        _finalImages = GetComponentsInChildren<TranslucentImage>(true);
        var childImages   = GetComponentsInChildren<Image>(true);
        var alphaPreviews = new List<Image>();
        foreach (var image in childImages)
            if (image is not TranslucentImage)
                alphaPreviews.Add(image);

        _alphaPreviews              = alphaPreviews.ToArray();
        _alphaMaterials             = new Material[_finalImages.Length];
        _authoredAlphaColors        = new Color[_finalImages.Length];
        _authoredAlphaPreviewScales = new Vector3[_finalImages.Length];
        _authoredRingThicknesses    = new float[_finalImages.Length];
        _authoredEffectAlphas       = new float[_finalImages.Length][];
        for (var index = 0; index < _finalImages.Length; index++)
        {
            _authoredRingThicknesses[index]    = _finalImages[index].paraformConfig.RingThickness;
            _alphaMaterials[index]             = new Material(alphaMorphShader);
            _authoredAlphaColors[index]        = _alphaPreviews[index].color;
            _authoredAlphaPreviewScales[index] = _alphaPreviews[index].rectTransform.localScale;
            var effects = _finalImages[index].paraformConfig.Effects;
            _authoredEffectAlphas[index] = new float[effects.Count];
            for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                if (effects[effectIndex] != null)
                    _authoredEffectAlphas[index][effectIndex] = effects[effectIndex].color.a;
            _alphaPreviews[index].material = _alphaMaterials[index];
        }
    }

    public void Play(
        float            punchScale,
        TinyTween.Spring punchSpring,
        float            alphaInitialDelay,
        float            alphaDuration,
        float            sweepDuration,
        float            effectSpeedMul,
        float            sweepKIn,
        float            sweepKOut,
        Color            alphaPreviewTargetColor
    )
    {
        StopAllCoroutines();
        for (var index = 0; index < _finalImages.Length; index++)
        {
            ConfigureAlphaPreview(index);
            _alphaPreviews[index].color = _authoredAlphaColors[index];
            _finalImages[index].gameObject.SetActive(false);
            _alphaPreviews[index].gameObject.SetActive(true);
        }

        TinyTween.Animate(this,
                          _authoredScale * punchScale,
                          _authoredScale,
                          SET_SCALE,
                          punchSpring);
        StartCoroutine(Reveal());

        IEnumerator Reveal()
        {
            var duration = FormSpriteAlphaMorphAnimation.GetDuration(alphaInitialDelay, alphaDuration);
            var start    = Time.time;

            while (true)
            {
                var elapsed = Time.time - start;
                for (var index = 0; index < _finalImages.Length; index++)
                {
                    var source = _finalImages[index].FormAsset.generationParams.source;
                    FormSpriteAlphaMorphAnimation.Apply(_alphaMaterials[index],
                                                        new Vector2(source.width, source.height),
                                                        elapsed,
                                                        alphaInitialDelay,
                                                        alphaDuration);
                }
                if (elapsed >= duration)
                    break;
                yield return null;
            }

            foreach (var finalImage in _finalImages)
                finalImage.paraformConfig.RingThickness = 0;
            start = Time.time;
            yield return null;

            while (true)
            {
                var progress    = Mathf.Clamp01((Time.time - start) / sweepDuration);
                var eased       = EaseInOut(progress, sweepKIn, sweepKOut);
                var effectEased = EaseInOut(Mathf.Clamp01(progress * effectSpeedMul), sweepKIn, sweepKOut);
                for (var index = 0; index < _finalImages.Length; index++)
                {
                    var finalImage = _finalImages[index];
                    finalImage.paraformConfig.RingThickness = _authoredRingThicknesses[index] * eased;
                    var effects = finalImage.paraformConfig.Effects;
                    if (_authoredEffectAlphas[index].Length > 0)
                    {
                        using var edit = finalImage.paraformConfig.EditEffects();
                        for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                        {
                            var effect = effects[effectIndex];
                            if (effect == null)
                                continue;

                            var effectColor = effect.color;
                            effectColor.a = _authoredEffectAlphas[index][effectIndex] * effectEased;
                            effect.color = effectColor;
                        }
                    }
                    finalImage.gameObject.SetActive(true);
                    _alphaPreviews[index].color = Color.Lerp(_authoredAlphaColors[index],
                                                             alphaPreviewTargetColor,
                                                             eased);
                }

                if (progress >= 1)
                    break;
                yield return null;
            }

            foreach (var alphaPreview in _alphaPreviews)
                alphaPreview.gameObject.SetActive(false);
        }
    }

    static float EaseInOut(float progress, float kIn, float kOut)
    {
        var split = kIn / (kIn + kOut);
        return progress < split
            ? .5f * Mathf.Pow(progress / split, kIn)
            : 1 - .5f * Mathf.Pow((1 - progress) / (1 - split), kOut);
    }

    void ConfigureAlphaPreview(int index)
    {
        var finalImage      = _finalImages[index];
        var alphaPreview    = _alphaPreviews[index];
        var alphaMaterial   = _alphaMaterials[index];
        var form            = finalImage.FormAsset;
        var generatedSprite = form.FormSprite;
        var finalDrawingRect   = ParaformUtils.GetFormSpriteDrawingRect(finalImage.GetPixelAdjustedRect(),
                                                                         form,
                                                                         finalImage.rectTransform.pivot);
        var previewRect        = alphaPreview.GetPixelAdjustedRect();
        var finalContentScale  = Mathf.Min(finalDrawingRect.width / form.ContentSize.x,
                                           finalDrawingRect.height / form.ContentSize.y);
        var previewSpriteScale = Mathf.Min(previewRect.width / form.TextureSize.x,
                                           previewRect.height / form.TextureSize.y);
        alphaPreview.rectTransform.localScale = _authoredAlphaPreviewScales[index] *
                                                (finalContentScale / previewSpriteScale);

        var texture         = generatedSprite.texture;
        var textureRect     = generatedSprite.textureRect;
        var uvRect = new Rect(textureRect.x / texture.width,
                              textureRect.y / texture.height,
                              textureRect.width / texture.width,
                              textureRect.height / texture.height);

        alphaPreview.sprite = generatedSprite;
        alphaMaterial.SetVector(SPRITE_UV_RECT,
                                new Vector4(uvRect.x, uvRect.y, uvRect.width, uvRect.height));
        alphaMaterial.SetVector(SPRITE_SIZE,
                                new Vector4(textureRect.width, textureRect.height, 0, 0));
        alphaMaterial.SetFloat(DISTANCE_RANGE, form.generationParams.distanceRange);
    }

    void OnDestroy()
    {
        foreach (var alphaMaterial in _alphaMaterials)
            if (alphaMaterial)
                Destroy(alphaMaterial);
    }
}
}
