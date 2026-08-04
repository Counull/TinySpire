using System;
using System.Collections.Generic;
using cfg;
using DG.Tweening;
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
        [SerializeField] private RectTransform _nameAnchor;
        [SerializeField] private RectTransform _vitalsAnchor;
        [SerializeField] private Text _nameText;
        [SerializeField] private Image _healthFillImage;
        [SerializeField] private Text _healthText;
        [SerializeField] private GameObject _statusRoot;
        [SerializeField] private GameObject _blockRoot;
        [SerializeField] private Text _blockText;
        [SerializeField] private GameObject _strengthRoot;
        [SerializeField] private Text _strengthText;
        [SerializeField] private GameObject _vulnerableRoot;
        [SerializeField] private Text _vulnerableText;
        [SerializeField] private GameObject _intentRoot;
        [SerializeField] private Image _intentIcon;
        [SerializeField] private Text _intentValueText;
        [SerializeField] private RectTransform _targetHighlightAnchor;
        [SerializeField] private GameObject _legalTargetHighlightRoot;
        [SerializeField] private Image[] _legalTargetHighlightCornerImages;
        [SerializeField] private GameObject _hoveredTargetHighlightRoot;
        [SerializeField] private Image[] _hoveredTargetHighlightCornerImages;
        [SerializeField] private RectTransform _feedbackAnchor;
        [SerializeField] private BattleFloatingNumberView _floatingNumberPrefab;
        [SerializeField] private Sprite _attackIntentSprite;
        [SerializeField] private Sprite _defendIntentSprite;
        [SerializeField] private Sprite _buffIntentSprite;
        [SerializeField] private Sprite _debuffIntentSprite;
        [SerializeField] private Sprite _specialIntentSprite;
        [SerializeField, Min(0f)] private float _headOffset = 0.2f;
        [SerializeField, Min(0f)] private float _nameAboveVitalsOffset = 0.5f;
        [SerializeField, Min(0f)] private float _targetHighlightPadding = 16f;
        [SerializeField, Min(0.01f)] private float _hitShakeDurationSeconds = 0.28f;
        [SerializeField, Min(0f)] private float _hitShakeStrength = 0.12f;
        [SerializeField, Min(0.02f)] private float _hudPulseDurationSeconds = 0.24f;
        [SerializeField, Min(1f)] private float _hudPulseScale = 1.18f;
        [SerializeField, Min(0.01f)] private float _deathTransitionDurationSeconds = 0.38f;
        [SerializeField, Range(0f, 1f)] private float _deathTransitionScale = 0.72f;

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
            _spriteRenderer = _worldView.GetComponentInChildren<SpriteRenderer>(includeInactive: true);
            if (_spriteRenderer == null)
            {
                throw new InvalidOperationException(
                    $"Combatant {_combatant.Id}, template {_combatant.TemplateId} HUD requires a SpriteRenderer.");
            }

            _combatant.Health.Subscribe(RefreshHealth).AddTo(this);
            _combatant.Block.Subscribe(_ => RefreshStatus()).AddTo(this);
            _combatant.Strength.Subscribe(_ => RefreshStrengthAndIntent()).AddTo(this);
            _combatant.Vulnerable.Subscribe(_ => RefreshStatus()).AddTo(this);
            _localization.LocaleChanged.Subscribe(_ => RefreshLocalizedText()).AddTo(this);
            if (_enemyCombatant != null)
                _enemyIntents.Layout.Subscribe(RefreshIntent).AddTo(this);
            SetTargetHighlight(isLegalCandidate: false, isHovered: false);
            RefreshLocalizedText();
            ApplyInitialLifeVisibility();
        }

        /// <summary>只切换当前 HUD 的功能性合法/命中高亮，不保存目标合法性玩法事实。</summary>
        public void SetTargetHighlight(bool isLegalCandidate, bool isHovered)
        {
            if (_legalTargetHighlightRoot == null || _hoveredTargetHighlightRoot == null)
                return;

            bool showHovered = isLegalCandidate && isHovered;
            _legalTargetHighlightRoot.SetActive(isLegalCandidate && !showHovered);
            _hoveredTargetHighlightRoot.SetActive(showHovered);
        }

        /// <summary>从当前绑定参与者创建一个只消费冻结 cue 的 concrete Tween lease。</summary>
        internal BattleCommandPresentationTween CreateCombatFeedbackTween(
            BattleCombatFeedbackCue cue)
        {
            if (cue == null)
                throw new ArgumentNullException(nameof(cue));
            if (_combatant == null)
                throw new InvalidOperationException("ParticipantHudView must be bound before feedback playback.");
            if (cue.TargetId != _combatant.Id)
            {
                throw new InvalidOperationException(
                    $"Combat feedback target {cue.TargetId} does not match bound participant {_combatant.Id}.");
            }

            switch (cue.Kind)
            {
                case BattleCommandPresentationStepKind.BlockAbsorbedNumber:
                case BattleCommandPresentationStepKind.HealthLossNumber:
                case BattleCommandPresentationStepKind.BlockGainedNumber:
                {
                    BattleFloatingNumberView floatingNumber = null;
                    try
                    {
                        floatingNumber = Instantiate(
                            _floatingNumberPrefab,
                            _feedbackAnchor,
                            worldPositionStays: false);
                        floatingNumber.name = $"{cue.Kind}_{cue.Amount}";
                        return new BattleCommandPresentationTween(
                            floatingNumber.CreateTween(cue.Kind, cue.Amount),
                            () => DestroyFloatingNumber(floatingNumber));
                    }
                    catch
                    {
                        DestroyFloatingNumber(floatingNumber, resetHidden: false);
                        throw;
                    }
                }
                case BattleCommandPresentationStepKind.HitShake:
                    return CreateHitShakeTween();
                case BattleCommandPresentationStepKind.StrengthIconPulse:
                {
                    int frozenValue = RequireFrozenValue(cue);
                    return CreateHudPulseTween(
                        _strengthRoot,
                        () =>
                        {
                            _statusRoot.SetActive(true);
                            _strengthText.text = ParticipantHudPresentation.FormatStatusValue(
                                frozenValue);
                        },
                        RefreshStatus);
                }
                case BattleCommandPresentationStepKind.VulnerableIconPulse:
                {
                    int frozenValue = RequireFrozenValue(cue);
                    return CreateHudPulseTween(
                        _vulnerableRoot,
                        () =>
                        {
                            _statusRoot.SetActive(true);
                            _vulnerableText.text = ParticipantHudPresentation.FormatStatusValue(
                                frozenValue);
                        },
                        RefreshStatus);
                }
                case BattleCommandPresentationStepKind.EnemyIntentPulse:
                {
                    int frozenBehaviorId = RequireFrozenValue(cue);
                    return CreateHudPulseTween(
                        _intentRoot,
                        () => RefreshFrozenIntent(frozenBehaviorId),
                        () => RefreshIntent(_enemyIntents.Layout.CurrentValue));
                }
                case BattleCommandPresentationStepKind.DeathTransition:
                    return CreateDeathTransitionTween();
                default:
                    throw new ArgumentOutOfRangeException(nameof(cue), cue.Kind, null);
            }
        }

        /// <summary>HUD 暂停时隐藏纯表现目标高亮，避免对象重新启用前残留旧候选。</summary>
        private void OnDisable()
        {
            SetTargetHighlight(isLegalCandidate: false, isHovered: false);
        }

        /// <summary>在布局结束后将生命与状态投影到头顶，并把名称稳定置于生命 HUD 上方。</summary>
        private void LateUpdate()
        {
            if (_spriteRenderer == null || _canvas == null)
                return;

            Bounds bounds = _spriteRenderer.bounds;
            PositionTargetHighlightAtBounds(bounds);
            PositionAtWorldPoint(_feedbackAnchor, bounds.center);
            var vitalsWorldPoint = new Vector3(
                bounds.center.x,
                bounds.max.y + _headOffset,
                bounds.center.z);
            PositionAtWorldPoint(_vitalsAnchor, vitalsWorldPoint);
            PositionAtWorldPoint(
                _nameAnchor,
                vitalsWorldPoint + Vector3.up * _nameAboveVitalsOffset);
        }

        /// <summary>按参与者当前生命事实刷新生命条与数值。</summary>
        private void RefreshHealth(int currentHealth)
        {
            _healthFillImage.fillAmount = (float)currentHealth / _combatant.MaxHealth;
            _healthText.text = ParticipantHudPresentation.FormatHealth(currentHealth, _combatant.MaxHealth);
            RefreshStatus();
            if (_enemyCombatant != null)
                RefreshIntent(_enemyIntents.Layout.CurrentValue);
        }

        /// <summary>按当前存活、Block、Strength 与 Vulnerable 事实重派生三个状态槽。</summary>
        private void RefreshStatus()
        {
            ParticipantStatusPresentationData presentation =
                ParticipantHudPresentation.DeriveStatus(_combatant);
            _statusRoot.SetActive(presentation.IsVisible);
            _blockRoot.SetActive(presentation.IsBlockVisible);
            _strengthRoot.SetActive(presentation.IsStrengthVisible);
            _vulnerableRoot.SetActive(presentation.IsVulnerableVisible);
            if (presentation.IsBlockVisible)
            {
                _blockText.text = ParticipantHudPresentation.FormatStatusValue(
                    presentation.Block);
            }
            if (presentation.IsStrengthVisible)
            {
                _strengthText.text = ParticipantHudPresentation.FormatStatusValue(
                    presentation.Strength);
            }
            if (presentation.IsVulnerableVisible)
            {
                _vulnerableText.text = ParticipantHudPresentation.FormatStatusValue(
                    presentation.Vulnerable);
            }
        }

        /// <summary>力量变化同时刷新状态槽与依赖共享公式的敌人意图值。</summary>
        private void RefreshStrengthAndIntent()
        {
            RefreshStatus();
            if (_enemyCombatant != null)
                RefreshIntent(_enemyIntents.Layout.CurrentValue);
        }

        /// <summary>语言改变时只重派生本地化名称，并从当前事实重刷状态槽。</summary>
        private void RefreshLocalizedText()
        {
            _nameText.text = _localization.GetString(_nameI18nKey);
            RefreshStatus();
        }

        /// <summary>首次绑定已死亡参与者时直接恢复死亡隐藏终态，不参与新 fatal 的播放时序。</summary>
        private void ApplyInitialLifeVisibility()
        {
            if (_combatant.IsAlive)
                return;

            _worldView.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        /// <summary>幂等隐藏并释放一个命令级纯字符 transient。</summary>
        private static void DestroyFloatingNumber(
            BattleFloatingNumberView floatingNumber,
            bool resetHidden = true)
        {
            if (floatingNumber == null)
                return;

            if (resetHidden)
                floatingNumber.ResetHidden();
            if (Application.isPlaying)
                Destroy(floatingNumber.gameObject);
            else
                DestroyImmediate(floatingNumber.gameObject);
        }

        /// <summary>读取状态或意图 cue 的冻结值，缺失时同步 fault 而不回查最终事实。</summary>
        private static int RequireFrozenValue(BattleCombatFeedbackCue cue)
        {
            if (!cue.FrozenValue.HasValue)
            {
                throw new InvalidOperationException(
                    $"Combat feedback {cue.Kind} requires a frozen settlement value.");
            }

            return cue.FrozenValue.Value;
        }

        /// <summary>创建只改变世界 View 局部位置并在清理时恢复 base pose 的受击抖动。</summary>
        private BattleCommandPresentationTween CreateHitShakeTween()
        {
            Transform target = _worldView;
            Vector3 basePosition = target.localPosition;
            Sequence sequence = DOTween.Sequence()
                .Pause()
                .AppendCallback(() =>
                {
                    if (target != null)
                        target.localPosition = basePosition;
                })
                .Append(
                    target.DOShakePosition(
                        _hitShakeDurationSeconds,
                        _hitShakeStrength,
                        vibrato: 12,
                        randomness: 45f,
                        snapping: false,
                        fadeOut: true));
            return new BattleCommandPresentationTween(
                sequence,
                () =>
                {
                    if (target != null)
                        target.localPosition = basePosition;
                });
        }

        /// <summary>创建只缩放既有 HUD 节点并在清理后重投影当前事实的短脉冲。</summary>
        private BattleCommandPresentationTween CreateHudPulseTween(
            GameObject pulseRoot,
            Action prepareCurrentFact,
            Action restoreCurrentFact)
        {
            if (pulseRoot == null)
                throw new ArgumentNullException(nameof(pulseRoot));
            if (prepareCurrentFact == null)
                throw new ArgumentNullException(nameof(prepareCurrentFact));
            if (restoreCurrentFact == null)
                throw new ArgumentNullException(nameof(restoreCurrentFact));

            Transform target = pulseRoot.transform;
            Vector3 baseScale = target.localScale;
            float halfDuration = _hudPulseDurationSeconds * 0.5f;
            Sequence sequence = DOTween.Sequence()
                .Pause()
                .AppendCallback(() =>
                {
                    if (this == null || pulseRoot == null || target == null)
                        return;

                    target.localScale = baseScale;
                    prepareCurrentFact.Invoke();
                    pulseRoot.SetActive(true);
                })
                .Append(target.DOScale(baseScale * _hudPulseScale, halfDuration).SetEase(Ease.OutQuad))
                .Append(target.DOScale(baseScale, halfDuration).SetEase(Ease.InQuad))
                .AppendCallback(() => RestoreHudPulse(
                    pulseRoot,
                    target,
                    baseScale,
                    restoreCurrentFact));
            return new BattleCommandPresentationTween(
                sequence,
                () => RestoreHudPulse(
                    pulseRoot,
                    target,
                    baseScale,
                    restoreCurrentFact));
        }

        /// <summary>在 cue 结束或 owner 取消时幂等恢复脉冲节点与当前权威 HUD 事实。</summary>
        private void RestoreHudPulse(
            GameObject pulseRoot,
            Transform target,
            Vector3 baseScale,
            Action restoreCurrentFact)
        {
            if (this == null || pulseRoot == null || target == null)
                return;

            target.localScale = baseScale;
            restoreCurrentFact.Invoke();
        }

        /// <summary>创建 fatal 末尾才隐藏世界 View 与完整 HUD 的死亡过渡。</summary>
        private BattleCommandPresentationTween CreateDeathTransitionTween()
        {
            Transform worldTransform = _worldView;
            Transform hudTransform = transform;
            SpriteRenderer spriteRenderer = _spriteRenderer;
            Vector3 baseWorldScale = worldTransform.localScale;
            Vector3 baseHudScale = hudTransform.localScale;
            Color baseWorldColor = spriteRenderer.color;
            Sequence sequence = DOTween.Sequence()
                .Pause()
                .AppendCallback(() =>
                {
                    if (this == null ||
                        worldTransform == null ||
                        hudTransform == null ||
                        spriteRenderer == null)
                    {
                        return;
                    }

                    worldTransform.gameObject.SetActive(true);
                    gameObject.SetActive(true);
                    worldTransform.localScale = baseWorldScale;
                    hudTransform.localScale = baseHudScale;
                    spriteRenderer.color = baseWorldColor;
                })
                .Append(
                    worldTransform
                        .DOScale(baseWorldScale * _deathTransitionScale, _deathTransitionDurationSeconds)
                        .SetEase(Ease.InCubic))
                .Join(
                    hudTransform
                        .DOScale(baseHudScale * _deathTransitionScale, _deathTransitionDurationSeconds)
                        .SetEase(Ease.InCubic))
                .Join(spriteRenderer.DOFade(0f, _deathTransitionDurationSeconds))
                .AppendCallback(() =>
                {
                    if (this != null)
                        ApplyCurrentLifeVisibility();
                });
            return new BattleCommandPresentationTween(
                sequence,
                () =>
                {
                    if (worldTransform != null)
                        worldTransform.localScale = baseWorldScale;
                    if (hudTransform != null)
                        hudTransform.localScale = baseHudScale;
                    if (spriteRenderer != null)
                        spriteRenderer.color = baseWorldColor;
                    if (this != null)
                        ApplyCurrentLifeVisibility();
                });
        }

        /// <summary>仅从当前权威存活事实恢复死亡过渡的最终可见性。</summary>
        private void ApplyCurrentLifeVisibility()
        {
            if (this == null || _combatant == null)
                return;

            bool isVisible = _combatant.IsAlive;
            if (_worldView != null)
                _worldView.gameObject.SetActive(isVisible);
            gameObject.SetActive(isVisible);
        }

        /// <summary>用该条意图 settlement 的冻结下一行为建立临时只读投影，并复用共享意图公式。</summary>
        private void RefreshFrozenIntent(int behaviorId)
        {
            var layout = new EnemyIntentLayoutData(
                new[]
                {
                    new KeyValuePair<CombatantId, int>(_combatant.Id, behaviorId),
                });
            RefreshIntent(layout);
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

        /// <summary>将四角锁定框投影为当前角色 Sprite 边界外的屏幕矩形，使角件围住目标而不覆盖其主体。</summary>
        private void PositionTargetHighlightAtBounds(Bounds bounds)
        {
            Camera camera = _canvas.worldCamera != null ? _canvas.worldCamera : Camera.main;
            if (camera == null)
                throw new InvalidOperationException("ParticipantHudView requires a Canvas camera or a tagged Main Camera.");

            RectTransform canvasRect = (RectTransform)_canvas.transform;
            Vector2 minLocal = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maxLocal = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int x = 0; x <= 1; x++)
            for (int y = 0; y <= 1; y++)
            for (int z = 0; z <= 1; z++)
            {
                Vector3 worldPoint = new Vector3(
                    x == 0 ? bounds.min.x : bounds.max.x,
                    y == 0 ? bounds.min.y : bounds.max.y,
                    z == 0 ? bounds.min.z : bounds.max.z);
                Vector3 screenPoint = camera.WorldToScreenPoint(worldPoint);
                if (screenPoint.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect,
                        screenPoint,
                        _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : camera,
                        out Vector2 localPoint))
                {
                    _targetHighlightAnchor.gameObject.SetActive(false);
                    return;
                }

                minLocal = Vector2.Min(minLocal, localPoint);
                maxLocal = Vector2.Max(maxLocal, localPoint);
            }

            _targetHighlightAnchor.gameObject.SetActive(true);
            _targetHighlightAnchor.anchoredPosition = (minLocal + maxLocal) * 0.5f;
            _targetHighlightAnchor.sizeDelta = maxLocal - minLocal
                                               + Vector2.one * (_targetHighlightPadding * 2f);
        }

        /// <summary>确认 Prefab 已配置所有必需的展示节点。</summary>
        private void ValidateReferences()
        {
            if (_nameAnchor == null
                || _vitalsAnchor == null
                || _nameText == null
                || _healthFillImage == null
                || _healthText == null
                || _statusRoot == null
                || _blockRoot == null
                || _blockText == null
                || _strengthRoot == null
                || _strengthText == null
                || _vulnerableRoot == null
                || _vulnerableText == null
                || _intentRoot == null
                || _intentIcon == null
                || _intentValueText == null
                || _targetHighlightAnchor == null
                || _legalTargetHighlightRoot == null
                || !HasFourTargetHighlightCorners(_legalTargetHighlightCornerImages)
                || _hoveredTargetHighlightRoot == null
                || !HasFourTargetHighlightCorners(_hoveredTargetHighlightCornerImages)
                || _feedbackAnchor == null
                || _floatingNumberPrefab == null
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

        /// <summary>确认每一种锁定状态均有四个独立且序列化的角件，不依赖运行时查找或临时创建。</summary>
        private static bool HasFourTargetHighlightCorners(Image[] cornerImages)
        {
            if (cornerImages == null || cornerImages.Length != 4)
                return false;

            foreach (Image cornerImage in cornerImages)
            {
                if (cornerImage == null)
                    return false;
            }

            return true;
        }
    }
}
