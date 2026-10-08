using System;
using System.Collections.Generic;
using System.Reflection;
using LeTai.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LeTai.Paraform
{
public abstract class UIStateAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                                        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
{
    enum InteractionSource
    {
        Self,
        Selectable
    }

    enum SelectableState
    {
        Normal,
        Highlighted,
        Selected,
        Pressed,
        Disabled
    }

    static class SelectableStateReader
    {
        static readonly Func<Selectable, bool> IsPointerDown   = CacheGetter("isPointerDown");
        static readonly Func<Selectable, bool> HasSelection    = CacheGetter("hasSelection");
        static readonly Func<Selectable, bool> IsPointerInside = CacheGetter("isPointerInside");

        static Func<Selectable, bool> CacheGetter(string propertyName)
        {
            var getter = typeof(Selectable).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic)
                                           .GetGetMethod(true);
            return (Func<Selectable, bool>)getter.CreateDelegate(typeof(Func<Selectable, bool>));
        }

        public static SelectableState Read(Selectable selectable)
        {
            if (!selectable.IsInteractable())
                return SelectableState.Disabled;
            if (IsPointerDown(selectable))
                return SelectableState.Pressed;
            if (HasSelection(selectable))
                return SelectableState.Selected;
            if (IsPointerInside(selectable))
                return SelectableState.Highlighted;
            return SelectableState.Normal;
        }
    }

    [Serializable]
    public class Override
    {
        public string name;
    }

    public float duration = .2f;
    public Ease  ease     = Ease.QuadInOut;

    [Tooltip("Deselect on pointer up")]
    public bool autoDeselect;

    [Tooltip("Animate using this Selectable's state. Leave empty to use this GameObject's interaction.")]
    public Selectable interactionTarget;

    readonly HashSet<string> _activeCustomStates  = new(StringComparer.Ordinal);
    readonly List<Override>  _applicableOverrides = new(4);

    InteractionSource _interactionSource;
    SelectableState   _selectableState;
    bool              _hovered;
    bool              _selected;
    bool              _clicked;

    InteractionSource Source => interactionTarget ? InteractionSource.Selectable : InteractionSource.Self;

    protected abstract Override                NormalOverride   { get; }
    protected abstract Override                HoverOverride    { get; }
    protected abstract Override                SelectedOverride { get; }
    protected abstract Override                ClickedOverride  { get; }
    protected abstract Override                DisabledOverride { get; }
    protected abstract IReadOnlyList<Override> CustomOverrides  { get; }

    public abstract    void CaptureBaseline();
    protected abstract void Animate(IReadOnlyList<Override> applicableOverrides, bool instant);

    protected virtual void Reset()
    {
        autoDeselect = Input.mousePresent || Input.touchSupported;
    }

    protected void InitializeState()
    {
        _hovered = false;
        _selected = !autoDeselect && EventSystem.current &&
                    EventSystem.current.currentSelectedGameObject == gameObject;
        _clicked = false;
        AnimateState(instant: true);
    }

    protected virtual void LateUpdate()
    {
        var source = Source;
        if (source != _interactionSource ||
            source == InteractionSource.Selectable && SelectableStateReader.Read(interactionTarget) != _selectableState)
            AnimateState();
    }

    public void SetState(string customStateName, bool active, bool instant = false)
    {
        if (active)
        {
            var customOverrides = CustomOverrides;
            var known           = false;
            for (var index = 0; index < customOverrides.Count; index++)
                if (customOverrides[index]?.name == customStateName)
                {
                    if (known)
                        throw new ArgumentException($"Duplicate custom state: {customStateName}", nameof(customStateName));
                    known = true;
                }
            if (!known)
                throw new ArgumentException($"Unknown custom state: {customStateName}", nameof(customStateName));
        }

        var changed = active ? _activeCustomStates.Add(customStateName) : _activeCustomStates.Remove(customStateName);
        if ((changed || instant) && isActiveAndEnabled)
            AnimateState(instant: instant);
    }

    protected void AnimateState(bool instant = false)
    {
        var source = Source;
        _interactionSource = source;
        if (source == InteractionSource.Selectable)
            _selectableState = SelectableStateReader.Read(interactionTarget);

        var  clickedOverride       = ClickedOverride;
        var  disabledOverride      = DisabledOverride;
        var  hoverOverride         = HoverOverride;
        var  selectedOverride      = SelectedOverride;
        var  normalOverride        = NormalOverride;
        bool useSelectable         = source == InteractionSource.Selectable;
        bool isClickedStateActive  = useSelectable ? _selectableState == SelectableState.Pressed : _clicked;
        bool isHoverStateActive    = useSelectable ? _selectableState == SelectableState.Highlighted : _hovered;
        bool isSelectedStateActive = useSelectable ? _selectableState == SelectableState.Selected : _selected;
        bool isDisabledStateActive = useSelectable && _selectableState == SelectableState.Disabled;
        bool isNormalStateActive   = Application.isPlaying;

        _applicableOverrides.Clear();
        if (isDisabledStateActive && disabledOverride != null)
            _applicableOverrides.Add(disabledOverride);
        if (isClickedStateActive && clickedOverride != null)
            _applicableOverrides.Add(clickedOverride);
        if (isHoverStateActive && hoverOverride != null)
            _applicableOverrides.Add(hoverOverride);
        if (isSelectedStateActive && selectedOverride != null)
            _applicableOverrides.Add(selectedOverride);
        var customOverrides = CustomOverrides;
        for (var index = 0; index < customOverrides.Count; index++)
        {
            var  stateOverride       = customOverrides[index];
            bool isCustomStateActive = stateOverride != null && _activeCustomStates.Contains(stateOverride.name);
            if (isCustomStateActive)
                _applicableOverrides.Add(stateOverride);
        }
        if (isNormalStateActive && normalOverride != null)
            _applicableOverrides.Add(normalOverride);
        Animate(_applicableOverrides, instant || duration <= 0);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        if (!interactionTarget)
            AnimateState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        if (!interactionTarget)
            AnimateState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _selected = true;
        if (!interactionTarget)
            AnimateState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _selected = false;
        if (!interactionTarget)
            AnimateState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _clicked = true;
        if (!interactionTarget)
            AnimateState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!interactionTarget && autoDeselect && EventSystem.current &&
            EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);

        _clicked = false;
        if (!interactionTarget)
            AnimateState();
    }
}
}
