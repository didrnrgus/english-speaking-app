using System.Linq;
using LeTai.Asset.TranslucentImage;
using LeTai.Common;
using LeTai.Paraform.Scaffold;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LeTai.Paraform.Demo
{
public class DemoEffectStack : MonoBehaviour
{
    public TranslucentImage target;
    public float            duration = .2f;
    public Ease             ease     = Ease.QuadInOut;
    public string            defaultEffectName = "Big Shadow";

    Toggle[]            _toggles;
    Effect[]            _effects;
    UnityAction<bool>[] _toggleListeners;
    float[]             _fullAlphas;
    float[]             _fromAlphas;
    float[]             _toAlphas;
    float[]             _elapsed;
    bool[]              _animating;

    void Awake()
    {
        _toggles = GetComponentsInChildren<Toggle>(true);
        var sourceEffects = target.paraformConfig.Effects;
        _effects         = new Effect[sourceEffects.Count];
        _toggleListeners = new UnityAction<bool>[_toggles.Length];
        _fullAlphas      = new float[sourceEffects.Count];
        _fromAlphas      = new float[sourceEffects.Count];
        _toAlphas        = new float[sourceEffects.Count];
        _elapsed         = new float[sourceEffects.Count];
        _animating       = new bool[sourceEffects.Count];

        for (var index = 0; index < sourceEffects.Count; index++)
        {
            _effects[index] = sourceEffects[index];
            var effectIndex = index;
            _toggleListeners[index] = active => SetEffectState(effectIndex, active);
        }
    }

    void OnEnable()
    {
        for (var index = 0; index < _toggles.Length; index++)
            _toggles[index].onValueChanged.AddListener(_toggleListeners[index]);
    }

    void Start()
    {
        using var edit = target.paraformConfig.EditEffects();

        int defaultIndex = edit.effects.FindIndex(effect => effect.name == defaultEffectName);
        for (var index = 0; index < _effects.Length; index++)
        {
            var effect = edit.effects[index];
            _fullAlphas[index] = effect.color.a;
            effect.enabled     = index == defaultIndex;
            var color = effect.color;
            color.a      = index == defaultIndex ? _fullAlphas[index] : 0;
            effect.color = color;
            _toggles[index].GetComponentInChildren<TextMeshProUGUI>(true).text = effect.name;
            _toggles[index].SetIsOnWithoutNotify(effect.enabled);
        }
    }

    void OnDisable()
    {
        if (_toggles == null)
            return;

        for (var index = 0; index < _toggles.Length; index++)
            _toggles[index].onValueChanged.RemoveListener(_toggleListeners[index]);
    }

    void SetEffectState(int index, bool active)
    {
        using var edit   = target.paraformConfig.EditEffects();
        var       effect = edit.effects[index];
        var       color  = effect.color;
        _fromAlphas[index] = color.a;
        _toAlphas[index]   = active ? _fullAlphas[index] : 0;
        _elapsed[index]    = 0;
        effect.enabled     = active || color.a > 0;
        _animating[index]  = duration > 0 && !Mathf.Approximately(_fromAlphas[index], _toAlphas[index]);

        if (_animating[index])
            return;

        color.a        = _toAlphas[index];
        effect.color   = color;
        effect.enabled = _toAlphas[index] > 0;
    }

    void Update()
    {
        var hasAnimation = false;
        for (var index = 0; index < _animating.Length; index++)
            hasAnimation |= _animating[index];
        if (!hasAnimation)
            return;

        using var edit = target.paraformConfig.EditEffects();
        for (var index = 0; index < _animating.Length; index++)
        {
            if (!_animating[index])
                continue;

            _elapsed[index] += Time.deltaTime;
            var progress = Mathf.Min(1, _elapsed[index] / duration);
            var effect   = edit.effects[index];
            var color    = effect.color;
            color.a      = Mathf.LerpUnclamped(_fromAlphas[index], _toAlphas[index], Easing.Eval(ease, progress));
            effect.color = color;

            if (progress < 1)
                continue;

            _animating[index] = false;
            effect.enabled    = _toAlphas[index] > 0;
        }
    }
}
}
