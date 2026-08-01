using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TinySpire.Battle;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

namespace TinySpire.UI.Battle
{
    /// <summary>
    /// Creates and releases BattleScene participant prefabs from static Addressables addresses.
    /// </summary>
    public sealed class BattleParticipantPresenter : MonoBehaviour
    {
        [SerializeField] private Transform _playerAnchor;
        [SerializeField] private Transform _enemyAnchor;
        [SerializeField, Min(0.01f)] private float _enemySpacing = 2f;
        [SerializeField] private Canvas _hudCanvas;
        [SerializeField] private ParticipantHudView _hudViewPrefab;

        private readonly Dictionary<CombatantId, GameObject> _views = new Dictionary<CombatantId, GameObject>();
        private readonly Dictionary<CombatantId, ParticipantHudView> _hudViews = new Dictionary<CombatantId, ParticipantHudView>();

        private BattleSession _session;
        private ConfigService _configs;
        private LocalizationService _localization;
        private bool _isDestroyed;

        /// <summary>接收 BattleScene 作用域准备完成的战斗事实、配置与本地化服务。</summary>
        [Inject]
        public void Construct(BattleSession session, ConfigService configs, LocalizationService localization)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        /// <summary>场景启动后校验依赖，并异步创建本场参与者的角色与 HUD。</summary>
        private async void Start()
        {
            ValidateSetup();
            await CreateViewsAsync();
        }

        /// <summary>场景离开时释放角色 Addressables 实例与对应的普通 HUD 实例。</summary>
        private void OnDestroy()
        {
            _isDestroyed = true;

            foreach (ParticipantHudView hudView in _hudViews.Values)
            {
                if (hudView != null)
                    Destroy(hudView.gameObject);
            }

            _hudViews.Clear();
            foreach (GameObject view in _views.Values)
            {
                if (view != null)
                    Addressables.ReleaseInstance(view);
            }

            _views.Clear();
        }

        /// <summary>按当前会话先创建唯一玩家，再按 Encounter 顺序创建敌人。</summary>
        private async UniTask CreateViewsAsync()
        {
            await CreatePlayerViewAsync();
            await CreateEnemyViewsAsync();
        }

        /// <summary>从玩家运行时事实与 Hero 配置创建一个角色及其 HUD。</summary>
        private async UniTask CreatePlayerViewAsync()
        {
            PlayerCombatantData player = null;
            foreach (CombatantData combatant in _session.Combatants.All.Values)
            {
                if (combatant is PlayerCombatantData candidate)
                {
                    if (player != null)
                        throw new InvalidOperationException("M3A supports exactly one player combatant.");

                    player = candidate;
                }
            }

            if (player == null)
                throw new InvalidOperationException("Battle session does not contain a player combatant.");

            cfg.battle.Hero hero = _configs.Tables.TbHero.GetOrDefault(player.TemplateId)
                ?? throw new InvalidOperationException($"Player Combatant {player.Id} references missing hero template {player.TemplateId}.");
            await CreateParticipantViewAsync(
                player,
                hero.NameI18nKey,
                hero.ViewPrefabAddress,
                _playerAnchor,
                Vector3.zero);
        }

        /// <summary>按既定 Encounter 顺序从敌人运行时事实与 Enemy 配置创建角色及 HUD。</summary>
        private async UniTask CreateEnemyViewsAsync()
        {
            IReadOnlyList<CombatantId> enemyIds = _session.EnemyCombatantIdsInEncounterOrder;
            IReadOnlyList<Vector3> positions = EnemyCombatantLayout.CalculateLocalPositions(
                enemyIds.Count,
                _enemySpacing);
            for (int index = 0; index < enemyIds.Count; index++)
            {
                CombatantId enemyId = enemyIds[index];
                if (!_session.Combatants.TryGet(enemyId, out CombatantData combatant)
                    || !(combatant is EnemyCombatantData enemy))
                {
                    throw new InvalidOperationException(
                        $"Encounter order references missing enemy combatant {enemyId}.");
                }

                cfg.battle.Enemy template = _configs.Tables.TbEnemy.GetOrDefault(enemy.TemplateId)
                    ?? throw new InvalidOperationException(
                        $"Enemy Combatant {enemy.Id} references missing enemy template {enemy.TemplateId}.");
                await CreateParticipantViewAsync(
                    enemy,
                    template.NameI18nKey,
                    template.ViewPrefabAddress,
                    _enemyAnchor,
                    positions[index]);
            }
        }

