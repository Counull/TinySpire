using System;
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

        /// <summary>
        /// 接收本场战斗的卡区事实与当前本地化服务。
        /// </summary>
        [Inject]
        public void Construct(BattleSession session, LocalizationService localization)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
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
