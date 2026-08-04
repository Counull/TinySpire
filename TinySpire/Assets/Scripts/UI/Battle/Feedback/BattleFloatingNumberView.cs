using System;
using System.Globalization;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TinySpire.UI.Battle
{
    /// <summary>只显示冻结实际量的非交互纯字符飘字 View。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Text), typeof(CanvasGroup))]
    public sealed class BattleFloatingNumberView : MonoBehaviour
    {
        [SerializeField] private Color _blockAbsorbedColor = new Color32(110, 205, 255, 255);
        [SerializeField] private Color _healthLossColor = new Color32(255, 100, 100, 255);
        [SerializeField] private Color _blockGainedColor = new Color32(105, 235, 185, 255);
        [SerializeField, Min(0.01f)] private float _durationSeconds = 0.45f;
        [SerializeField, Min(0f)] private float _riseDistance = 48f;

        private RectTransform _rectTransform;
        private Text _text;
        private CanvasGroup _canvasGroup;
        private Vector2 _baseAnchoredPosition;

        /// <summary>组件载入时固定纯字符样式与非交互约束。</summary>
        private void Awake()
        {
            ResolveReferences();
            _text.raycastTarget = false;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.fontStyle = FontStyle.Bold;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        /// <summary>以冻结正数配置单个飘字，并返回由命令级 runner 串行拥有的 Tween。</summary>
        internal Tween CreateTween(
            BattleCommandPresentationStepKind kind,
            int amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            ResolveReferences();
            ApplyStyle(kind, amount);
            _text.raycastTarget = false;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            _baseAnchoredPosition = _rectTransform.anchoredPosition;
            _canvasGroup.alpha = 0f;

            return DOTween.Sequence()
                .Pause()
                .AppendCallback(() =>
                {
                    gameObject.SetActive(true);
                    _rectTransform.anchoredPosition = _baseAnchoredPosition;
                    _canvasGroup.alpha = 1f;
                })
                .Append(
                    _rectTransform
                        .DOAnchorPosY(_baseAnchoredPosition.y + _riseDistance, _durationSeconds)
                        .SetEase(Ease.OutCubic))
                .Join(_canvasGroup.DOFade(0f, _durationSeconds).SetEase(Ease.InQuad));
        }

        /// <summary>在正常完成或取消时恢复不可见基准姿态。</summary>
        internal void ResetHidden()
        {
            ResolveReferences();
            _rectTransform.anchoredPosition = _baseAnchoredPosition;
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        /// <summary>把三类数字步骤映射为锁定的纯字符与颜色样式。</summary>
        private void ApplyStyle(
            BattleCommandPresentationStepKind kind,
            int amount)
        {
            string magnitude = amount.ToString(CultureInfo.InvariantCulture);
            switch (kind)
            {
                case BattleCommandPresentationStepKind.BlockAbsorbedNumber:
                    _text.text = $"-{magnitude}";
                    _text.color = _blockAbsorbedColor;
                    break;
                case BattleCommandPresentationStepKind.HealthLossNumber:
                    _text.text = $"-{magnitude}";
                    _text.color = _healthLossColor;
                    break;
                case BattleCommandPresentationStepKind.BlockGainedNumber:
                    _text.text = $"+{magnitude}";
                    _text.color = _blockGainedColor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        /// <summary>从同一 Prefab 根解析且校验纯 UGUI 组件。</summary>
        private void ResolveReferences()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();
            if (_text == null)
                _text = GetComponent<Text>();
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_rectTransform == null || _text == null || _canvasGroup == null)
            {
                throw new InvalidOperationException(
                    "BattleFloatingNumberView requires RectTransform, Text and CanvasGroup.");
            }
        }
    }
}
