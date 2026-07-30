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

        private readonly Dictionary<CombatantId, GameObject> _views = new Dictionary<CombatantId, GameObject>();

        private BattleSession _session;
        private ConfigService _configs;

        /// <summary>
        /// Receives the initialized battle facts and static configuration from the BattleScene scope.
        /// </summary>
        [Inject]
        public void Construct(BattleSession session, ConfigService configs)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        private async void Start()
        {
            ValidateSetup();
            await CreateViewsAsync();
        }

        private void OnDestroy()
        {
            foreach (GameObject view in _views.Values)
            {
                if (view != null)
                    Addressables.ReleaseInstance(view);
            }

            _views.Clear();
        }

        /// <summary>Creates one player view and encounter-ordered enemy views for the current session.</summary>
        private async UniTask CreateViewsAsync()
        {
            await CreatePlayerViewAsync();
            await CreateEnemyViewsAsync();
        }

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
            await InstantiateViewAsync(player.Id, player.TemplateId, hero.ViewPrefabAddress, _playerAnchor, Vector3.zero);
        }

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
                await InstantiateViewAsync(enemy.Id, enemy.TemplateId, template.ViewPrefabAddress, _enemyAnchor, positions[index]);
            }
        }

        private async UniTask InstantiateViewAsync(
            CombatantId combatantId,
            int templateId,
            string address,
            Transform anchor,
            Vector3 localPosition)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new InvalidOperationException(
                    $"Combatant {combatantId} template {templateId} has no view_prefab_address.");
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

            view.transform.localPosition = localPosition;
            view.transform.localRotation = Quaternion.identity;
            _views.Add(combatantId, view);
        }

        private void ValidateSetup()
        {
            if (_session == null)
                throw new InvalidOperationException("BattleParticipantPresenter did not receive the initialized battle session.");
            if (_configs == null || _configs.Tables == null)
                throw new InvalidOperationException("BattleParticipantPresenter did not receive initialized configuration tables.");
            if (_playerAnchor == null)
                throw new InvalidOperationException("BattleParticipantPresenter is missing Player Anchor.");
            if (_enemyAnchor == null)
                throw new InvalidOperationException("BattleParticipantPresenter is missing Enemy Anchor.");
        }
    }
}
