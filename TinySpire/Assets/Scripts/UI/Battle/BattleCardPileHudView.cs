using System;
using DG.Tweening;
using R3;
using TinySpire.Battle;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TinySpire.UI.Battle
{
    /// <summary>
    /// 显示抽牌堆、弃牌堆与消耗牌堆计数的场景 HUD；不持有任何卡区事实的镜像。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleCardPileHudView : MonoBehaviour
    {
        private const string DrawPileNameKey = "battle.card_pile.draw.name";
        private const string DiscardPileNameKey = "battle.card_pile.discard.name";
        private const string ExhaustPileNameKey = "battle.card_pile.exhaust.name";

        [SerializeField] private Text _drawPileText;
        [SerializeField] private Text _discardPileText;
        [SerializeField] private Text _exhaustPileText;

        private BattleSession _session;
        private LocalizationService _localization;
        private Action<GameObject> _destroyReshuffleTransientForTesting;

        /// <summary>
        /// 接收本场战斗的卡区事实与当前本地化服务。
        /// </summary>
        [Inject]
        public void Construct(BattleSession session, LocalizationService localization)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        /// <summary>仅供程序集内 Editor 测试替换重洗字符销毁边界，不参与运行时 DI。</summary>
        internal void ConfigureReshuffleTransientDestroyForTesting(
            Action<GameObject> destroyTransient)
        {
            _destroyReshuffleTransientForTesting = destroyTransient
                ?? throw new ArgumentNullException(nameof(destroyTransient));
        }

        /// <summary>
        /// 校验视图引用，并订阅卡区布局与语言事实以重派生文本。
        /// </summary>
        private void Start()
        {
            ValidateReferences();
            if (_session == null || _localization == null)
            {
                throw new InvalidOperationException(
                    "BattleCardPileHudView did not receive the initialized battle session or localization service.");
            }

            _session.CardZones.Layout
                .Subscribe(RefreshCounts)
                .AddTo(this);
            _localization.LocaleChanged
                .Subscribe(_ => RefreshCounts(_session.CardZones.Layout.CurrentValue))
                .AddTo(this);
        }

        /// <summary>从当前牌堆 Text 的 RectTransform 即时解析屏幕中心，不缓存布局坐标。</summary>
        internal bool TryGetPileScreenAnchor(
            BattleCardZone zone,
            out Vector2 screenAnchor)
        {
            screenAnchor = default;
            Text pileText = zone switch
            {
                BattleCardZone.DrawPile => _drawPileText,
                BattleCardZone.DiscardPile => _discardPileText,
                BattleCardZone.ExhaustPile => _exhaustPileText,
                _ => null,
            };
            if (pileText == null)
                return false;

            Canvas canvas = pileText.canvas;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (canvas != null &&
                canvas.renderMode != RenderMode.ScreenSpaceOverlay &&
                camera == null)
            {
                return false;
            }

            RectTransform rect = pileText.rectTransform;
            screenAnchor = RectTransformUtility.WorldToScreenPoint(
                camera,
                rect.TransformPoint(rect.rect.center));
            return true;
        }

        /// <summary>以一条冻结重洗记录创建一个弃牌堆到抽牌堆的非交互字符 cue。</summary>
        internal BattleCommandPresentationTween CreateReshuffleMotionTween(
            BattleCardMotionCue cue,
            float duration,
            Ease ease)
        {
            if (cue == null)
                throw new ArgumentNullException(nameof(cue));
            if (cue.Kind != BattleCardMotionCueKind.CardsReshuffled ||
                cue.CardId.HasValue ||
                cue.TargetId.HasValue ||
                !cue.SettlementOrder.HasValue)
            {
                throw new InvalidOperationException(
                    "Reshuffle motion requires one frozen CardsReshuffled settlement cue.");
            }
            if (duration < 0f)
                throw new ArgumentOutOfRangeException(nameof(duration));

            ValidateReferences();
            Canvas canvas = _drawPileText.canvas;
            if (canvas == null || _discardPileText.canvas != canvas)
            {
                throw new InvalidOperationException(
                    "Draw and discard pile anchors must share one Canvas.");
            }

            GameObject transient = null;
            RectTransform transientRect = null;
            Vector2 startScreenPosition = default;
            Vector2 endScreenPosition = default;
            bool released = false;

            // 正常完成、立即完成与取消统一领取这一次释放权，避免重复销毁瞬时字符。
            void ReleaseTransient()
            {
                if (released)
                    return;

                released = true;
                if (transient == null)
                    return;

                DestroyReshuffleTransient(transient);
                transient = null;
                transientRect = null;
            }

            Sequence sequence = DOTween.Sequence()
                .AppendCallback(() =>
                {
                    if (released || this == null ||
                        !TryGetPileScreenAnchor(
                            BattleCardZone.DiscardPile,
                            out startScreenPosition) ||
                        !TryGetPileScreenAnchor(
                            BattleCardZone.DrawPile,
                            out endScreenPosition))
                    {
                        return;
                    }

                    transient = CreateReshuffleTransient(canvas);
                    transientRect = transient.GetComponent<RectTransform>();
                    SetTransientScreenPosition(
                        transientRect,
                        canvas,
                        startScreenPosition);
                })
                .Append(DOVirtual.Float(
                        0f,
                        1f,
                        duration,
                        progress =>
                        {
                            if (transientRect == null)
                                return;

                            Vector2 screenPosition = Vector2.LerpUnclamped(
                                startScreenPosition,
                                endScreenPosition,
                                progress);
                            screenPosition += Vector2.up *
                                              (Mathf.Sin(Mathf.PI * progress) * 48f);
                            SetTransientScreenPosition(
                                transientRect,
                                canvas,
                                screenPosition);
                        })
                    .SetEase(ease))
                .AppendCallback(ReleaseTransient);
            return new BattleCommandPresentationTween(sequence, ReleaseTransient);
        }

        /// <summary>在现有牌堆 Canvas 下懒创建一个纯字符、不可射线命中的重洗瞬时 View。</summary>
        private GameObject CreateReshuffleTransient(Canvas canvas)
        {
            var transient = new GameObject(
                "CardReshuffleTransient",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(CanvasGroup));
            RectTransform rect = transient.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, worldPositionStays: false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = _discardPileText.rectTransform.rect.size;

            Text text = transient.GetComponent<Text>();
            text.text = "↻";
            text.font = _discardPileText.font;
            text.fontSize = _discardPileText.fontSize;
            text.fontStyle = _discardPileText.fontStyle;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = _discardPileText.color;
            text.raycastTarget = false;

            CanvasGroup group = transient.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            return transient;
        }

        /// <summary>把当前屏幕坐标换算到牌堆 Canvas 的中心锚点局部坐标。</summary>
        private static bool SetTransientScreenPosition(
            RectTransform transientRect,
            Canvas canvas,
            Vector2 screenPosition)
        {
            if (transientRect == null || canvas == null ||
                !(canvas.transform is RectTransform canvasRect))
            {
                return false;
            }

            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPosition,
                    camera,
                    out Vector2 localPosition))
            {
                return false;
            }

            transientRect.anchoredPosition = localPosition;
            return true;
        }

        /// <summary>按运行环境销毁重洗瞬时 View，并允许 Editor 测试观察精确一次释放。</summary>
        private void DestroyReshuffleTransient(GameObject transient)
        {
            if (_destroyReshuffleTransientForTesting != null)
            {
                _destroyReshuffleTransientForTesting.Invoke(transient);
                return;
            }

            if (Application.isPlaying)
                Destroy(transient);
            else
                DestroyImmediate(transient);
        }

        /// <summary>
        /// 从刚发布的完整卡区布局派生三个计数文本，不保存数量副本。
        /// </summary>
        private void RefreshCounts(CardZoneLayoutData layout)
        {
            _drawPileText.text = BattleCardPileHudPresentation.Format(
                _localization.GetString(DrawPileNameKey),
                layout.DrawPile.Count);
            _discardPileText.text = BattleCardPileHudPresentation.Format(
                _localization.GetString(DiscardPileNameKey),
                layout.DiscardPile.Count);
            _exhaustPileText.text = BattleCardPileHudPresentation.Format(
                _localization.GetString(ExhaustPileNameKey),
                layout.ExhaustPile.Count);
        }

        /// <summary>
        /// 确认场景 HUD 已配置三个必需的文本节点。
        /// </summary>
        private void ValidateReferences()
        {
            if (_drawPileText == null || _discardPileText == null || _exhaustPileText == null)
            {
                throw new InvalidOperationException(
                    "BattleCardPileHudView is missing one or more card-pile text references.");
            }
        }
    }
}
