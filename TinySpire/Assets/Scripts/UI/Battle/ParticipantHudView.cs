using System;
using cfg;
using R3;
using TinySpire.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace TinySpire.UI.Battle
{
    /// <summary>
    /// 一个参与者的屏幕空间 HUD；它订阅运行时事实，不拥有生命、力量、意图或预测值镜像。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParticipantHudView : MonoBehaviour
    {
        private const string StrengthNameKey = "battle.keyword.strength.name";

        [SerializeField] private RectTransform _nameAnchor;
        [SerializeField] private RectTransform _vitalsAnchor;
        [SerializeField] private Text _nameText;
        [SerializeField] private Image _healthFillImage;
        [SerializeField] private Text _healthText;
        [SerializeField] private GameObject _strengthRoot;
        [SerializeField] private Text _strengthText;
        [SerializeField] private GameObject _intentRoot;
        [SerializeField] private Image _intentIcon;
        [SerializeField] private Text _intentValueText;
        [SerializeField] private Sprite _attackIntentSprite;
        [SerializeField] private Sprite _defendIntentSprite;
        [SerializeField] private Sprite _buffIntentSprite;
        [SerializeField] private Sprite _debuffIntentSprite;
        [SerializeField] private Sprite _specialIntentSprite;
        [SerializeField, Min(0f)] private float _headOffset = 0.2f;
        [SerializeField, Min(0f)] private float _feetOffset = 0.2f;

        private CombatantData _combatant;
        private Transform _worldView;
        private SpriteRenderer _spriteRenderer;
        private Canvas _canvas;
        private LocalizationService _localization;
        private Tables _tables;
        private BattleEnemyIntentsData _enemyIntents;
        private EnemyCombatantData _enemyCombatant;
        private string _nameI18nKey;

        /// <summary>
        /// 将 HUD 绑定到唯一的参与者、敌人意图、静态配置、世界角色和本地化事实。
        /// </summary>
        public void Bind(
            CombatantData combatant,
            string nameI18nKey,
            Transform worldView,
            Canvas canvas,
            LocalizationService localization,
            Tables tables,
            BattleEnemyIntentsData enemyIntents)
        {
            ValidateReferences();
            _combatant = combatant ?? throw new ArgumentNullException(nameof(combatant));
            _worldView = worldView ?? throw new ArgumentNullException(nameof(worldView));
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            if (string.IsNullOrWhiteSpace(nameI18nKey))
                throw new ArgumentException("Localization key cannot be empty.", nameof(nameI18nKey));

            _nameI18nKey = nameI18nKey;
            _enemyCombatant = combatant as EnemyCombatantData;
            if (_enemyCombatant != null)
                _enemyIntents = enemyIntents ?? throw new ArgumentNullException(nameof(enemyIntents));
            else
                _intentRoot.SetActive(false);
            _spriteRenderer = _worldView.GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                throw new InvalidOperationException(
                    $"Combatant {_combatant.Id}, template {_combatant.TemplateId} HUD requires a SpriteRenderer.");
            }

            _combatant.Health.Subscribe(RefreshHealth).AddTo(this);
            _combatant.Strength.Subscribe(RefreshStrength).AddTo(this);
            _localization.LocaleChanged.Subscribe(_ => RefreshLocalizedText()).AddTo(this);
            if (_enemyCombatant != null)
                _enemyIntents.Layout.Subscribe(RefreshIntent).AddTo(this);
            RefreshLocalizedText();
        }

        /// <summary>在布局结束后将名称和生命 HUD 投影到角色的头顶与脚下。</summary>
        private void LateUpdate()
        {
            if (_spriteRenderer == null || _canvas == null)
                return;

            Bounds bounds = _spriteRenderer.bounds;
            PositionAtWorldPoint(_nameAnchor, new Vector3(bounds.center.x, bounds.max.y + _headOffset, bounds.center.z));
            PositionAtWorldPoint(_vitalsAnchor, new Vector3(bounds.center.x, bounds.min.y - _feetOffset, bounds.center.z));
        }

        /// <summary>按参与者当前生命事实刷新生命条与数值。</summary>
        private void RefreshHealth(int currentHealth)
        {
            _healthFillImage.fillAmount = (float)currentHealth / _combatant.MaxHealth;
            _healthText.text = ParticipantHudPresentation.FormatHealth(currentHealth, _combatant.MaxHealth);
            if (_enemyCombatant != null)
                RefreshIntent(_enemyIntents.Layout.CurrentValue);
        }

        /// <summary>按参与者当前力量事实刷新可见性与数值。</summary>
        private void RefreshStrength(int strength)
        {
            bool shouldShow = ParticipantHudPresentation.ShouldShowStrength(strength);
            _strengthRoot.SetActive(shouldShow);
            if (shouldShow)
                _strengthText.text = ParticipantHudPresentation.FormatStrength(
                    _localization.GetString(StrengthNameKey),
                    strength);
            if (_enemyCombatant != null)
                RefreshIntent(_enemyIntents.Layout.CurrentValue);
        }

        /// <summary>语言改变时重派生名称和力量显示，不缓存翻译正文。</summary>
        private void RefreshLocalizedText()
        {
            _nameText.text = _localization.GetString(_nameI18nKey);
            RefreshStrength(_combatant.Strength.CurrentValue);
        }

        /// <summary>从完整当前意图快照与参与者事实重派生图标、数值和死亡可见性。</summary>
        private void RefreshIntent(EnemyIntentLayoutData layout)
        {
            if (_enemyCombatant == null)
            {
                _intentRoot.SetActive(false);
                return;
            }

            EnemyIntentPresentationData presentation = ParticipantHudPresentation.DeriveEnemyIntent(
                layout,
                _tables,
                _enemyCombatant);
            _intentRoot.SetActive(presentation.IsVisible);
            if (!presentation.IsVisible)
                return;

            _intentIcon.sprite = SelectIntentSprite(presentation.IntentType);
            _intentValueText.text = ParticipantHudPresentation.FormatIntentValue(presentation.Value);
        }

        /// <summary>把静态意图枚举映射到 Prefab 序列化的正式 Sprite 资源。</summary>
        private Sprite SelectIntentSprite(cfg.battle.EnemyIntentType intentType)
        {
            switch (intentType)
            {
                case cfg.battle.EnemyIntentType.Attack:
                    return _attackIntentSprite;
                case cfg.battle.EnemyIntentType.Defend:
                    return _defendIntentSprite;
                case cfg.battle.EnemyIntentType.Buff:
                    return _buffIntentSprite;
                case cfg.battle.EnemyIntentType.Debuff:
                    return _debuffIntentSprite;
                case cfg.battle.EnemyIntentType.Special:
                    return _specialIntentSprite;
                default:
                    throw new ArgumentOutOfRangeException(nameof(intentType), intentType, "Unsupported enemy intent type.");
            }
        }

        /// <summary>将 HUD 子节点的世界点投影为 Canvas 内局部坐标。</summary>
        private void PositionAtWorldPoint(RectTransform hudElement, Vector3 worldPoint)
        {
            Camera camera = _canvas.worldCamera != null ? _canvas.worldCamera : Camera.main;
            if (camera == null)
                throw new InvalidOperationException("ParticipantHudView requires a Canvas camera or a tagged Main Camera.");

            Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
            bool visible = screenPoint.z > 0f;
            if (hudElement.gameObject.activeSelf != visible)
                hudElement.gameObject.SetActive(visible);
            if (!visible)
                return;

            RectTransform canvasRect = (RectTransform)_canvas.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPoint,
                    _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera,
                    out Vector2 localPoint))
            {
                throw new InvalidOperationException("Unable to project ParticipantHudView into its Canvas.");
            }

            hudElement.anchoredPosition = localPoint;
        }

        /// <summary>确认 Prefab 已配置所有必需的展示节点。</summary>
        private void ValidateReferences()
        {
            if (_nameAnchor == null
                || _vitalsAnchor == null
                || _nameText == null
                || _healthFillImage == null
                || _healthText == null
                || _strengthRoot == null
                || _strengthText == null
                || _intentRoot == null
                || _intentIcon == null
                || _intentValueText == null
                || _attackIntentSprite == null
                || _defendIntentSprite == null
                || _buffIntentSprite == null
                || _debuffIntentSprite == null
                || _specialIntentSprite == null)
            {
                throw new InvalidOperationException(
                    "ParticipantHudView is missing one or more serialized HUD references.");
            }
        }
    }
}
