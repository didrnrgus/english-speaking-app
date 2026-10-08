// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System.Collections;
using LeTai.Asset.TranslucentImage;
using LeTai.Common;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using Pointer = UnityEngine.InputSystem.Pointer;
#endif

namespace LeTai.Paraform.Demo
{
public class DemoControllerFloater : MonoBehaviour
{
    public RectTransform floater;

    public TinyTween.Spring springFollow = TinyTween.Spring.DEFAULT;

    public TinyTween.Spring springExit = TinyTween.Spring.DEFAULT;

    public float            punchScale  = .8f;
    public TinyTween.Spring springPunch = TinyTween.Spring.DEFAULT;

    public float alphaInitialDelay       = .25f;
    public float alphaDuration           = .35f;
    public float sweepDuration           = .5f;
    public float effectSpeedMul          = 2;
    public float sweepKIn                = 4;
    public float sweepKOut               = 4;
    public Color alphaPreviewTargetColor = new(1, 1, 1, 0);
    public float defaultRevealDuration   = .5f;

    public Shader           alphaMorphShader;
    public TranslucentImage defaultForm;

    public RectTransform boundUI;

    RectTransform _parent;
    Camera        _cam;
    GameObject    _activeForm;
    Vector2       _restPos;
    bool          _isOffscreen;
    Coroutine     _defaultReveal;
    Vector3       _defaultScale;
    Color         _defaultColor;
    float         _defaultBevelWidth;
    float         _defaultElevation;
    float         _defaultBrightness;

    RectTransform canvasRt;
    float         ExitOffset => canvasRt.rect.height * 1.2f;

    void Start()
    {
        _parent = floater.parent as RectTransform;
        _cam    = boundUI.GetComponentInParent<Canvas>().worldCamera;

        floater.gameObject.SetActive(true);
        _restPos = floater.anchoredPosition;

        foreach (var image in floater.GetComponentsInChildren<TranslucentImage>(true))
            image.raycastTarget = Application.isMobilePlatform;

        foreach (var form in floater.GetComponentsInChildren<DemoFloaterForm>(true))
        {
            form.Initialize(alphaMorphShader);
            form.gameObject.SetActive(false);
        }
        defaultForm.gameObject.SetActive(true);
        _activeForm        = defaultForm.gameObject;
        _defaultScale      = defaultForm.rectTransform.localScale;
        _defaultColor      = defaultForm.color;
        _defaultBevelWidth = defaultForm.paraformConfig.BevelWidth;
        _defaultElevation  = defaultForm.paraformConfig.Elevation;
        _defaultBrightness = defaultForm.brightness;

        canvasRt = (RectTransform)GetComponentInParent<Canvas>().transform;
    }

    public void Show(DemoFloaterForm next)
    {
        StopDefaultReveal();

        Activate(next.gameObject);
        next.Play(punchScale,
                  springPunch,
                  alphaInitialDelay,
                  alphaDuration,
                  sweepDuration,
                  effectSpeedMul,
                  sweepKIn,
                  sweepKOut,
                  alphaPreviewTargetColor);
    }

    public void ShowDefault()
    {
        StopDefaultReveal();
        defaultForm.paraformConfig.RingThickness = 0;
        SetDefaultReveal(1);
        defaultForm.brightness               = _defaultBrightness;
        defaultForm.rectTransform.localScale = _defaultScale;
        Activate(defaultForm.gameObject);
    }

    public void ShowDefaultAnimated()
    {
        if (_activeForm == defaultForm.gameObject)
            return;

        StopDefaultReveal();
        _activeForm.SetActive(false);
        _activeForm = defaultForm.gameObject;

        defaultForm.paraformConfig.RingThickness = 0;
        SetDefaultReveal(0);
        defaultForm.brightness               = 1;
        defaultForm.rectTransform.localScale = _defaultScale * punchScale;
        _activeForm.SetActive(true);
        TinyTween.Animate(defaultForm.rectTransform,
                          defaultForm.rectTransform.localScale,
                          _defaultScale,
                          static (target, scale) => target.localScale = scale,
                          springPunch);
        _defaultReveal = StartCoroutine(RevealDefault());
    }

    void StopDefaultReveal()
    {
        if (_defaultReveal == null)
            return;

        StopCoroutine(_defaultReveal);
        _defaultReveal = null;
    }

    IEnumerator RevealDefault()
    {
        var start = Time.time;

        while (true)
        {
            var progress = Mathf.Clamp01((Time.time - start) / defaultRevealDuration);
            var eased    = EaseInOut(progress, sweepKIn, sweepKOut);
            SetDefaultReveal(eased);

            if (progress >= 1)
                break;
            yield return null;
        }

        _defaultReveal = null;
    }

    void SetDefaultReveal(float progress)
    {
        var color = _defaultColor;
        color.a                               *= progress;
        defaultForm.color                     =  color;
        defaultForm.paraformConfig.BevelWidth =  _defaultBevelWidth * progress;
        defaultForm.paraformConfig.Elevation  =  _defaultElevation * progress;
        defaultForm.brightness                =  Mathf.Lerp(1, _defaultBrightness, progress);
    }

    static float EaseInOut(float progress, float kIn, float kOut)
    {
        var split = kIn / (kIn + kOut);
        return progress < split
            ? .5f * Mathf.Pow(progress / split, kIn)
            : 1 - .5f * Mathf.Pow((1 - progress) / (1 - split), kOut);
    }

    void Activate(GameObject next)
    {
        if (_activeForm != next)
            _activeForm.SetActive(false);

        _activeForm = next;
        _activeForm.SetActive(true);
    }

    void Update()
    {
        if (_isOffscreen)
            return;

        Vector2 target = _restPos;
#if ENABLE_INPUT_SYSTEM
        Vector2 pointer = Pointer.current.position.ReadValue();
#else
        Vector2 pointer = Input.mousePosition;
#endif
        if (RectTransformUtility.RectangleContainsScreenPoint(boundUI, pointer, _cam))
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, pointer, _cam, out var mouseLocal))
            {
                target = mouseLocal;
            }
        }

        TinyTween.Move(floater, target, springFollow);
    }

    public void AnimateIn()
    {
        _isOffscreen = false;
        TinyTween.Move(floater, _restPos, springExit);
    }

    public void AnimateOut()
    {
        _isOffscreen = true;
        TinyTween.Move(floater, new Vector2(floater.anchoredPosition.x, _restPos.y + ExitOffset), springExit);
    }
}
}
