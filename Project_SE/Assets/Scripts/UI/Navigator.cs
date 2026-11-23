using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EnglishApp.UI
{
    /// <summary>화면 스택 (밀어 넣기 / 꺼내기, 좌우 슬라이드). 화면은 UIPrefabs 의 프리팹으로 만든다.</summary>
    public class Navigator
    {
        private const float Duration = 0.24f;

        private readonly RectTransform layer;
        private readonly UIPrefabs prefabs;
        private readonly MonoBehaviour runner;
        private readonly List<BaseScreen> stack = new List<BaseScreen>();
        private bool animating;

        public event Action Changed;

        public Navigator(RectTransform layer, UIPrefabs prefabs, MonoBehaviour runner)
        {
            this.layer = layer;
            this.prefabs = prefabs;
            this.runner = runner;
        }

        public BaseScreen Top => stack.Count > 0 ? stack[stack.Count - 1] : null;
        public int Count => stack.Count;

        public T Push<T>(Action<T> setup = null) where T : BaseScreen
        {
            var screen = UIKit.Spawn(prefabs.Screen<T>(), layer);
            var rt = screen.Root.Fill();
            setup?.Invoke(screen);
            screen.Init();

            var prev = Top;
            stack.Add(screen);
            Changed?.Invoke();
            screen.OnShow();

            if (prev == null)
                return screen;

            float w = layer.rect.width;
            runner.StartCoroutine(Slide(rt, w, 0f, null));
            runner.StartCoroutine(Slide(prev.Root, 0f, -w * 0.3f, () => prev.gameObject.SetActive(false)));
            return screen;
        }

        /// <summary>맨 위 화면을 새 화면으로 바꾼다 (새 화면이 오른쪽에서 밀려 들어오고, 이전 화면은 닫힘).</summary>
        public T Replace<T>(Action<T> setup = null) where T : BaseScreen
        {
            if (stack.Count <= 1) return Push(setup);
            var old = Top;
            stack.RemoveAt(stack.Count - 1);
            var screen = UIKit.Spawn(prefabs.Screen<T>(), layer);
            var rt = screen.Root.Fill();
            setup?.Invoke(screen);
            screen.Init();
            stack.Add(screen);
            Changed?.Invoke();
            screen.OnShow();

            float w = layer.rect.width;
            runner.StartCoroutine(Slide(rt, w, 0f, null));
            runner.StartCoroutine(Slide(old.Root, 0f, -w * 0.3f, () => UnityEngine.Object.Destroy(old.gameObject)));
            return screen;
        }

        public void Pop()
        {
            if (stack.Count <= 1 || animating) return;
            var top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            var prev = Top;
            prev.gameObject.SetActive(true);
            Changed?.Invoke();
            prev.OnShow();

            float w = layer.rect.width;
            runner.StartCoroutine(Slide(prev.Root, -w * 0.3f, 0f, null));
            runner.StartCoroutine(Slide(top.Root, 0f, w, () => UnityEngine.Object.Destroy(top.gameObject)));
        }

        /// <summary>같은 종류의 화면이 스택에 있으면 그 위를 모두 닫는다.</summary>
        public bool PopTo<T>(Func<T, bool> match) where T : BaseScreen
        {
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                var s = stack[i] as T;
                if (s != null && match(s))
                {
                    while (stack.Count - 1 > i)
                    {
                        var top = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        UnityEngine.Object.Destroy(top.gameObject);
                    }
                    stack[i].gameObject.SetActive(true);
                    stack[i].Root.anchoredPosition = Vector2.zero;
                    Changed?.Invoke();
                    stack[i].OnShow();
                    return true;
                }
            }
            return false;
        }

        private IEnumerator Slide(RectTransform rt, float fromX, float toX, Action done)
        {
            animating = true;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Duration)
            {
                if (rt == null) yield break;
                float e = 1f - Mathf.Pow(1f - t, 3f); // ease-out
                rt.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, e), 0);
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = new Vector2(toX, 0);
            animating = false;
            done?.Invoke();
        }
    }
}
