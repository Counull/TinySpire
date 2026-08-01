using System;
using UnityEngine;
using UnityEngine.UI;

namespace TinySpire.UI.Battle
{
    /// <summary>只按屏幕端点绘制的功能性目标箭头；全部 Graphic 永不接收射线。</summary>
    [DisallowMultipleComponent]
    public sealed class BattleTargetingArrowView : MonoBehaviour
    {
        [SerializeField] private RectTransform _coordinateSpace;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private RectTransform _lineRect;
        [SerializeField] private RectTransform _headRect;
        [SerializeField] private Image _lineImage;
        [SerializeField] private Image _headImage;
        [SerializeField, Min(1f)] private float _lineThickness = 12f;

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

        /// <summary>显示箭头并以两个屏幕坐标写入起点、长度与方向。</summary>
        public void Show(Vector2 originScreenPosition, Vector2 targetScreenPosition)
        {
            EnsureReferences();
            _lineImage.raycastTarget = false;
            _headImage.raycastTarget = false;
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

        /// <summary>隐藏全部箭头表现，不改变任何拖拽或战斗事实。</summary>
        public void Hide()
        {
            if (_visualRoot != null)
                _visualRoot.SetActive(false);
        }

        /// <summary>把屏幕端点转换到当前 Overlay 局部空间并写入线段与箭头端点。</summary>
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

            Vector2 direction = targetLocal - originLocal;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            _lineRect.anchoredPosition = originLocal;
            _lineRect.sizeDelta = new Vector2(direction.magnitude, _lineThickness);
            _lineRect.localEulerAngles = new Vector3(0f, 0f, angle);
            _headRect.anchoredPosition = targetLocal;
            _headRect.localEulerAngles = new Vector3(0f, 0f, angle - 45f);
            return true;
        }

        /// <summary>确认箭头 Prefab 已配置坐标空间、可见根、线段和端点 Image。</summary>
        private void EnsureReferences()
        {
            if (_coordinateSpace == null
                || _visualRoot == null
                || _lineRect == null
                || _headRect == null
                || _lineImage == null
                || _headImage == null)
            {
                throw new InvalidOperationException(
                    "BattleTargetingArrowView is missing one or more serialized references.");
            }
        }
    }
}
