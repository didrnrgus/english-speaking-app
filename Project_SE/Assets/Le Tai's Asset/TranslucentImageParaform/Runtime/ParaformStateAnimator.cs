using System;
using System.Collections.Generic;
using LeTai.Common;
using LeTai.Paraform.Scaffold;
using UnityEngine;

namespace LeTai.Paraform
{
public class ParaformStateAnimator : UIStateAnimator
{
    [Serializable]
    internal sealed class ParaformOverride : Override
    {
        public bool           overrideForm;
        public ParaformConfig config = ParaformConfig.DEFAULT;
    }

    [SerializeField] internal ParaformOverride       normalOverride   = new();
    [SerializeField] internal ParaformOverride       hoverOverride    = new();
    [SerializeField] internal ParaformOverride       selectedOverride = new();
    [SerializeField] internal ParaformOverride       clickedOverride  = new();
    [SerializeField] internal ParaformOverride       disabledOverride = new();
    [SerializeField] internal List<ParaformOverride> customOverrides  = new();

    IParaformHost  _host;
    ParaformConfig _baselineForm;
    ParaformConfig _fromForm;
    ParaformConfig _toForm;
    Effect[]       _fromEffects = Array.Empty<Effect>();
    Effect[]       _toEffects   = Array.Empty<Effect>();
    float          _elapsed;
    bool           _active;

    volatile bool _refreshState;

    protected override Override                NormalOverride   => normalOverride;
    protected override Override                HoverOverride    => hoverOverride;
    protected override Override                SelectedOverride => selectedOverride;
    protected override Override                ClickedOverride  => clickedOverride;
    protected override Override                DisabledOverride => disabledOverride;
    protected override IReadOnlyList<Override> CustomOverrides  => customOverrides;

    void OnEnable()
    {
        CaptureBaseline();
        InitializeState();
    }

    void OnDisable()
    {
        RestoreBaseline();
    }

    void OnValidate()
    {
        _refreshState = true;
    }

    void InitializeReferences()
    {
        if (!(_host as Component))
            _host = GetComponent<IParaformHost>();
    }

    void RestoreBaseline()
    {
        _toForm = _baselineForm;
        var baselineEffects = _baselineForm.Effects;
        for (var index = 0; index < _toEffects.Length; index++)
            _toEffects[index] = baselineEffects[index];
        Apply(1);
        _active = false;
    }

    public override void CaptureBaseline()
    {
        InitializeReferences();
        _baselineForm = _host.ParaformConfig.Snapshot();
        InitializeEffectBuffers();
        _active = false;
    }

    void InitializeEffectBuffers()
    {
        var liveEffects = _host.ParaformConfig.Effects;
        _fromEffects = new Effect[liveEffects.Count];
        _toEffects   = new Effect[liveEffects.Count];
        for (var index = 0; index < liveEffects.Count; index++)
            _fromEffects[index] = liveEffects[index]?.Clone();
    }

    static Effect FindOverride(IReadOnlyList<Effect> effects, Effect target)
    {
        if (effects == null || string.IsNullOrWhiteSpace(target.name))
            return null;

        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            if (effect == null || effect.name != target.name || effect.GetType() != target.GetType())
                continue;
            return effect;
        }

        return null;
    }

    protected override void Animate(IReadOnlyList<Override> applicableOverrides, bool instant)
    {
        ParaformOverride formOverride = null;
        for (var index = 0; index < applicableOverrides.Count; index++)
        {
            var stateOverride = (ParaformOverride)applicableOverrides[index];
            if (formOverride == null && stateOverride.overrideForm)
                formOverride = stateOverride;
        }

        _fromForm = _host.ParaformConfig;
        _toForm   = formOverride != null ? formOverride.config : _baselineForm;

        var liveEffects = _host.ParaformConfig.Effects;
        // TODO: Reconcile effect buffers when live effects change count, order, or type.
        for (var index = 0; index < _toEffects.Length; index++)
        {
            var liveEffect = liveEffects[index];
            if (liveEffect == null)
                continue;

            _fromEffects[index].Lerp(liveEffect, liveEffect, 1);
            Effect destination = null;
            for (var stateIndex = 0; stateIndex < applicableOverrides.Count; stateIndex++)
            {
                var stateOverride = (ParaformOverride)applicableOverrides[stateIndex];
                destination = FindOverride(stateOverride.config.Effects, liveEffect);
                if (destination != null)
                    break;
            }
            _toEffects[index] = destination ?? _baselineForm.Effects[index];
        }

        _elapsed = 0;
        _active  = !instant;
        Apply(instant ? 1 : 0);
    }

    void Apply(float amount)
    {
        _host.ParaformConfig.Lerp(_fromForm, _toForm, amount);

        using var edit        = _host.ParaformConfig.EditEffects();
        var       liveEffects = _host.ParaformConfig.Effects;
        for (var index = 0; index < _toEffects.Length; index++)
            liveEffects[index]?.Lerp(_fromEffects[index], _toEffects[index], amount);
    }

    void Update()
    {
        if (_refreshState)
        {
            _refreshState = false;
            AnimateState(instant: true);
        }

        if (!_active)
            return;

        _elapsed += Time.deltaTime;
        var progress = duration > 0 ? Mathf.Min(1, _elapsed / duration) : 1;
        _active = progress < 1;
        Apply(_active ? Easing.Eval(ease, progress) : 1);
    }
}
}
