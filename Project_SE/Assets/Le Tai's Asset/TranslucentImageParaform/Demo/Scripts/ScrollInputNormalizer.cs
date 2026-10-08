using UnityEngine;
using UnityEngine.UI;

namespace LeTai.Paraform.Demo
{
public class ScrollInputNormalizer : MonoBehaviour
{
    [System.Serializable]
    public struct PlatformOverride
    {
        public RuntimePlatform platform;

        [Min(0f)]
        public float scrollSensitivity;
    }

    public PlatformOverride[] platformOverrides = System.Array.Empty<PlatformOverride>();

    private ScrollRect _scrollRect;
    private float      _originalScrollSensitivity;

    private void Awake()
    {
        _scrollRect                = GetComponent<ScrollRect>();
        _originalScrollSensitivity = _scrollRect.scrollSensitivity;

        foreach (var platformOverride in platformOverrides)
        {
            if (platformOverride.platform != Application.platform)
                continue;

            _scrollRect.scrollSensitivity = platformOverride.scrollSensitivity;
            break;
        }
    }

    private void OnDestroy()
    {
        if (_scrollRect)
            _scrollRect.scrollSensitivity = _originalScrollSensitivity;
    }

    public void SetScrollSensitivity(float value)
    {
        _scrollRect.scrollSensitivity = value;
    }
}
}