        /// <summary>加载一个世界角色，并将同一参与者事实绑定到独立 HUD。</summary>
        private async UniTask CreateParticipantViewAsync(
            CombatantData combatant,
            string nameI18nKey,
            string address,
            Transform anchor,
            Vector3 localPosition)
        {
            CombatantId combatantId = combatant.Id;
            int templateId = combatant.TemplateId;
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new InvalidOperationException(
                    $"Combatant {combatantId} template {templateId} has no view_prefab_address.");
            }
            if (string.IsNullOrWhiteSpace(nameI18nKey))
            {
                throw new InvalidOperationException(
                    $"Combatant {combatantId} template {templateId} has no name_i18n_key.");
            }

            AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(address, anchor);
            GameObject view;
            try
            {
                view = await handle.Task;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Failed to load view for Combatant {combatantId}, template {templateId}, address '{address}'.",
                    exception);
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || view == null)
            {
                throw new InvalidOperationException(
                    $"Failed to load view for Combatant {combatantId}, template {templateId}, address '{address}'.",
                    handle.OperationException);
            }
            if (view.GetComponentInChildren<SpriteRenderer>() == null)
            {
                Addressables.ReleaseInstance(view);
                throw new InvalidOperationException(
                    $"View for Combatant {combatantId}, template {templateId}, address '{address}' has no SpriteRenderer.");
            }

            if (_isDestroyed)
            {
                Addressables.ReleaseInstance(view);
                return;
            }

            view.transform.localPosition = localPosition;
            view.transform.localRotation = Quaternion.identity;
            _views.Add(combatantId, view);
            ParticipantHudView hudView = null;
            try
            {
                hudView = Instantiate(_hudViewPrefab, _hudCanvas.transform);
                hudView.name = $"CombatantHud_{combatantId.Value:00}_Template_{templateId}";
                hudView.Bind(
                    combatant,
                    nameI18nKey,
                    view.transform,
                    _hudCanvas,
                    _localization,
                    _configs.Tables,
                    _session.EnemyIntents);
                _hudViews.Add(combatantId, hudView);
            }
            catch
            {
                if (hudView != null)
                    Destroy(hudView.gameObject);
                _views.Remove(combatantId);
                Addressables.ReleaseInstance(view);
                throw;
            }
        }

        /// <summary>确认 Presenter 的依赖、锚点和 HUD Prefab 在创建前均已配置。</summary>
        private void ValidateSetup()
        {
            if (_session == null)
                throw new InvalidOperationException("BattleParticipantPresenter did not receive the initialized battle session.");
            if (_configs == null || _configs.Tables == null)
                throw new InvalidOperationException("BattleParticipantPresenter did not receive initialized configuration tables.");
            if (_localization == null)
                throw new InvalidOperationException("BattleParticipantPresenter did not receive the localization service.");
            if (_playerAnchor == null)
                throw new InvalidOperationException("BattleParticipantPresenter is missing Player Anchor.");
            if (_enemyAnchor == null)
                throw new InvalidOperationException("BattleParticipantPresenter is missing Enemy Anchor.");
            if (_hudCanvas == null)
                throw new InvalidOperationException("BattleParticipantPresenter is missing the participant HUD Canvas.");
            if (_hudViewPrefab == null)
                throw new InvalidOperationException("BattleParticipantPresenter is missing the ParticipantHudView prefab.");
        }
    }
}
