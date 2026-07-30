using System;
using R3;
using TinySpire.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace TinySpire.UI.Battle
{
    /// <summary>
    /// 一个参与者的屏幕空间 HUD；它订阅运行时事实，不拥有生命或力量的镜像状态。
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
        [SerializeField, Min(0f)] private float _headOffset = 0.2f;
        [SerializeField, Min(0f)] private float _feetOffset = 0.2f;

        private CombatantData _combatant;
        private Transform _worldView;
        private SpriteRenderer _spriteRenderer;
        private Canvas _canvas;
        private LocalizationService _localization;
        private string _nameI18nKey;

        /// <summary>
        /// 将 HUD 绑定到唯一的参与者事实、世界角色和本地化服务。
        /// </summary>
        public void Bind(
            CombatantData combatant,
            string nameI18nKey,
            Transform worldView,
            Canvas canvas,
            LocalizationService localization)
        {
            ValidateReferences();
            _combatant = combatant ?? throw new ArgumentNullException(nameof(combatant));
            _worldView = worldView ?? throw new ArgumentNullException(nameof(worldView));
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
            if (string.IsNullOrWhiteSpace(nameI18nKey))
                throw new ArgumentException("Localization key cannot be empty.", nameof(nameI18nKey));

            _nameI18nKey = nameI18nKey;
            _spriteRenderer = _worldView.GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer == null)
            {
                throw new InvalidOperationException(
                    $"Combatant {_combatant.Id}, template {_combatant.TemplateId} HUD requires a SpriteRenderer.");
            }

            _combatant.Health.Subscribe(RefreshHealth).AddTo(this);
            _combatant.Strength.Subscribe(RefreshStrength).AddTo(this);
            _localization.LocaleChanged.Subscribe(_ => RefreshLocalizedText()).AddTo(this);
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
        }

        /// <summary>语言改变时重派生名称和力量显示，不缓存翻译正文。</summary>
        private void RefreshLocalizedText()
        {
            _nameText.text = _localization.GetString(_nameI18nKey);
            RefreshStrength(_combatant.Strength.CurrentValue);
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
                || _strengthText == null)
            {
                throw new InvalidOperationException(
                    "ParticipantHudView is missing one or more serialized HUD references.");
            }
        }
    }
}
