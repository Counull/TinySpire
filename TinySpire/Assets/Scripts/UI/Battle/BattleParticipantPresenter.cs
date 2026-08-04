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
        [SerializeField, Min(0f)] private float _targetHitPadding = 18f;

        private readonly Dictionary<CombatantId, GameObject> _views = new Dictionary<CombatantId, GameObject>();
        private readonly Dictionary<CombatantId, ParticipantHudView> _hudViews = new Dictionary<CombatantId, ParticipantHudView>();
        private readonly List<CombatantId> _targetSelectionIds = new List<CombatantId>();

        private BattleSession _session;
        private ConfigService _configs;
        private LocalizationService _localization;
        private bool _isDestroyed;
        private Func<string, Transform, UniTask<GameObject>> _instantiateViewAsyncForTesting;
        private Action<GameObject> _releaseViewForTesting;
        private Action<GameObject> _destroyHudForTesting;

        /// <summary>从当前 Session 与 HUD 唯一映射即时判断全部参与者世界 View/HUD 是否可供表现。</summary>
        internal bool IsPresentationReady
        {
            get
            {
                if (_isDestroyed || _session == null ||
                    _views.Count != _session.Combatants.All.Count ||
                    _hudViews.Count != _session.Combatants.All.Count)
                {
                    return false;
                }

                foreach (CombatantId combatantId in _session.Combatants.All.Keys)
                {
                    if (!_views.TryGetValue(combatantId, out GameObject view) ||
                        view == null ||
                        !_hudViews.TryGetValue(combatantId, out ParticipantHudView hudView) ||
                        hudView == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>接收 BattleScene 作用域准备完成的战斗事实、配置与本地化服务。</summary>
        [Inject]
        public void Construct(BattleSession session, ConfigService configs, LocalizationService localization)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        }

        /// <summary>仅为程序集内 Editor 测试替换本 Presenter 的资源边界，不参与运行时 DI。</summary>
        internal void ConfigureViewResourceBoundaryForTesting(
            Func<string, Transform, UniTask<GameObject>> instantiateViewAsync,
            Action<GameObject> releaseView,
            Action<GameObject> destroyHud = null)
        {
            if (instantiateViewAsync == null)
                throw new ArgumentNullException(nameof(instantiateViewAsync));
            if (releaseView == null)
                throw new ArgumentNullException(nameof(releaseView));
            if (_views.Count > 0 || _hudViews.Count > 0)
            {
                throw new InvalidOperationException(
                    "Participant resource boundary cannot change after presentation creation starts.");
            }

            _instantiateViewAsyncForTesting = instantiateViewAsync;
            _releaseViewForTesting = releaseView;
            _destroyHudForTesting = destroyHud;
        }

        /// <summary>场景启动后校验依赖，并异步创建本场参与者的角色与 HUD。</summary>
        private async void Start()
        {
            ValidateSetup();
            await CreateViewsAsync();
        }

        /// <summary>Presenter 暂停时立即清除纯表现目标高亮，避免禁用期间残留瞄准状态。</summary>
        private void OnDisable()
        {
            EndTargetSelection();
        }

        /// <summary>场景离开时释放角色 Addressables 实例与对应的普通 HUD 实例。</summary>
        private void OnDestroy()
        {
            DisposePresentation();
        }

        /// <summary>仅供程序集内 Editor 测试触发与 OnDestroy 完全相同的场景收口。</summary>
        internal void DisposePresentationForTesting()
        {
            DisposePresentation();
        }

        /// <summary>幂等标记旧 Presenter 失效，并释放它持有的全部非权威表现对象。</summary>
        private void DisposePresentation()
        {
            if (_isDestroyed)
                return;

            _isDestroyed = true;
            EndTargetSelection();
            ReleaseCreatedViews();
        }

        /// <summary>按规则 module 给出的稳定合法候选开始一次纯表现目标选择。</summary>
        public void BeginTargetSelection(IReadOnlyList<CombatantId> legalTargetIds)
        {
            if (legalTargetIds == null)
                throw new ArgumentNullException(nameof(legalTargetIds));

            EndTargetSelection();
            foreach (CombatantId legalTargetId in legalTargetIds)
            {
                if (!ContainsTargetId(_targetSelectionIds, legalTargetId))
                    _targetSelectionIds.Add(legalTargetId);
            }

            RefreshTargetHighlights(hoveredTargetId: null);
        }

        /// <summary>用当前角色 Sprite 屏幕矩形更新命中与高亮；View 未就绪时安全返回空目标。</summary>
        public CombatantId? UpdateTargetSelection(Vector2 pointerScreenPosition)
        {
            Camera camera = ResolveTargetCamera();
            if (camera == null || _targetSelectionIds.Count == 0)
            {
                RefreshTargetHighlights(hoveredTargetId: null);
                return null;
            }

            var candidates = new List<BattleTargetScreenCandidate>(_targetSelectionIds.Count);
            foreach (CombatantId targetId in _targetSelectionIds)
            {
                if (!TryCreateScreenCandidate(targetId, camera, out BattleTargetScreenCandidate candidate))
                    continue;

                candidates.Add(candidate);
            }

            CombatantId? selectedId = BattleTargetScreenSelector.Select(
                pointerScreenPosition,
                candidates);
            RefreshTargetHighlights(selectedId);
            return selectedId;
        }

        /// <summary>结束当前目标选择并清除全部 HUD 高亮，不改变参与者或回合事实。</summary>
        public void EndTargetSelection()
        {
            _targetSelectionIds.Clear();
            foreach (ParticipantHudView hudView in _hudViews.Values)
            {
                if (hudView != null)
                    hudView.SetTargetHighlight(isLegalCandidate: false, isHovered: false);
            }
        }

        /// <summary>把已加载世界 View 加入 Presenter 的唯一参与者映射。</summary>
        internal void RegisterParticipantView(CombatantId combatantId, GameObject view)
        {
            if (view == null)
                throw new ArgumentNullException(nameof(view));
            if (_views.ContainsKey(combatantId))
            {
                throw new InvalidOperationException(
                    $"Participant world View mapping already contains {combatantId}.");
            }

            _views.Add(combatantId, view);
        }

        /// <summary>把已绑定 HUD 加入 Presenter 的唯一参与者映射。</summary>
        internal void RegisterParticipantHud(
            CombatantId combatantId,
            ParticipantHudView hudView)
        {
            if (hudView == null)
                throw new ArgumentNullException(nameof(hudView));
            if (_hudViews.ContainsKey(combatantId))
            {
                throw new InvalidOperationException(
                    $"Participant HUD mapping already contains {combatantId}.");
            }

            _hudViews.Add(combatantId, hudView);
        }

        /// <summary>只从现有唯一映射解析精确参与者并创建 concrete 战斗反馈 Tween。</summary>
        internal BattleCommandPresentationTween CreateCombatFeedbackTween(
            BattleCombatFeedbackCue cue)
        {
            if (cue == null)
                throw new ArgumentNullException(nameof(cue));
            if (!_hudViews.TryGetValue(cue.TargetId, out ParticipantHudView hudView)
                || hudView == null)
            {
                throw new InvalidOperationException(
                    $"Combat feedback target {cue.TargetId} has no registered Participant HUD.");
            }

            return hudView.CreateCombatFeedbackTween(cue);
        }

        /// <summary>从当前唯一世界 View 映射即时投影表现锚点，不因 fatal 结算后的存活状态过滤目标。</summary>
        internal bool TryGetPresentationScreenAnchor(
            CombatantId combatantId,
            out Vector2 screenAnchor)
        {
            screenAnchor = default;
            Camera camera = ResolveTargetCamera();
            if (camera == null ||
                !_views.TryGetValue(combatantId, out GameObject view) ||
                view == null)
            {
                return false;
            }

            SpriteRenderer spriteRenderer = view.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
                return false;

            Vector3 projected = camera.WorldToScreenPoint(spriteRenderer.bounds.center);
            if (projected.z <= 0f)
                return false;

            screenAnchor = projected;
            return true;
        }

        /// <summary>把一个仍合法且已有 View 的参与者投影为加入 padding 的屏幕候选。</summary>
        private bool TryCreateScreenCandidate(
            CombatantId targetId,
            Camera camera,
            out BattleTargetScreenCandidate candidate)
        {
            candidate = default;
            if (!IsLivingEnemyTarget(targetId)
                || !_views.TryGetValue(targetId, out GameObject view)
                || view == null)
            {
                return false;
            }

            SpriteRenderer spriteRenderer = view.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null
                || !TryProjectBoundsToScreenRect(camera, spriteRenderer.bounds, out Rect screenRect))
            {
                return false;
            }

            screenRect.xMin -= _targetHitPadding;
            screenRect.xMax += _targetHitPadding;
            screenRect.yMin -= _targetHitPadding;
            screenRect.yMax += _targetHitPadding;
            candidate = new BattleTargetScreenCandidate(targetId, screenRect);
            return true;
        }

        /// <summary>把世界轴对齐边界的八个角投影为当前 Camera 下的屏幕矩形。</summary>
        private static bool TryProjectBoundsToScreenRect(
            Camera camera,
            Bounds bounds,
            out Rect screenRect)
        {
            screenRect = default;
            bool hasVisibleCorner = false;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int xIndex = 0; xIndex < 2; xIndex++)
            {
                for (int yIndex = 0; yIndex < 2; yIndex++)
                {
                    for (int zIndex = 0; zIndex < 2; zIndex++)
                    {
                        var worldCorner = new Vector3(
                            xIndex == 0 ? bounds.min.x : bounds.max.x,
                            yIndex == 0 ? bounds.min.y : bounds.max.y,
                            zIndex == 0 ? bounds.min.z : bounds.max.z);
                        Vector3 projected = camera.WorldToScreenPoint(worldCorner);
                        if (projected.z <= 0f)
                            continue;

                        hasVisibleCorner = true;
                        minimum = Vector2.Min(minimum, projected);
                        maximum = Vector2.Max(maximum, projected);
                    }
                }
            }

            if (!hasVisibleCorner)
                return false;

            screenRect = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            return true;
        }

        /// <summary>从当前 HUD Canvas 或 Main Camera 取得唯一屏幕投影相机。</summary>
        private Camera ResolveTargetCamera()
        {
            return _hudCanvas != null && _hudCanvas.worldCamera != null
                ? _hudCanvas.worldCamera
                : Camera.main;
        }

        /// <summary>用当前参与者事实拒绝死亡、玩家、缺失或已离开本场的候选。</summary>
        private bool IsLivingEnemyTarget(CombatantId targetId)
        {
            if (_session == null)
                return true;

            return _session.Combatants.TryGet(targetId, out CombatantData combatant)
                   && combatant is EnemyCombatantData
                   && combatant.IsAlive;
        }

        /// <summary>刷新普通合法高亮与当前命中强化高亮，不创建第二份目标注册表。</summary>
        private void RefreshTargetHighlights(CombatantId? hoveredTargetId)
        {
            foreach (KeyValuePair<CombatantId, ParticipantHudView> entry in _hudViews)
            {
                if (entry.Value == null)
                    continue;

                bool isLegal = ContainsTargetId(_targetSelectionIds, entry.Key)
                               && IsLivingEnemyTarget(entry.Key)
                               && _views.TryGetValue(entry.Key, out GameObject view)
                               && view != null;
                entry.Value.SetTargetHighlight(
                    isLegal,
                    isLegal && hoveredTargetId.HasValue && hoveredTargetId.Value == entry.Key);
            }
        }

        /// <summary>按稳定列表顺序判断一个参与者标识是否已在候选集合中。</summary>
        private static bool ContainsTargetId(
            IReadOnlyList<CombatantId> targetIds,
            CombatantId targetId)
        {
            foreach (CombatantId candidateId in targetIds)
            {
                if (candidateId == targetId)
                    return true;
            }

            return false;
        }

        /// <summary>按当前会话先创建唯一玩家，再按 Encounter 顺序创建敌人。</summary>
        internal async UniTask CreateViewsAsync()
        {
            if (_isDestroyed)
                return;

            try
            {
                await CreatePlayerViewAsync();
                if (_isDestroyed)
                    return;

                await CreateEnemyViewsAsync();
            }
            catch
            {
                ReleaseCreatedViews();
                if (_isDestroyed)
                    return;

                throw;
            }
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
                if (_isDestroyed)
                    return;

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
                if (_isDestroyed)
                    return;
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
            if (_isDestroyed)
                return;

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

            GameObject view;
            try
            {
                view = _instantiateViewAsyncForTesting != null
                    ? await _instantiateViewAsyncForTesting(address, anchor)
                    : await InstantiateAddressableViewAsync(address, anchor);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Failed to load view for Combatant {combatantId}, template {templateId}, address '{address}'.",
                    exception);
            }

            if (_isDestroyed)
            {
                ReleaseParticipantView(view);
                return;
            }
            if (view.GetComponentInChildren<SpriteRenderer>() == null)
            {
                ReleaseParticipantView(view);
                throw new InvalidOperationException(
                    $"View for Combatant {combatantId}, template {templateId}, address '{address}' has no SpriteRenderer.");
            }

            view.transform.localPosition = localPosition;
            view.transform.localRotation = Quaternion.identity;
            RegisterParticipantView(combatantId, view);
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
                RegisterParticipantHud(combatantId, hudView);
            }
            catch
            {
                if (hudView != null)
                    DestroyParticipantHud(hudView.gameObject);
                _views.Remove(combatantId);
                ReleaseParticipantView(view);
                throw;
            }
        }

        /// <summary>用生产 Addressables 边界实例化一个参与者世界 View，并把句柄交给统一等待清理。</summary>
        private static async UniTask<GameObject> InstantiateAddressableViewAsync(
            string address,
            Transform anchor)
        {
            AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(address, anchor);
            return await AwaitAddressableViewAsync(handle);
        }

        /// <summary>等待生产实例句柄；失败或空结果先精确释放，再保留异常语义。</summary>
        internal static async UniTask<GameObject> AwaitAddressableViewAsync(
            AsyncOperationHandle<GameObject> handle)
        {
            GameObject view;
            try
            {
                view = await handle.Task;
            }
            catch
            {
                ReleaseFailedViewHandle(handle);
                throw;
            }

            if (handle.Status == AsyncOperationStatus.Succeeded && view != null)
                return view;

            Exception operationException = handle.OperationException;
            ReleaseFailedViewHandle(handle);
            throw new InvalidOperationException(
                "Addressables participant view operation did not return a valid instance.",
                operationException);
        }

        /// <summary>释放失败或空结果的参与者实例句柄；无效句柄保持幂等无操作。</summary>
        internal static void ReleaseFailedViewHandle(AsyncOperationHandle<GameObject> handle)
        {
            if (handle.IsValid())
                Addressables.ReleaseInstance(handle);
        }

        /// <summary>释放当前 Presenter 已创建的 HUD 与世界 View，供销毁和部分失败共用。</summary>
        private void ReleaseCreatedViews()
        {
            foreach (ParticipantHudView hudView in _hudViews.Values)
            {
                if (hudView != null)
                    DestroyParticipantHud(hudView.gameObject);
            }

            _hudViews.Clear();
            foreach (GameObject view in _views.Values)
            {
                if (view != null)
                    ReleaseParticipantView(view);
            }

            _views.Clear();
        }

        /// <summary>经当前 Presenter 唯一资源边界释放一个已创建世界 View。</summary>
        private void ReleaseParticipantView(GameObject view)
        {
            if (view == null)
                return;
            if (_releaseViewForTesting != null)
            {
                _releaseViewForTesting(view);
                return;
            }

            Addressables.ReleaseInstance(view);
        }

        /// <summary>经当前 Presenter 唯一资源边界销毁一个非权威 HUD 实例。</summary>
        private void DestroyParticipantHud(GameObject hudObject)
        {
            if (hudObject == null)
                return;
            if (_destroyHudForTesting != null)
            {
                _destroyHudForTesting(hudObject);
                return;
            }

            Destroy(hudObject);
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
