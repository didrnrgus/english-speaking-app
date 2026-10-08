// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using LeTai.Asset.TranslucentImage;
using UnityEngine;
using UnityEngine.UI;

namespace LeTai.Paraform.Demo
{
[RequireComponent(typeof(Button))]
public class DemoFormChange : MonoBehaviour
{
    public DemoFloaterForm target;

    DemoControllerFloater _floaterController;
    Button                _button;

    void Awake()
    {
        _floaterController = Shims.FindAnyObjectByType<DemoControllerFloater>();
        _button            = GetComponent<Button>();
    }

    void OnEnable()
    {
        _button.onClick.AddListener(ChangeForm);
    }

    void OnDisable()
    {
        _button.onClick.RemoveListener(ChangeForm);
    }

    public void ChangeForm()
    {
        if (target)
            _floaterController.Show(target);
        else
            _floaterController.ShowDefaultAnimated();
    }
}
}
