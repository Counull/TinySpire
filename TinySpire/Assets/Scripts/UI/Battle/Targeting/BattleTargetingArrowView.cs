using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TinySpire.UI.Battle
{
    /// <summary>以屏幕端点绘制弧形分段箭身与独立箭头的功能性目标指示器；全部 Graphic 永不接收射线。</summary>
    [DisallowMultipleComponent]
    public sealed class BattleTargetingArrowView : MonoBehaviour
    {
        [SerializeField] private RectTransform _coordinateSpace;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Image _fragmentTemplate;
        [SerializeField] private RectTransform _headRect;
        [SerializeField] private Image _headImage;
        [SerializeField, Min(1f)] private float _fragmentLength = 88f;
        [SerializeField, Min(1f)] private float _fragmentThickness = 24f;
        [SerializeField, Min(0f)] private float _fragmentSpacing = 16f;
        [SerializeField, Min(1)] private int _maxFragmentCount = 12;
        [SerializeField, Range(0f, 0.5f)] private float _curveBendRatio = 0.18f;
        [SerializeField, Min(0f)] private float _maxCurveBend = 96f;
        [SerializeField, Range(0f, 0.45f)] private float _headClearanceRatio = 0.16f;

        private readonly List<Image> _fragments = new List<Image>();

        /// <summary>当前功能性箭头是否正在显示。</summary>
        public bool IsVisible => _visualRoot != null && _visualRoot.activeSelf;

        /// <summary>把序列化子 Prefab 提升为同场景独立 Overlay 根，隔离手牌 Canvas 的缩放与深度。</summary>
        public void PrepareAsScreenOverlay()
        {
            EnsureReferences();
            var rectTransform = (RectTransform)transform;
            rectTransform.SetParent(null, worldPositionStays: false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                throw new InvalidOperationException("BattleTargetingArrowView requires a Canvas.");

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            Hide();
        }

        /// <summary>组件初始化时保持箭头隐藏，等待一次明确的 Enemy 瞄准。</summary>
        private void Awake()
        {
            Hide();
        }

        /// <summary>显示箭头并将两个屏幕端点投影为弧形箭身的分段位置与切线朝向。</summary>
        public void Show(Vector2 originScreenPosition, Vector2 targetScreenPosition)
        {
            EnsureReferences();
            SetRaycastTargetsDisabled();
            _visualRoot.SetActive(true);
            if (!TryApplyScreenPoints(originScreenPosition, targetScreenPosition))
                Hide();
        }

        /// <summary>在保持显示时更新端点；坐标无法转换时安全隐藏。</summary>
        public void UpdateArrow(Vector2 originScreenPosition, Vector2 targetScreenPosition)
        {
            if (!IsVisible)
                return;
            if (!TryApplyScreenPoints(originScreenPosition, targetScreenPosition))
                Hide();
        }

        /// <summary>隐藏全部箭头表现并回收箭身片段，不改变任何拖拽或战斗事实。</summary>
        public void Hide()
        {
            HideFragments();
            if (_visualRoot != null)
                _visualRoot.SetActive(false);
        }

        /// <summary>把屏幕端点转换到 Overlay 局部空间，并按三次贝塞尔曲线布置箭身与箭头。</summary>
        private bool TryApplyScreenPoints(
            Vector2 originScreenPosition,
            Vector2 targetScreenPosition)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _coordinateSpace,
                    originScreenPosition,
                    eventCamera,
                    out Vector2 originLocal)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _coordinateSpace,
                    targetScreenPosition,
                    eventCamera,
                    out Vector2 targetLocal))
            {
                return false;
            }

            Vector2 chord = targetLocal - originLocal;
            if (chord.sqrMagnitude < 0.01f)
                return false;

            BuildCurve(originLocal, targetLocal, out Vector2 controlOne, out Vector2 controlTwo);
            ApplyFragments(originLocal, controlOne, controlTwo, targetLocal);
            Vector2 headTangent = EvaluateCubicTangent(
                originLocal,
                controlOne,
                controlTwo,
                targetLocal,
                1f);
            _headRect.anchoredPosition = targetLocal;
            _headRect.localEulerAngles = new Vector3(0f, 0f, ToAngle(headTangent));
            return true;
        }

        /// <summary>由端点构造朝战场上方轻微弯曲的三次贝塞尔曲线，左右目标保持镜像一致的弯曲意图。</summary>
        private void BuildCurve(
            Vector2 origin,
            Vector2 target,
            out Vector2 controlOne,
            out Vector2 controlTwo)
        {
            Vector2 chord = target - origin;
            float length = chord.magnitude;
            Vector2 direction = chord / length;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float bendDirection = Mathf.Abs(direction.x) > 0.01f ? Mathf.Sign(direction.x) : 1f;
            float bend = Mathf.Min(_maxCurveBend, length * _curveBendRatio) * bendDirection;
            Vector2 bendOffset = normal * bend;
            controlOne = origin + chord * 0.28f + bendOffset;
            controlTwo = origin + chord * 0.72f + bendOffset;
        }

        /// <summary>沿弧线按近似等距生成多个固定长度箭身片段，并令每段朝向自身位置的局部切线。</summary>
        private void ApplyFragments(
            Vector2 origin,
            Vector2 controlOne,
            Vector2 controlTwo,
            Vector2 target)
        {
            float curveLength = EstimateCurveLength(origin, controlOne, controlTwo, target);
            float usableLength = curveLength * (1f - _headClearanceRatio);
            int fragmentCount = Mathf.Clamp(
                Mathf.FloorToInt(usableLength / (_fragmentLength + _fragmentSpacing)),
                1,
                _maxFragmentCount);
            EnsureFragmentCount(fragmentCount);
            float firstT = Mathf.Min(0.18f, 0.5f / fragmentCount);
            float lastT = Mathf.Max(firstT, 1f - _headClearanceRatio);
            for (int index = 0; index < _fragments.Count; index++)
            {
                Image fragment = _fragments[index];
                bool isUsed = index < fragmentCount;
                fragment.gameObject.SetActive(isUsed);
                if (!isUsed)
                    continue;

                float progress = fragmentCount == 1
                    ? (firstT + lastT) * 0.5f
                    : Mathf.Lerp(firstT, lastT, index / (float)(fragmentCount - 1));
                RectTransform rect = fragment.rectTransform;
                rect.anchoredPosition = EvaluateCubic(origin, controlOne, controlTwo, target, progress);
                rect.sizeDelta = new Vector2(_fragmentLength, _fragmentThickness);
                rect.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    ToAngle(EvaluateCubicTangent(
                        origin,
                        controlOne,
                        controlTwo,
                        target,
                        progress)));
            }
        }

        /// <summary>按曲线长度与片段间距创建可复用片段池，调用方始终只面对 Show、UpdateArrow 与 Hide。</summary>
        private void EnsureFragmentCount(int requiredCount)
        {
            _fragmentTemplate.gameObject.SetActive(false);
            while (_fragments.Count < requiredCount)
            {
                Image fragment = Instantiate(_fragmentTemplate, _fragmentTemplate.transform.parent);
                fragment.name = $"ShaftFragment_{_fragments.Count + 1:D2}";
                fragment.raycastTarget = false;
                fragment.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                fragment.transform.SetSiblingIndex(_headRect.GetSiblingIndex());
                fragment.gameObject.SetActive(false);
                _fragments.Add(fragment);
            }
        }

        /// <summary>在隐藏或缩短箭头时回收已创建片段，避免残留在下一次瞄准中。</summary>
        private void HideFragments()
        {
            foreach (Image fragment in _fragments)
            {
                if (fragment != null)
                    fragment.gameObject.SetActive(false);
            }

            if (_fragmentTemplate != null)
                _fragmentTemplate.gameObject.SetActive(false);
        }

        /// <summary>估算三次贝塞尔曲线长度，以稳定计算不同距离下的片段密度。</summary>
        private static float EstimateCurveLength(
            Vector2 origin,
            Vector2 controlOne,
            Vector2 controlTwo,
            Vector2 target)
        {
            const int sampleCount = 16;
            float length = 0f;
            Vector2 previous = origin;
            for (int index = 1; index <= sampleCount; index++)
            {
                Vector2 current = EvaluateCubic(
                    origin,
                    controlOne,
                    controlTwo,
                    target,
                    index / (float)sampleCount);
                length += Vector2.Distance(previous, current);
                previous = current;
            }

            return length;
        }

        /// <summary>计算三次贝塞尔曲线在给定进度的局部位置。</summary>
        private static Vector2 EvaluateCubic(
            Vector2 origin,
            Vector2 controlOne,
            Vector2 controlTwo,
            Vector2 target,
            float progress)
        {
            float inverse = 1f - progress;
            return inverse * inverse * inverse * origin
                   + 3f * inverse * inverse * progress * controlOne
                   + 3f * inverse * progress * progress * controlTwo
                   + progress * progress * progress * target;
        }

        /// <summary>计算三次贝塞尔曲线在给定进度的导数，并为退化情况提供稳定朝向。</summary>
        private static Vector2 EvaluateCubicTangent(
            Vector2 origin,
            Vector2 controlOne,
            Vector2 controlTwo,
            Vector2 target,
            float progress)
        {
            float inverse = 1f - progress;
            Vector2 tangent = 3f * inverse * inverse * (controlOne - origin)
                              + 6f * inverse * progress * (controlTwo - controlOne)
                              + 3f * progress * progress * (target - controlTwo);
            return tangent.sqrMagnitude > 0.0001f ? tangent : target - origin;
        }

        /// <summary>把二维切线转换为 UGUI 旋转角度。</summary>
        private static float ToAngle(Vector2 tangent)
        {
            return Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
        }

        /// <summary>确保模板、箭头与已创建片段均不会截获手牌拖拽射线。</summary>
        private void SetRaycastTargetsDisabled()
        {
            _fragmentTemplate.raycastTarget = false;
            _headImage.raycastTarget = false;
            foreach (Image fragment in _fragments)
            {
                if (fragment != null)
                    fragment.raycastTarget = false;
            }
        }

        /// <summary>确认箭头 Prefab 已配置坐标空间、可见根、片段模板和端点 Image。</summary>
        private void EnsureReferences()
        {
            if (_coordinateSpace == null
                || _visualRoot == null
                || _fragmentTemplate == null
                || _headRect == null
                || _headImage == null)
            {
                throw new InvalidOperationException(
                    "BattleTargetingArrowView is missing one or more serialized references.");
            }
        }
    }
}
