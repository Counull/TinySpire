using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using cfg;
using Cysharp.Threading.Tasks;
using R3;
using TinySpire.Run;
using TinySpire.Run.Map;
using UnityEngine.Localization;
using VContainer;
using VContainer.Unity;

namespace TinySpire.UI.Run
{
    /// <summary>RunEntryScene 内可见的互斥页面。</summary>
    public enum RunEntryPage
    {
        MainMenu,
        HeroSelection,
        Settings,
        Compendium,
        Statistics,
        Map,
        CardReward,
        Rest,
        Failure,
        AbandonConfirmation,
        SaveFailure,
        RollbackConfirmation,
        Chest,
        Shop,
        Event,
    }

    /// <summary>入口 View 可提交给 Presenter 的有限动作集合。</summary>
    public enum RunEntryActionKind
    {
        StartGame,
        OpenSettings,
        OpenCompendium,
        OpenStatistics,
        Back,
        SelectHero,
        ConfirmHero,
        EnterMapNode,
        LeaveTerminalRun,
        ContinueGame,
        ConfirmAbandon,
        RetrySave,
        RequestExitAfterSaveFailure,
        ConfirmRollback,
        SelectCardReward,
        SkipCardReward,
        HealAtRest,
        UpgradeCardAtRest,
        ClaimChest,
        SkipChest,
        PurchaseShopStock,
        LeaveShop,
        ChooseEvent,
    }

    /// <summary>入口投影中每个 TMP 文本的稳定槽位。</summary>
    public enum RunEntryTextSlot
    {
        MainTitle,
        StartGame,
        Settings,
        Compendium,
        Statistics,
        Back,
        ComingSoon,
        SettingsTitle,
        SettingsPlaceholder,
        HeroTitle,
        Hero1001Name,
        Hero1002Name,
        ConfirmHero,
        FutureSlot,
        MapTitle,
        BattleNode,
        Cleared,
        Health,
        CardRewardTitle,
        SkipCardReward,
        RestTitle,
        FailureTitle,
        LeaveRun,
        ContinueGame,
        Cancel,
        ConfirmationTitle,
        ConfirmationMessage,
        ConfirmationConfirm,
        SaveIssueTitle,
        SaveIssue,
        SaveFailureMessage,
        RetrySave,
        Exit,
        RollbackTitle,
        RollbackMessage,
        RollbackConfirm,
        ChestTitle,
        ShopTitle,
        EventTitle,
    }

    /// <summary>View 发出的单个不可变入口动作；选择类动作只携带对应领域身份。</summary>
    public readonly struct RunEntryAction
    {
        /// <summary>动作类型。</summary>
        public RunEntryActionKind Kind { get; }

        /// <summary>选择动作携带的 Hero 模板标识，其余动作为空。</summary>
        public int? HeroTemplateId { get; }

        /// <summary>地图节点动作携带的稳定节点身份，其余动作为空。</summary>
        public MapNodeId? MapNodeId { get; }

        /// <summary>奖励动作携带的稳定奖励身份，其余动作为空。</summary>
        public RunCardRewardId? CardRewardId { get; }

        /// <summary>选择奖励动作携带的卡牌模板标识，其余动作为空。</summary>
        public int? CardTemplateId { get; }

        /// <summary>非战斗节点结算动作携带的稳定访问身份，其余动作为空。</summary>
        public RunNodeVisitId? NodeVisitId { get; }

        /// <summary>Rest 升级动作携带的稳定卡牌实例身份，其余动作为空。</summary>
        public RunCardInstanceId? CardInstanceId { get; }

        /// <summary>Shop 购买动作携带的专用库存条目标识，其余动作为空。</summary>
        public int? ShopStockEntryId { get; }

        /// <summary>Event 动作携带的闭合类型化选择，其余动作为空。</summary>
        public RunEventChoiceKind? EventChoice { get; }

        /// <summary>创建并验证一个入口 UI 意图。</summary>
        public RunEntryAction(
            RunEntryActionKind kind,
            int? heroTemplateId = null,
            MapNodeId? mapNodeId = null,
            RunCardRewardId? cardRewardId = null,
            int? cardTemplateId = null,
            RunNodeVisitId? nodeVisitId = null,
            RunCardInstanceId? cardInstanceId = null,
            int? shopStockEntryId = null,
            RunEventChoiceKind? eventChoice = null)
        {
            if (kind == RunEntryActionKind.SelectHero)
            {
                if (!heroTemplateId.HasValue || heroTemplateId.Value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            }
            else if (heroTemplateId.HasValue)
            {
                throw new ArgumentException(
                    "Only SelectHero actions may carry a hero template id.",
                    nameof(heroTemplateId));
            }


            if (kind == RunEntryActionKind.EnterMapNode)
            {
                if (mapNodeId == null || string.IsNullOrEmpty(mapNodeId.Value.Value))
                    throw new ArgumentException("EnterMapNode requires a stable node id.", nameof(mapNodeId));
            }
            else if (mapNodeId.HasValue)
            {
                throw new ArgumentException(
                    "Only EnterMapNode actions may carry a map node id.",
                    nameof(mapNodeId));
            }

            if (kind == RunEntryActionKind.SelectCardReward)
            {
                if (!IsValidCardRewardId(cardRewardId) ||
                    !cardTemplateId.HasValue ||
                    cardTemplateId.Value <= 0)
                {
                    throw new ArgumentException(
                        "SelectCardReward requires a stable reward id and a positive card template id.");
                }
            }
            else if (kind == RunEntryActionKind.SkipCardReward)
            {
                if (!IsValidCardRewardId(cardRewardId) || cardTemplateId.HasValue)
                {
                    throw new ArgumentException(
                        "SkipCardReward requires only a stable reward id.");
                }
            }
            else if (cardRewardId.HasValue || cardTemplateId.HasValue)
            {
                throw new ArgumentException(
                    "Only card reward actions may carry reward payload.");
            }


            if (kind == RunEntryActionKind.HealAtRest)
            {
                if (!IsValidNodeVisitId(nodeVisitId) ||
                    cardInstanceId.HasValue ||
                    shopStockEntryId.HasValue ||
                    eventChoice.HasValue)
                {
                    throw new ArgumentException(
                        "HealAtRest requires only a stable node visit id.");
                }
            }
            else if (kind == RunEntryActionKind.UpgradeCardAtRest)
            {
                if (!IsValidNodeVisitId(nodeVisitId) ||
                    !cardInstanceId.HasValue ||
                    cardInstanceId.Value.Sequence <= 0 ||
                    shopStockEntryId.HasValue ||
                    eventChoice.HasValue)
                {
                    throw new ArgumentException(
                        "UpgradeCardAtRest requires a stable visit id and card instance id.");
                }
            }
            else if (kind == RunEntryActionKind.ClaimChest ||
                     kind == RunEntryActionKind.SkipChest)
            {
                if (!IsValidNodeVisitId(nodeVisitId) ||
                    cardInstanceId.HasValue ||
                    shopStockEntryId.HasValue ||
                    eventChoice.HasValue)
                {
                    throw new ArgumentException(
                    "Chest actions require only a stable node visit id.");
                }
            }
            else if (kind == RunEntryActionKind.PurchaseShopStock)
            {
                if (!IsValidNodeVisitId(nodeVisitId) ||
                    cardInstanceId.HasValue ||
                    !shopStockEntryId.HasValue ||
                    shopStockEntryId.Value <= 0 ||
                    eventChoice.HasValue)
                {
                    throw new ArgumentException(
                        "PurchaseShopStock requires a stable visit id and positive stock entry id.");
                }
            }
            else if (kind == RunEntryActionKind.LeaveShop)
            {
                if (!IsValidNodeVisitId(nodeVisitId) ||
                    cardInstanceId.HasValue ||
                    shopStockEntryId.HasValue ||
                    eventChoice.HasValue)
                {
                    throw new ArgumentException(
                        "LeaveShop requires only a stable node visit id.");
                }
            }
            else if (kind == RunEntryActionKind.ChooseEvent)
            {
                if (!IsValidNodeVisitId(nodeVisitId) ||
                    cardInstanceId.HasValue ||
                    shopStockEntryId.HasValue ||
                    !eventChoice.HasValue ||
                    !Enum.IsDefined(typeof(RunEventChoiceKind), eventChoice.Value))
                {
                    throw new ArgumentException(
                        "ChooseEvent requires a stable visit id and defined Event choice.");
                }
            }
            else if (nodeVisitId.HasValue ||
                     cardInstanceId.HasValue ||
                     shopStockEntryId.HasValue ||
                     eventChoice.HasValue)
            {
                throw new ArgumentException(
                    "Only non-combat node settlement actions may carry node visit payload.");
            }

            Kind = kind;
            HeroTemplateId = heroTemplateId;
            MapNodeId = mapNodeId;
            CardRewardId = cardRewardId;
            CardTemplateId = cardTemplateId;
            NodeVisitId = nodeVisitId;
            CardInstanceId = cardInstanceId;
            ShopStockEntryId = shopStockEntryId;
            EventChoice = eventChoice;
        }

        /// <summary>检查可空奖励身份是否含完整 Run、attempt 与节点事实。</summary>
        private static bool IsValidCardRewardId(RunCardRewardId? rewardId)
        {
            return rewardId.HasValue &&
                   rewardId.Value.BattleId.RunId.Value != Guid.Empty &&
                   rewardId.Value.BattleId.AttemptSequence > 0 &&
                   !string.IsNullOrEmpty(rewardId.Value.BattleId.NodeId.Value);
        }

        /// <summary>检查可空访问身份是否含完整 Run 与节点事实。</summary>
        private static bool IsValidNodeVisitId(RunNodeVisitId? visitId)
        {
            return visitId.HasValue &&
                   visitId.Value.RunId.Value != Guid.Empty &&
                   !string.IsNullOrEmpty(visitId.Value.NodeId.Value);
        }
    }

    /// <summary>地图节点在当前 Run 投影中的互斥功能状态。</summary>
    public enum RunMapNodePresentationState
    {
        Locked,
        Selectable,
        Completed,
        Current,
        BossGateReached,
    }

    /// <summary>地图节点使用的轻量程序化视觉锚点；Boss 候选以不同轮廓保持开局可区分。</summary>
    public enum RunMapVisualAnchorKind
    {
        StartFlag,
        EncounterSlimeSilhouette,
        EncounterSentrySilhouette,
        RestCampfire,
        ChestCache,
        ShopBag,
        EventQuestionMark,
        BossAlphaCrown,
        BossBetaHorns,
        BossGammaEye,
    }

    /// <summary>由静态内容身份解析出的只读显示描述，不进入 MapDefinition 或 Run 存档。</summary>
    public sealed class RunMapIdentityDescriptor
    {
        public string DisplayName { get; }
        public RunMapVisualAnchorKind VisualAnchorKind { get; }

        /// <summary>冻结玩家可见名称与程序化视觉锚点种类。</summary>
        public RunMapIdentityDescriptor(
            string displayName,
            RunMapVisualAnchorKind visualAnchorKind)
        {
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Map identity display name cannot be empty.", nameof(displayName));
            if (!Enum.IsDefined(typeof(RunMapVisualAnchorKind), visualAnchorKind))
                throw new ArgumentOutOfRangeException(nameof(visualAnchorKind));

            DisplayName = displayName;
            VisualAnchorKind = visualAnchorKind;
        }
    }

    /// <summary>把冻结 EncounterId/BossId 解析为当前语言只读展示身份的单一 seam。</summary>
    public interface IRunMapIdentityCatalog
    {
        /// <summary>读取指定节点内容身份的名称与视觉锚点，不写入任何 Run 事实。</summary>
        RunMapIdentityDescriptor Resolve(MapNodeKind kind, int contentId);
    }

    /// <summary>从 Luban Encounter 首敌解析名称，并提供 G3 明确 Boss 测试身份的目录适配器。</summary>
    public sealed class RunMapIdentityCatalog : IRunMapIdentityCatalog
    {
        private readonly Func<Tables> _tablesProvider;
        private readonly Func<string, IReadOnlyDictionary<string, object>, string> _localize;

        /// <summary>以生产配置与本地化服务创建只读地图身份目录。</summary>
        [Inject]
        public RunMapIdentityCatalog(
            ConfigService configs,
            LocalizationService localization)
            : this(
                CreateTablesProvider(configs),
                CreateLocalizer(localization))
        {
        }

        /// <summary>以可替换表与本地化 seam 创建可直接 EditMode 验证的身份目录。</summary>
        internal RunMapIdentityCatalog(
            Func<Tables> tablesProvider,
            Func<string, IReadOnlyDictionary<string, object>, string> localize)
        {
            _tablesProvider = tablesProvider ?? throw new ArgumentNullException(nameof(tablesProvider));
            _localize = localize ?? throw new ArgumentNullException(nameof(localize));
        }

        /// <summary>按节点种类解析开局明牌身份，并拒绝未定义的内容 ID。</summary>
        public RunMapIdentityDescriptor Resolve(MapNodeKind kind, int contentId)
        {
            switch (kind)
            {
                case MapNodeKind.Start when contentId == 0:
                    return new RunMapIdentityDescriptor(
                        "START",
                        RunMapVisualAnchorKind.StartFlag);
                case MapNodeKind.Start:
                    throw new InvalidOperationException("Start map identity must use content id 0.");
                case MapNodeKind.Combat:
                    return ResolveEncounter(contentId);
                case MapNodeKind.Rest:
                case MapNodeKind.Chest:
                case MapNodeKind.Shop:
                case MapNodeKind.Event:
                    return ResolveG6ProgrammaticIdentity(kind, contentId);
                case MapNodeKind.Boss:
                    return ResolveG3BossTestIdentity(contentId);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>为 G6 非战斗节点解析唯一程序化内容 anchor，不依赖尚未进入的配置表或结算实现。</summary>
        private static RunMapIdentityDescriptor ResolveG6ProgrammaticIdentity(
            MapNodeKind kind,
            int contentId)
        {
            switch (kind)
            {
                case MapNodeKind.Rest when contentId == RunNodeVisitIdentityCatalog.RestContentId:
                    return new RunMapIdentityDescriptor(
                        "REST",
                        RunMapVisualAnchorKind.RestCampfire);
                case MapNodeKind.Chest when contentId == RunNodeVisitIdentityCatalog.ChestContentId:
                    return new RunMapIdentityDescriptor(
                        "CHEST",
                        RunMapVisualAnchorKind.ChestCache);
                case MapNodeKind.Shop when contentId == RunNodeVisitIdentityCatalog.ShopContentId:
                    return new RunMapIdentityDescriptor(
                        "SHOP",
                        RunMapVisualAnchorKind.ShopBag);
                case MapNodeKind.Event when contentId == RunNodeVisitIdentityCatalog.EventContentId:
                    return new RunMapIdentityDescriptor(
                        "EVENT",
                        RunMapVisualAnchorKind.EventQuestionMark);
                default:
                    throw new InvalidOperationException(
                        $"G6 programmatic map identity {kind}/{contentId} is not defined.");
            }
        }

        /// <summary>从 Encounter 的首个 TbEnemy 读取 NameI18nKey 并解析当前语言名称。</summary>
        private RunMapIdentityDescriptor ResolveEncounter(int encounterId)
        {
            Tables tables = _tablesProvider()
                ?? throw new InvalidOperationException(
                    "ConfigService must be initialized before resolving map identities.");
            cfg.battle.Encounter encounter = tables.TbEncounter.GetOrDefault(encounterId)
                ?? throw new InvalidOperationException($"Encounter template {encounterId} does not exist.");
            if (encounter.EnemyTemplateIds == null || encounter.EnemyTemplateIds.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Encounter template {encounterId} has no enemy identity to present.");
            }

            int enemyId = encounter.EnemyTemplateIds[0];
            cfg.battle.Enemy enemy = tables.TbEnemy.GetOrDefault(enemyId)
                ?? throw new InvalidOperationException($"Enemy template {enemyId} does not exist.");
            if (string.IsNullOrWhiteSpace(enemy.NameI18nKey))
                throw new InvalidOperationException($"Enemy template {enemyId} has no name localization key.");

            string mainEnemyName = _localize(enemy.NameI18nKey, null);
            if (string.IsNullOrWhiteSpace(mainEnemyName))
            {
                throw new InvalidOperationException(
                    $"Enemy template {enemyId} resolved an empty display name.");
            }

            return ResolveG3EncounterTestIdentity(encounterId, enemyId, mainEnemyName);
        }

        /// <summary>解析当前 G3 profile 的 5001 与仅供目录判别测试的 5002，并把首敌身份绑定到稳定剪影。</summary>
        private static RunMapIdentityDescriptor ResolveG3EncounterTestIdentity(
            int encounterId,
            int mainEnemyId,
            string mainEnemyName)
        {
            switch (encounterId)
            {
                case 5001 when mainEnemyId == 2001:
                    return new RunMapIdentityDescriptor(
                        $"SLIME PATROL\n{mainEnemyName}",
                        RunMapVisualAnchorKind.EncounterSlimeSilhouette);
                case 5002 when mainEnemyId == 2101:
                    return new RunMapIdentityDescriptor(
                        $"SENTRY LINE\n{mainEnemyName}",
                        RunMapVisualAnchorKind.EncounterSentrySilhouette);
                default:
                    throw new InvalidOperationException(
                        $"G3 test Encounter identity {encounterId} with main enemy {mainEnemyId} is not defined.");
            }
        }

        /// <summary>解析仅供 G3 地图闭环使用的三名测试 Boss 身份，不冒充真实 Boss 配置。</summary>
        private static RunMapIdentityDescriptor ResolveG3BossTestIdentity(int bossId)
        {
            switch (bossId)
            {
                case 9001:
                    return new RunMapIdentityDescriptor(
                        "BOSS ALPHA",
                        RunMapVisualAnchorKind.BossAlphaCrown);
                case 9002:
                    return new RunMapIdentityDescriptor(
                        "BOSS BETA",
                        RunMapVisualAnchorKind.BossBetaHorns);
                case 9003:
                    return new RunMapIdentityDescriptor(
                        "BOSS GAMMA",
                        RunMapVisualAnchorKind.BossGammaEye);
                default:
                    throw new InvalidOperationException(
                        $"G3 test Boss identity {bossId} is not defined.");
            }
        }

        /// <summary>从生产 ConfigService 延迟读取初始化完成后的 Luban 表。</summary>
        private static Func<Tables> CreateTablesProvider(ConfigService configs)
        {
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));

            return () => configs.Tables;
        }

        /// <summary>把生产 LocalizationService 适配为身份目录的只读文本函数。</summary>
        private static Func<string, IReadOnlyDictionary<string, object>, string> CreateLocalizer(
            LocalizationService localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            return localization.GetString;
        }
    }

    /// <summary>一个明牌地图节点及其悬停后半程的不可变 View 投影。</summary>
    public sealed class RunMapNodeViewModel
    {
        private readonly ReadOnlyCollection<string> _downstreamNodeIds;
        private readonly ReadOnlyCollection<string> _downstreamEdgeKeys;

        public string NodeId { get; }
        public int Layer { get; }
        public int Slot { get; }
        public MapNodeKind Kind { get; }
        public int ContentId { get; }
        public string DisplayName { get; }
        public RunMapVisualAnchorKind VisualAnchorKind { get; }
        public RunMapNodePresentationState State { get; }
        public IReadOnlyList<string> DownstreamNodeIds => _downstreamNodeIds;
        public IReadOnlyList<string> DownstreamEdgeKeys => _downstreamEdgeKeys;

        /// <summary>冻结一个节点的布局、明牌身份、交互状态与纯派生后半程。</summary>
        public RunMapNodeViewModel(
            string nodeId,
            int layer,
            int slot,
            MapNodeKind kind,
            int contentId,
            string displayName,
            RunMapVisualAnchorKind visualAnchorKind,
            RunMapNodePresentationState state,
            IReadOnlyList<string> downstreamNodeIds,
            IReadOnlyList<string> downstreamEdgeKeys)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Map node id cannot be empty.", nameof(nodeId));
            if (layer < 0)
                throw new ArgumentOutOfRangeException(nameof(layer));
            if (slot < 0)
                throw new ArgumentOutOfRangeException(nameof(slot));
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Map node display name cannot be empty.", nameof(displayName));
            ValidateVisualAnchor(kind, visualAnchorKind);

            NodeId = nodeId;
            Layer = layer;
            Slot = slot;
            Kind = kind;
            ContentId = contentId;
            DisplayName = displayName;
            VisualAnchorKind = visualAnchorKind;
            State = state;
            _downstreamNodeIds = Array.AsReadOnly(
                (downstreamNodeIds ?? throw new ArgumentNullException(nameof(downstreamNodeIds))).ToArray());
            _downstreamEdgeKeys = Array.AsReadOnly(
                (downstreamEdgeKeys ?? throw new ArgumentNullException(nameof(downstreamEdgeKeys))).ToArray());
        }

        /// <summary>约束节点种类与视觉锚点种类一致，避免 View 猜测内容身份。</summary>
        private static void ValidateVisualAnchor(
            MapNodeKind nodeKind,
            RunMapVisualAnchorKind visualAnchorKind)
        {
            bool isValid;
            switch (nodeKind)
            {
                case MapNodeKind.Start:
                    isValid = visualAnchorKind == RunMapVisualAnchorKind.StartFlag;
                    break;
                case MapNodeKind.Combat:
                    isValid = visualAnchorKind == RunMapVisualAnchorKind.EncounterSlimeSilhouette ||
                              visualAnchorKind == RunMapVisualAnchorKind.EncounterSentrySilhouette;
                    break;
                case MapNodeKind.Rest:
                    isValid = visualAnchorKind == RunMapVisualAnchorKind.RestCampfire;
                    break;
                case MapNodeKind.Chest:
                    isValid = visualAnchorKind == RunMapVisualAnchorKind.ChestCache;
                    break;
                case MapNodeKind.Shop:
                    isValid = visualAnchorKind == RunMapVisualAnchorKind.ShopBag;
                    break;
                case MapNodeKind.Event:
                    isValid = visualAnchorKind == RunMapVisualAnchorKind.EventQuestionMark;
                    break;
                case MapNodeKind.Boss:
                    isValid = visualAnchorKind == RunMapVisualAnchorKind.BossAlphaCrown ||
                              visualAnchorKind == RunMapVisualAnchorKind.BossBetaHorns ||
                              visualAnchorKind == RunMapVisualAnchorKind.BossGammaEye;
                    break;
                default:
                    isValid = false;
                    break;
            }
            if (!isValid)
            {
                throw new ArgumentException(
                    $"Visual anchor '{visualAnchorKind}' is invalid for map node kind '{nodeKind}'.",
                    nameof(visualAnchorKind));
            }
        }
    }

    /// <summary>一个地图节点在 820×480 宿主内的确定性矩形布局。</summary>
    internal sealed class RunMapNodeLayout
    {
        public string NodeId { get; }
        public float CenterX { get; }
        public float CenterY { get; }
        public float Width { get; }
        public float Height { get; }
        public float Left => CenterX - Width * 0.5f;
        public float Right => CenterX + Width * 0.5f;
        public float Bottom => CenterY - Height * 0.5f;
        public float Top => CenterY + Height * 0.5f;

        /// <summary>冻结一个节点中心与正尺寸，供 View 和纯布局测试共享。</summary>
        internal RunMapNodeLayout(
            string nodeId,
            float centerX,
            float centerY,
            float width,
            float height)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Map layout node id cannot be empty.", nameof(nodeId));
            if (width <= 0f)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(height));

            NodeId = nodeId;
            CenterX = centerX;
            CenterY = centerY;
            Width = width;
            Height = height;
        }
    }

    /// <summary>把短 G3 图与八层 G6 mixed 图确定性排入同一个固定地图宿主。</summary>
    internal static class RunMapLayout
    {
        /// <summary>按 Layer/Slot 生成只读矩形；六层以上自动使用无重叠紧凑规格。</summary>
        internal static IReadOnlyList<RunMapNodeLayout> Build(
            IReadOnlyList<RunMapNodeViewModel> nodes)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));
            if (nodes.Count == 0)
                return Array.Empty<RunMapNodeLayout>();

            int maxLayer = nodes.Max(node => node.Layer);
            bool compact = maxLayer >= 6;
            float horizontalExtent = compact ? 300f : 315f;
            float verticalExtent = compact ? 200f : 185f;
            var layouts = new List<RunMapNodeLayout>(nodes.Count);
            foreach (IGrouping<int, RunMapNodeViewModel> layer in nodes.GroupBy(node => node.Layer))
            {
                RunMapNodeViewModel[] layerNodes = layer.OrderBy(node => node.Slot).ToArray();
                int maxSlot = layerNodes.Length == 0 ? 0 : layerNodes.Max(node => node.Slot);
                foreach (RunMapNodeViewModel node in layerNodes)
                {
                    float centerX = maxSlot == 0
                        ? 0f
                        : -horizontalExtent +
                          horizontalExtent * 2f * (node.Slot / (float)maxSlot);
                    float centerY = maxLayer == 0
                        ? 0f
                        : -verticalExtent +
                          verticalExtent * 2f * (node.Layer / (float)maxLayer);
                    bool boss = node.Kind == MapNodeKind.Boss;
                    layouts.Add(new RunMapNodeLayout(
                        node.NodeId,
                        centerX,
                        centerY,
                        compact ? (boss ? 156f : 144f) : (boss ? 196f : 176f),
                        compact ? (boss ? 52f : 46f) : (boss ? 98f : 88f)));
                }
            }

            return Array.AsReadOnly(layouts.ToArray());
        }
    }

    /// <summary>一条冻结地图边的稳定 View 投影。</summary>
    public sealed class RunMapEdgeViewModel
    {
        public string Key { get; }
        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public bool IsCompletedPath { get; }

        /// <summary>冻结一条边的端点与已走路径表现。</summary>
        public RunMapEdgeViewModel(
            string fromNodeId,
            string toNodeId,
            bool isCompletedPath)
        {
            if (string.IsNullOrWhiteSpace(fromNodeId))
                throw new ArgumentException("From node id cannot be empty.", nameof(fromNodeId));
            if (string.IsNullOrWhiteSpace(toNodeId))
                throw new ArgumentException("To node id cannot be empty.", nameof(toNodeId));

            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Key = $"{fromNodeId}>{toNodeId}";
            IsCompletedPath = isCompletedPath;
        }
    }

    /// <summary>整张冻结 Act 地图的功能性、无业务写入 View 投影。</summary>
    public sealed class RunMapViewModel
    {
        private readonly ReadOnlyCollection<RunMapNodeViewModel> _nodes;
        private readonly ReadOnlyCollection<RunMapEdgeViewModel> _edges;

        public string Fingerprint { get; }
        public IReadOnlyList<RunMapNodeViewModel> Nodes => _nodes;
        public IReadOnlyList<RunMapEdgeViewModel> Edges => _edges;

        /// <summary>冻结地图指纹、全部节点和全部边。</summary>
        public RunMapViewModel(
            string fingerprint,
            IReadOnlyList<RunMapNodeViewModel> nodes,
            IReadOnlyList<RunMapEdgeViewModel> edges)
        {
            if (string.IsNullOrWhiteSpace(fingerprint))
                throw new ArgumentException("Map fingerprint cannot be empty.", nameof(fingerprint));

            Fingerprint = fingerprint;
            _nodes = Array.AsReadOnly(
                (nodes ?? throw new ArgumentNullException(nameof(nodes))).ToArray());
            _edges = Array.AsReadOnly(
                (edges ?? throw new ArgumentNullException(nameof(edges))).ToArray());
        }
    }

    /// <summary>奖励页上一张卡牌的已本地化只读投影。</summary>
    public sealed class RunCardRewardCandidateViewModel
    {
        /// <summary>结算命令所需的稳定卡牌模板标识。</summary>
        public int TemplateId { get; }

        /// <summary>当前语言下的卡牌名称。</summary>
        public string Name { get; }

        /// <summary>当前语言下的卡牌规则描述。</summary>
        public string Description { get; }

        /// <summary>当前等级的最终费用文本。</summary>
        public string CostText { get; }

        /// <summary>冻结单张候选的身份与当前语言文本。</summary>
        public RunCardRewardCandidateViewModel(
            int templateId,
            string name,
            string description,
            string costText)
        {
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Card reward name cannot be empty.", nameof(name));
            if (description == null)
                throw new ArgumentNullException(nameof(description));
            if (string.IsNullOrWhiteSpace(costText))
                throw new ArgumentException("Card reward cost text cannot be empty.", nameof(costText));

            TemplateId = templateId;
            Name = name;
            Description = description;
            CostText = costText;
        }
    }

    /// <summary>奖励身份、固定三候选及当前交互门禁的完整页面投影。</summary>
    public sealed class RunCardRewardViewModel
    {
        private readonly ReadOnlyCollection<RunCardRewardCandidateViewModel> _candidates;

        /// <summary>选择或跳过必须回传的稳定奖励身份。</summary>
        public RunCardRewardId RewardId { get; }

        /// <summary>按冻结展示顺序排列的恰好三张候选。</summary>
        public IReadOnlyList<RunCardRewardCandidateViewModel> Candidates => _candidates;

        /// <summary>存档提交期间关闭选择与跳过，阻止重复命令。</summary>
        public bool ActionsEnabled { get; }

        /// <summary>验证并冻结奖励页所需的最小投影。</summary>
        public RunCardRewardViewModel(
            RunCardRewardId rewardId,
            IReadOnlyList<RunCardRewardCandidateViewModel> candidates,
            bool actionsEnabled)
        {
            if (rewardId.BattleId.RunId.Value == Guid.Empty ||
                rewardId.BattleId.AttemptSequence <= 0 ||
                string.IsNullOrEmpty(rewardId.BattleId.NodeId.Value))
            {
                throw new ArgumentException("Card reward id cannot be empty.", nameof(rewardId));
            }
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            if (candidates.Count != RunCardRewardGenerator.CandidateCount ||
                candidates.Any(candidate => candidate == null) ||
                candidates.Select(candidate => candidate.TemplateId).Distinct().Count() !=
                RunCardRewardGenerator.CandidateCount)
            {
                throw new ArgumentException(
                    "Card reward projection must contain exactly three distinct candidates.",
                    nameof(candidates));
            }

            RewardId = rewardId;
            _candidates = Array.AsReadOnly(candidates.ToArray());
            ActionsEnabled = actionsEnabled;
        }
    }

    /// <summary>Rest 页上一张冻结升级候选的实例身份、文本与交互门禁。</summary>
    public sealed class RunRestUpgradeCandidateViewModel
    {
        /// <summary>升级命令必须回传的稳定 Run 卡牌实例身份。</summary>
        public RunCardInstanceId CardInstanceId { get; }

        /// <summary>候选当前引用的静态卡牌模板身份。</summary>
        public int TemplateId { get; }

        /// <summary>候选当前升级等级。</summary>
        public int CurrentUpgradeLevel { get; }

        /// <summary>填入卡名与下一等级后的当前语言按钮文本。</summary>
        public string Text { get; }

        /// <summary>当前存档与配置门禁下是否允许发出升级动作。</summary>
        public bool Enabled { get; }

        /// <summary>验证并冻结单张 Rest 升级候选投影。</summary>
        public RunRestUpgradeCandidateViewModel(
            RunCardInstanceId cardInstanceId,
            int templateId,
            int currentUpgradeLevel,
            string text,
            bool enabled)
        {
            if (cardInstanceId.Sequence <= 0)
                throw new ArgumentException("Rest card instance id cannot be empty.", nameof(cardInstanceId));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (currentUpgradeLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(currentUpgradeLevel));
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Rest upgrade text cannot be empty.", nameof(text));

            CardInstanceId = cardInstanceId;
            TemplateId = templateId;
            CurrentUpgradeLevel = currentUpgradeLevel;
            Text = text;
            Enabled = enabled;
        }
    }

    /// <summary>Rest 访问身份、治疗动作与冻结有序升级候选的完整页面投影。</summary>
    public sealed class RunRestViewModel
    {
        private readonly ReadOnlyCollection<RunRestUpgradeCandidateViewModel> _upgradeCandidates;

        /// <summary>Heal 或 Upgrade 动作必须回传的稳定访问身份。</summary>
        public RunNodeVisitId VisitId { get; }

        /// <summary>进入 Rest 时冻结的治疗量。</summary>
        public int HealAmount { get; }

        /// <summary>填入冻结治疗量后的当前语言按钮文本。</summary>
        public string HealText { get; }

        /// <summary>只有英雄受伤且没有存档提交时才允许治疗。</summary>
        public bool HealEnabled { get; }

        /// <summary>按 Pending 冻结顺序排列的升级候选。</summary>
        public IReadOnlyList<RunRestUpgradeCandidateViewModel> UpgradeCandidates =>
            _upgradeCandidates;

        /// <summary>验证访问身份并防御性冻结全部 Rest 选择。</summary>
        public RunRestViewModel(
            RunNodeVisitId visitId,
            int healAmount,
            string healText,
            bool healEnabled,
            IReadOnlyList<RunRestUpgradeCandidateViewModel> upgradeCandidates)
        {
            if (visitId.RunId.Value == Guid.Empty || string.IsNullOrEmpty(visitId.NodeId.Value))
                throw new ArgumentException("Rest visit id cannot be empty.", nameof(visitId));
            if (healAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(healAmount));
            if (string.IsNullOrWhiteSpace(healText))
                throw new ArgumentException("Rest heal text cannot be empty.", nameof(healText));
            if (upgradeCandidates == null)
                throw new ArgumentNullException(nameof(upgradeCandidates));
            RunRestUpgradeCandidateViewModel[] frozenCandidates = upgradeCandidates.ToArray();
            if (frozenCandidates.Any(candidate => candidate == null) ||
                frozenCandidates.Select(candidate => candidate.CardInstanceId).Distinct().Count() !=
                frozenCandidates.Length)
            {
                throw new ArgumentException(
                    "Rest upgrade candidates must be non-null and instance-distinct.",
                    nameof(upgradeCandidates));
            }

            VisitId = visitId;
            HealAmount = healAmount;
            HealText = healText;
            HealEnabled = healEnabled;
            _upgradeCandidates = Array.AsReadOnly(frozenCandidates);
        }
    }

    /// <summary>Chest 访问身份、冻结药水奖励与领取/跳过可用性的完整页面投影。</summary>
    public sealed class RunChestViewModel
    {
        /// <summary>领取或跳过动作必须回传的稳定访问身份。</summary>
        public RunNodeVisitId VisitId { get; }

        /// <summary>由冻结 PotionTemplateId 与当前配置构建的本地化奖励投影。</summary>
        public RunHoldingItemViewModel Potion { get; }

        /// <summary>当前语言的领取按钮文本。</summary>
        public string ClaimText { get; }

        /// <summary>当前语言的跳过按钮文本。</summary>
        public string SkipText { get; }

        /// <summary>药水槽满时显示的当前语言说明。</summary>
        public string CapacityFullText { get; }

        /// <summary>只有容量可用且没有存档提交时才允许领取。</summary>
        public bool ClaimEnabled { get; }

        /// <summary>只要没有存档提交就允许跳过。</summary>
        public bool SkipEnabled { get; }

        /// <summary>当前三槽容量是否已满，仅供表现显示说明。</summary>
        public bool IsCapacityFull { get; }

        /// <summary>验证身份、奖励与文本后冻结完整 Chest 页面投影。</summary>
        public RunChestViewModel(
            RunNodeVisitId visitId,
            RunHoldingItemViewModel potion,
            string claimText,
            string skipText,
            string capacityFullText,
            bool claimEnabled,
            bool skipEnabled,
            bool isCapacityFull)
        {
            if (visitId.RunId.Value == Guid.Empty || string.IsNullOrEmpty(visitId.NodeId.Value))
                throw new ArgumentException("Chest visit id cannot be empty.", nameof(visitId));
            Potion = potion ?? throw new ArgumentNullException(nameof(potion));
            if (string.IsNullOrWhiteSpace(claimText))
                throw new ArgumentException("Chest claim text cannot be empty.", nameof(claimText));
            if (string.IsNullOrWhiteSpace(skipText))
                throw new ArgumentException("Chest skip text cannot be empty.", nameof(skipText));
            if (string.IsNullOrWhiteSpace(capacityFullText))
                throw new ArgumentException(
                    "Chest capacity text cannot be empty.",
                    nameof(capacityFullText));
            if (isCapacityFull && claimEnabled)
                throw new ArgumentException("A full potion belt cannot enable Chest claim.");

            VisitId = visitId;
            ClaimText = claimText;
            SkipText = skipText;
            CapacityFullText = capacityFullText;
            ClaimEnabled = claimEnabled;
            SkipEnabled = skipEnabled;
            IsCapacityFull = isCapacityFull;
        }
    }

    /// <summary>Shop 页上一项冻结库存的身份、当前语言文本与购买门禁。</summary>
    public sealed class RunShopStockEntryViewModel
    {
        /// <summary>购买命令必须回传的 Shop 内稳定条目标识。</summary>
        public int EntryId { get; }

        /// <summary>库存内容种类。</summary>
        public RunShopStockKind Kind { get; }

        /// <summary>冻结的当前配置模板身份。</summary>
        public int TemplateId { get; }

        /// <summary>进入 Shop 时冻结的购买价格。</summary>
        public int Price { get; }

        /// <summary>当前语言的物品名称；配置缺失时使用稳定身份占位。</summary>
        public string ItemName { get; }

        /// <summary>当前购买或已购状态的完整按钮文本。</summary>
        public string Text { get; }

        /// <summary>该库存是否已经在本次访问中购买。</summary>
        public bool Purchased { get; }

        /// <summary>当前余额、容量、持有物、配置与存档状态下是否允许购买。</summary>
        public bool PurchaseEnabled { get; }

        /// <summary>验证并冻结一项 Shop 库存投影。</summary>
        public RunShopStockEntryViewModel(
            int entryId,
            RunShopStockKind kind,
            int templateId,
            int price,
            string itemName,
            string text,
            bool purchased,
            bool purchaseEnabled)
        {
            if (entryId <= 0)
                throw new ArgumentOutOfRangeException(nameof(entryId));
            if (!Enum.IsDefined(typeof(RunShopStockKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (price <= 0)
                throw new ArgumentOutOfRangeException(nameof(price));
            if (string.IsNullOrWhiteSpace(itemName))
                throw new ArgumentException("Shop item name cannot be empty.", nameof(itemName));
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Shop stock text cannot be empty.", nameof(text));
            if (purchased && purchaseEnabled)
                throw new ArgumentException("Purchased Shop stock cannot remain enabled.");

            EntryId = entryId;
            Kind = kind;
            TemplateId = templateId;
            Price = price;
            ItemName = itemName;
            Text = text;
            Purchased = purchased;
            PurchaseEnabled = purchaseEnabled;
        }
    }

    /// <summary>Shop 访问身份、固定三项库存与离开动作的完整页面投影。</summary>
    public sealed class RunShopViewModel
    {
        private readonly ReadOnlyCollection<RunShopStockEntryViewModel> _entries;

        /// <summary>购买或离开动作必须回传的稳定访问身份。</summary>
        public RunNodeVisitId VisitId { get; }

        /// <summary>按冻结库存顺序排列的恰好三项投影。</summary>
        public IReadOnlyList<RunShopStockEntryViewModel> Entries => _entries;

        /// <summary>当前语言的离开按钮文本。</summary>
        public string LeaveText { get; }

        /// <summary>只有没有存档提交时才允许离开。</summary>
        public bool LeaveEnabled { get; }

        /// <summary>验证访问身份并防御性冻结三类 Shop 库存。</summary>
        public RunShopViewModel(
            RunNodeVisitId visitId,
            IReadOnlyList<RunShopStockEntryViewModel> entries,
            string leaveText,
            bool leaveEnabled)
        {
            if (visitId.RunId.Value == Guid.Empty || string.IsNullOrEmpty(visitId.NodeId.Value))
                throw new ArgumentException("Shop visit id cannot be empty.", nameof(visitId));
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            RunShopStockEntryViewModel[] frozenEntries = entries.ToArray();
            if (frozenEntries.Length != 3 ||
                frozenEntries.Any(entry => entry == null) ||
                frozenEntries.Select(entry => entry.EntryId).Distinct().Count() != 3 ||
                frozenEntries.Select(entry => entry.Kind).Distinct().Count() != 3)
            {
                throw new ArgumentException(
                    "Shop projection must contain three distinct entries and item kinds.",
                    nameof(entries));
            }
            if (string.IsNullOrWhiteSpace(leaveText))
                throw new ArgumentException("Shop leave text cannot be empty.", nameof(leaveText));

            VisitId = visitId;
            _entries = Array.AsReadOnly(frozenEntries);
            LeaveText = leaveText;
            LeaveEnabled = leaveEnabled;
        }
    }

    /// <summary>Event 访问身份、冻结数值、双选择文案与表现门禁的完整投影。</summary>
    public sealed class RunEventViewModel
    {
        /// <summary>选择动作必须回传的稳定访问身份。</summary>
        public RunNodeVisitId VisitId { get; }

        /// <summary>免费选择冻结的金币获得量。</summary>
        public int GainGoldAmount { get; }

        /// <summary>付费治疗冻结的金币成本。</summary>
        public int PaidHealCost { get; }

        /// <summary>付费治疗冻结的最大治疗量。</summary>
        public int PaidHealAmount { get; }

        /// <summary>当前语言的免费金币选择文本。</summary>
        public string GainGoldText { get; }

        /// <summary>当前语言的付费治疗选择文本。</summary>
        public string PaidHealText { get; }

        /// <summary>金币 checked 加法安全且没有提交中检查点时允许免费选择。</summary>
        public bool GainGoldEnabled { get; }

        /// <summary>余额足够、英雄受伤且没有提交中检查点时允许付费治疗。</summary>
        public bool PaidHealEnabled { get; }

        /// <summary>验证并冻结 Event 的两个有限选择投影。</summary>
        public RunEventViewModel(
            RunNodeVisitId visitId,
            int gainGoldAmount,
            int paidHealCost,
            int paidHealAmount,
            string gainGoldText,
            string paidHealText,
            bool gainGoldEnabled,
            bool paidHealEnabled)
        {
            if (visitId.RunId.Value == Guid.Empty || string.IsNullOrEmpty(visitId.NodeId.Value))
                throw new ArgumentException("Event visit id cannot be empty.", nameof(visitId));
            if (gainGoldAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(gainGoldAmount));
            if (paidHealCost <= 0)
                throw new ArgumentOutOfRangeException(nameof(paidHealCost));
            if (paidHealAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(paidHealAmount));
            if (string.IsNullOrWhiteSpace(gainGoldText))
                throw new ArgumentException("Event gain-gold text cannot be empty.", nameof(gainGoldText));
            if (string.IsNullOrWhiteSpace(paidHealText))
                throw new ArgumentException("Event paid-heal text cannot be empty.", nameof(paidHealText));

            VisitId = visitId;
            GainGoldAmount = gainGoldAmount;
            PaidHealCost = paidHealCost;
            PaidHealAmount = paidHealAmount;
            GainGoldText = gainGoldText;
            PaidHealText = paidHealText;
            GainGoldEnabled = gainGoldEnabled;
            PaidHealEnabled = paidHealEnabled;
        }
    }

    /// <summary>一个遗物或药水实例在 RunEntry 中的已本地化只读投影。</summary>
    public sealed class RunHoldingItemViewModel
    {
        /// <summary>对应 cfg.run 静态配置的稳定模板标识。</summary>
        public int TemplateId { get; }

        /// <summary>当前语言下的物品名称。</summary>
        public string Name { get; }

        /// <summary>以当前配置数值填充后的当前语言描述。</summary>
        public string Description { get; }

        /// <summary>验证并冻结一个只读持有物条目。</summary>
        public RunHoldingItemViewModel(int templateId, string name, string description)
        {
            if (templateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(templateId));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Run holding item name cannot be empty.", nameof(name));
            if (description == null)
                throw new ArgumentNullException(nameof(description));

            TemplateId = templateId;
            Name = name;
            Description = description;
        }
    }

    /// <summary>当前 Run 金币、遗物获得顺序与药水槽位顺序的完整只读投影。</summary>
    public sealed class RunHoldingsViewModel
    {
        private readonly ReadOnlyCollection<RunHoldingItemViewModel> _relics;
        private readonly ReadOnlyCollection<RunHoldingItemViewModel> _potions;

        /// <summary>包含当前金币数的已本地化文本。</summary>
        public string GoldText { get; }

        /// <summary>遗物区已本地化标题。</summary>
        public string RelicsTitle { get; }

        /// <summary>药水区已本地化标题。</summary>
        public string PotionsTitle { get; }

        /// <summary>空列表使用的已本地化占位文本。</summary>
        public string EmptyText { get; }

        /// <summary>按获得顺序冻结的遗物显示条目。</summary>
        public IReadOnlyList<RunHoldingItemViewModel> Relics => _relics;

        /// <summary>按槽位顺序冻结的药水显示条目。</summary>
        public IReadOnlyList<RunHoldingItemViewModel> Potions => _potions;

        /// <summary>验证文本并防御性冻结当前持有物投影。</summary>
        public RunHoldingsViewModel(
            string goldText,
            string relicsTitle,
            string potionsTitle,
            string emptyText,
            IReadOnlyList<RunHoldingItemViewModel> relics,
            IReadOnlyList<RunHoldingItemViewModel> potions)
        {
            if (string.IsNullOrWhiteSpace(goldText))
                throw new ArgumentException("Run holdings gold text cannot be empty.", nameof(goldText));
            if (string.IsNullOrWhiteSpace(relicsTitle))
                throw new ArgumentException("Run holdings relic title cannot be empty.", nameof(relicsTitle));
            if (string.IsNullOrWhiteSpace(potionsTitle))
                throw new ArgumentException("Run holdings potion title cannot be empty.", nameof(potionsTitle));
            if (emptyText == null)
                throw new ArgumentNullException(nameof(emptyText));
            if (relics == null)
                throw new ArgumentNullException(nameof(relics));
            if (potions == null)
                throw new ArgumentNullException(nameof(potions));

            RunHoldingItemViewModel[] frozenRelics = relics.ToArray();
            RunHoldingItemViewModel[] frozenPotions = potions.ToArray();
            if (frozenRelics.Any(item => item == null))
                throw new ArgumentException("Run holdings cannot contain null relic projections.", nameof(relics));
            if (frozenPotions.Any(item => item == null))
                throw new ArgumentException("Run holdings cannot contain null potion projections.", nameof(potions));

            GoldText = goldText;
            RelicsTitle = relicsTitle;
            PotionsTitle = potionsTitle;
            EmptyText = emptyText;
            _relics = Array.AsReadOnly(frozenRelics);
            _potions = Array.AsReadOnly(frozenPotions);
        }
    }

    /// <summary>由 Presenter 一次冻结、供 View 无业务判断渲染的完整页面投影。</summary>
    public sealed class RunEntryViewModel
    {
        private readonly IReadOnlyDictionary<RunEntryTextSlot, string> _texts;

        /// <summary>当前唯一可见页面。</summary>
        public RunEntryPage Page { get; }

        /// <summary>尚未创建 Run 时临时选择的单个 Hero。</summary>
        public int? SelectedHeroTemplateId { get; }

        /// <summary>角色确认按钮是否可用。</summary>
        public bool ConfirmEnabled { get; }

        /// <summary>当前 Run 的完整地图投影；尚未创建 Run 时为空。</summary>
        public RunMapViewModel Map { get; }

        /// <summary>主菜单继续游戏按钮是否可用。</summary>
        public bool ContinueEnabled { get; }

        /// <summary>普通检查点提交失败时是否允许显式回退；Terminal 永远不允许回退。</summary>
        public bool CanRollbackFailedSave { get; }

        /// <summary>当前普通战斗奖励投影；仅奖励页非空。</summary>
        public RunCardRewardViewModel CardReward { get; }

        /// <summary>当前 Run 的已本地化只读持有物投影；尚未创建 Run 时为空。</summary>
        public RunHoldingsViewModel Holdings { get; }

        /// <summary>当前 Rest Pending 的冻结选择投影；仅 Rest 页非空。</summary>
        public RunRestViewModel Rest { get; }

        /// <summary>当前 Chest Pending 的冻结奖励投影；仅 Chest 页非空。</summary>
        public RunChestViewModel Chest { get; }

        /// <summary>当前 Shop Pending 的冻结库存投影；仅 Shop 页非空。</summary>
        public RunShopViewModel Shop { get; }

        /// <summary>当前 Event Pending 的冻结双选择投影；仅 Event 页非空。</summary>
        public RunEventViewModel Event { get; }

        /// <summary>冻结当前页面、交互状态与全部本地化文本。</summary>
        public RunEntryViewModel(
            RunEntryPage page,
            IReadOnlyDictionary<RunEntryTextSlot, string> texts,
            int? selectedHeroTemplateId,
            bool confirmEnabled,
            RunMapViewModel map,
            bool continueEnabled = false,
            bool canRollbackFailedSave = false,
            RunCardRewardViewModel cardReward = null,
            RunHoldingsViewModel holdings = null,
            RunRestViewModel rest = null,
            RunChestViewModel chest = null,
            RunShopViewModel shop = null,
            RunEventViewModel eventNode = null)
        {
            if (texts == null)
                throw new ArgumentNullException(nameof(texts));

            Page = page;
            SelectedHeroTemplateId = selectedHeroTemplateId;
            ConfirmEnabled = confirmEnabled;
            Map = map;
            ContinueEnabled = continueEnabled;
            CanRollbackFailedSave = canRollbackFailedSave;
            CardReward = cardReward;
            Holdings = holdings;
            Rest = rest;
            Chest = chest;
            Shop = shop;
            Event = eventNode;
            _texts = new ReadOnlyDictionary<RunEntryTextSlot, string>(
                new Dictionary<RunEntryTextSlot, string>(texts));
        }

        /// <summary>读取指定 TMP 槽位的已本地化文本，并拒绝不完整投影。</summary>
        public string GetText(RunEntryTextSlot slot)
        {
            if (!_texts.TryGetValue(slot, out string value))
                throw new InvalidOperationException($"Run entry text slot '{slot}' is missing.");

            return value;
        }
    }

    /// <summary>RunEntry Presenter 与 Unity View 之间唯一、无业务状态的渲染 seam。</summary>
    public interface IRunEntryView
    {
        /// <summary>按钮点击被归一化后发布的唯一动作事件。</summary>
        event Action<RunEntryAction> ActionRequested;

        /// <summary>用完整不可变投影替换当前可见页面。</summary>
        void Render(RunEntryViewModel model);
    }

    /// <summary>把入口导航与 RunState 投影到 View；跨场景业务事实只读取 RunStateStore。</summary>
    public sealed class RunEntryPresenter : IInitializable, IDisposable
    {
        private const int WarriorHeroTemplateId = 1001;
        private const int MachineGunnerHeroTemplateId = 1002;

        private const string MainTitleKey = "run.entry.title";
        private const string StartGameKey = "run.entry.menu.start";
        private const string ContinueGameKey = "run.entry.menu.continue";
        private const string SettingsKey = "run.entry.menu.settings";
        private const string CompendiumKey = "run.entry.menu.compendium";
        private const string StatisticsKey = "run.entry.menu.statistics";
        private const string BackKey = "run.entry.common.back";
        private const string ComingSoonKey = "run.entry.common.coming_soon";
        private const string SettingsTitleKey = "run.entry.settings.title";
        private const string SettingsPlaceholderKey = "run.entry.settings.placeholder";
        private const string HeroTitleKey = "run.entry.hero.title";
        private const string HeroConfirmKey = "run.entry.hero.confirm";
        private const string FutureSlotKey = "run.entry.hero.future_slot";
        private const string MapTitleKey = "run.entry.map.title";
        private const string BattleNodeKey = "run.entry.map.battle_node";
        private const string ClearedKey = "run.entry.map.cleared";
        private const string HealthKey = "run.entry.map.health";
        private const string CardRewardTitleKey = "run.entry.reward.title";
        private const string SkipCardRewardKey = "run.entry.reward.skip";
        private const string RestTitleKey = "run.entry.rest.title";
        private const string RestHealKey = "run.entry.rest.heal";
        private const string RestUpgradeKey = "run.entry.rest.upgrade";
        private const string ChestTitleKey = "run.entry.chest.title";
        private const string ChestClaimKey = "run.entry.chest.claim";
        private const string ChestSkipKey = "run.entry.chest.skip";
        private const string ChestFullKey = "run.entry.chest.full";
        private const string ShopTitleKey = "run.entry.shop.title";
        private const string ShopPurchaseKey = "run.entry.shop.purchase";
        private const string ShopPurchasedKey = "run.entry.shop.purchased";
        private const string ShopLeaveKey = "run.entry.shop.leave";
        private const string EventTitleKey = "run.entry.event.title";
        private const string EventGainGoldKey = "run.entry.event.gain_gold";
        private const string EventPaidHealKey = "run.entry.event.paid_heal";
        private const string HoldingsGoldKey = "run.entry.holdings.gold";
        private const string HoldingsRelicsKey = "run.entry.holdings.relics";
        private const string HoldingsPotionsKey = "run.entry.holdings.potions";
        private const string HoldingsEmptyKey = "run.entry.holdings.empty";
        private const string StrengthKeywordKey = "battle.keyword.strength.name";
        private const string VulnerableKeywordKey = "battle.keyword.vulnerable.name";
        private const string FailureTitleKey = "run.entry.failure.title";
        private const string CancelKey = "run.entry.common.cancel";
        private const string AbandonTitleKey = "run.entry.abandon.title";
        private const string AbandonMessageKey = "run.entry.abandon.message";
        private const string AbandonConfirmKey = "run.entry.abandon.confirm";
        private const string DeleteTitleKey = "run.entry.save.delete.title";
        private const string DeleteMessageKey = "run.entry.save.delete.message";
        private const string DeleteConfirmKey = "run.entry.save.delete.confirm";
        private const string SaveIssueTitleKey = "run.entry.save.issue.title";
        private const string InvalidJsonKey = "run.entry.save.issue.invalid_json";
        private const string InvalidDocumentKey = "run.entry.save.issue.invalid_document";
        private const string UnsupportedSchemaKey = "run.entry.save.issue.unsupported_schema";
        private const string InterruptedCommitKey = "run.entry.save.issue.interrupted_commit";
        private const string IoFailureKey = "run.entry.save.issue.io_failure";
        private const string MissingConfigurationKey =
            "run.entry.save.issue.missing_configuration";
        private const string DeleteFailedKey = "run.entry.save.delete.failed";
        private const string CommitFailedKey = "run.entry.save.commit_failed";
        private const string RetrySaveKey = "run.entry.save.retry";
        private const string ExitKey = "run.entry.save.exit";
        private const string RollbackTitleKey = "run.entry.save.rollback.title";
        private const string RollbackMessageKey = "run.entry.save.rollback.message";
        private const string RollbackConfirmKey = "run.entry.save.rollback.confirm";

        private readonly IRunEntryView _view;
        private readonly RunStateStore _store;
        private readonly RunFlowService _flow;
        private readonly Func<Tables> _tablesProvider;
        private readonly IRunMapIdentityCatalog _mapIdentities;
        private readonly Func<string, IReadOnlyDictionary<string, object>, string> _localize;
        private readonly Observable<Locale> _localeChanges;

        private IDisposable _stateSubscription;
        private IDisposable _localeSubscription;
        private RunEntryPage _localPage = RunEntryPage.MainMenu;
        private int? _selectedHeroTemplateId;
        private bool _initialized;
        private bool _disposed;

        /// <summary>以生产配置、本地化服务和跨场景 Run 服务创建入口 Presenter。</summary>
        [Inject]
        public RunEntryPresenter(
            IRunEntryView view,
            RunStateStore store,
            RunFlowService flow,
            ConfigService configs,
            LocalizationService localization,
            IRunMapIdentityCatalog mapIdentities)
            : this(
                view,
                store,
                flow,
                CreateTablesProvider(configs),
                mapIdentities,
                CreateLocalizer(localization),
                RequireLocaleChanges(localization))
        {
        }

        /// <summary>以可替换配置与本地化 seam 创建可直接 EditMode 验证的 Presenter。</summary>
        internal RunEntryPresenter(
            IRunEntryView view,
            RunStateStore store,
            RunFlowService flow,
            Func<Tables> tablesProvider,
            Func<string, IReadOnlyDictionary<string, object>, string> localize,
            Observable<Locale> localeChanges)
            : this(
                view,
                store,
                flow,
                tablesProvider,
                new RunMapIdentityCatalog(tablesProvider, localize),
                localize,
                localeChanges)
        {
        }

        /// <summary>以显式地图身份目录创建可直接验证身份投影的 Presenter。</summary>
        internal RunEntryPresenter(
            IRunEntryView view,
            RunStateStore store,
            RunFlowService flow,
            Func<Tables> tablesProvider,
            IRunMapIdentityCatalog mapIdentities,
            Func<string, IReadOnlyDictionary<string, object>, string> localize,
            Observable<Locale> localeChanges)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _tablesProvider = tablesProvider ?? throw new ArgumentNullException(nameof(tablesProvider));
            _mapIdentities = mapIdentities ?? throw new ArgumentNullException(nameof(mapIdentities));
            _localize = localize ?? throw new ArgumentNullException(nameof(localize));
            _localeChanges = localeChanges ?? throw new ArgumentNullException(nameof(localeChanges));
        }

        /// <summary>一次性订阅 View、RunState 与语言变化，并立即渲染当前页面。</summary>
        public void Initialize()
        {
            ThrowIfDisposed();
            if (_initialized)
                return;

            _initialized = true;
            _view.ActionRequested += HandleAction;
            _flow.PersistenceChanged += HandlePersistenceChanged;
            _stateSubscription = _store.State.Subscribe(_ => Render());
            _localeSubscription = _localeChanges.Subscribe(_ => Render());
            _flow.RefreshSaveAvailability();
            Render();
        }

        /// <summary>解除全部场景级订阅，使旧 RunEntryScene 不留下回调。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_initialized)
            {
                _view.ActionRequested -= HandleAction;
                _flow.PersistenceChanged -= HandlePersistenceChanged;
            }
            _stateSubscription?.Dispose();
            _localeSubscription?.Dispose();
        }

        /// <summary>只把 View 意图路由到本地导航或 RunFlow，不让 View 直接写 Run。</summary>
        private void HandleAction(RunEntryAction action)
        {
            if (_disposed)
                return;

            if (_store.Current != null)
            {
                HandleRunAction(action);
                return;
            }

            HandlePreRunAction(action);
        }

        /// <summary>处理创建 Run 之前的菜单、返回与单 Hero 选择。</summary>
        private void HandlePreRunAction(RunEntryAction action)
        {
            switch (action.Kind)
            {
                case RunEntryActionKind.StartGame when _localPage == RunEntryPage.MainMenu:
                    _localPage = _flow.Persistence.HasStoredData
                        ? RunEntryPage.AbandonConfirmation
                        : RunEntryPage.HeroSelection;
                    _selectedHeroTemplateId = null;
                    Render();
                    break;
                case RunEntryActionKind.ContinueGame
                    when _localPage == RunEntryPage.MainMenu &&
                         _flow.Persistence.CanContinue:
                    _flow.ContinueSavedRun();
                    break;
                case RunEntryActionKind.OpenSettings when _localPage == RunEntryPage.MainMenu:
                    _localPage = RunEntryPage.Settings;
                    Render();
                    break;
                case RunEntryActionKind.OpenCompendium when _localPage == RunEntryPage.MainMenu:
                    _localPage = RunEntryPage.Compendium;
                    Render();
                    break;
                case RunEntryActionKind.OpenStatistics when _localPage == RunEntryPage.MainMenu:
                    _localPage = RunEntryPage.Statistics;
                    Render();
                    break;
                case RunEntryActionKind.Back when _localPage != RunEntryPage.MainMenu:
                    _localPage = RunEntryPage.MainMenu;
                    _selectedHeroTemplateId = null;
                    Render();
                    break;
                case RunEntryActionKind.SelectHero when _localPage == RunEntryPage.HeroSelection:
                    SelectHero(action.HeroTemplateId.Value);
                    break;
                case RunEntryActionKind.ConfirmHero
                    when _localPage == RunEntryPage.HeroSelection && _selectedHeroTemplateId.HasValue:
                    _flow.CreateNewRun(_selectedHeroTemplateId.Value);
                    break;
                case RunEntryActionKind.ConfirmAbandon
                    when _localPage == RunEntryPage.AbandonConfirmation:
                    RunSaveDeleteResult delete = _flow.AbandonSavedRun();
                    _localPage = delete.Status == RunSaveDeleteStatus.Success
                        ? RunEntryPage.HeroSelection
                        : RunEntryPage.MainMenu;
                    _selectedHeroTemplateId = null;
                    Render();
                    break;
            }
        }

        /// <summary>只响应由当前 Run 阶段允许的地图选择、终局离开或存档恢复动作。</summary>
        private void HandleRunAction(RunEntryAction action)
        {
            RunState state = _store.Current;
            if (action.Kind == RunEntryActionKind.EnterMapNode &&
                action.MapNodeId.HasValue &&
                state.ProgressPhase == RunProgressPhase.MapReady)
            {
                _flow.EnterMapNodeAsync(action.MapNodeId.Value).Forget();
            }
            else if (action.Kind == RunEntryActionKind.LeaveTerminalRun &&
                     state.ProgressPhase == RunProgressPhase.Terminal)
            {
                RunSaveDeleteResult delete = _flow.AbandonSavedRun();
                if (delete.Status == RunSaveDeleteStatus.Success)
                {
                    _localPage = RunEntryPage.MainMenu;
                    _selectedHeroTemplateId = null;
                }

                Render();
            }
            else if (action.Kind == RunEntryActionKind.RetrySave &&
                     _flow.Persistence.Status == RunPersistenceStatus.CommitFailed)
            {
                _flow.RetryPendingCommit();
            }
            else if (action.Kind == RunEntryActionKind.SelectCardReward &&
                     action.CardRewardId.HasValue &&
                     action.CardTemplateId.HasValue &&
                     state.ProgressPhase == RunProgressPhase.RewardPending &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitFailed)
            {
                _flow.SettleCardReward(
                    action.CardRewardId.Value,
                    action.CardTemplateId.Value);
            }
            else if (action.Kind == RunEntryActionKind.SkipCardReward &&
                     action.CardRewardId.HasValue &&
                     state.ProgressPhase == RunProgressPhase.RewardPending &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitFailed)
            {
                _flow.SettleCardReward(action.CardRewardId.Value, selectedCardTemplateId: null);
            }
            else if (action.Kind == RunEntryActionKind.HealAtRest &&
                     action.NodeVisitId.HasValue &&
                     state.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                     state.PendingNodeVisit?.Kind == MapNodeKind.Rest &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitFailed)
            {
                _flow.SettleRestHeal(action.NodeVisitId.Value);
            }
            else if (action.Kind == RunEntryActionKind.UpgradeCardAtRest &&
                     action.NodeVisitId.HasValue &&
                     action.CardInstanceId.HasValue &&
                     state.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                     state.PendingNodeVisit?.Kind == MapNodeKind.Rest &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitFailed)
            {
                _flow.SettleRestUpgrade(
                    action.NodeVisitId.Value,
                    action.CardInstanceId.Value);
            }
            else if (action.Kind == RunEntryActionKind.ClaimChest &&
                     action.NodeVisitId.HasValue &&
                     state.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                     state.PendingNodeVisit?.Kind == MapNodeKind.Chest &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitFailed)
            {
                _flow.SettleChestClaim(action.NodeVisitId.Value);
            }
            else if (action.Kind == RunEntryActionKind.SkipChest &&
                     action.NodeVisitId.HasValue &&
                     state.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                     state.PendingNodeVisit?.Kind == MapNodeKind.Chest &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitFailed)
            {
                _flow.SettleChestSkip(action.NodeVisitId.Value);
            }
            else if (action.Kind == RunEntryActionKind.PurchaseShopStock &&
                     action.NodeVisitId.HasValue &&
                     action.ShopStockEntryId.HasValue &&
                     state.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                     state.PendingNodeVisit?.Kind == MapNodeKind.Shop &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitFailed)
            {
                _flow.SettleShopPurchase(
                    action.NodeVisitId.Value,
                    action.ShopStockEntryId.Value);
            }
            else if (action.Kind == RunEntryActionKind.LeaveShop &&
                     action.NodeVisitId.HasValue &&
                     state.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                     state.PendingNodeVisit?.Kind == MapNodeKind.Shop &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitFailed)
            {
                _flow.SettleShopLeave(action.NodeVisitId.Value);
            }
            else if (action.Kind == RunEntryActionKind.ChooseEvent &&
                     action.NodeVisitId.HasValue &&
                     action.EventChoice.HasValue &&
                     state.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                     state.PendingNodeVisit?.Kind == MapNodeKind.Event &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                     _flow.Persistence.Status != RunPersistenceStatus.CommitFailed)
            {
                _flow.SettleEventChoice(
                    action.NodeVisitId.Value,
                    action.EventChoice.Value);
            }
            else if (action.Kind == RunEntryActionKind.RequestExitAfterSaveFailure &&
                     _flow.CanRollbackFailedCheckpoint)
            {
                _localPage = RunEntryPage.RollbackConfirmation;
                Render();
            }
            else if (action.Kind == RunEntryActionKind.Back &&
                     _localPage == RunEntryPage.RollbackConfirmation)
            {
                _localPage = RunEntryPage.SaveFailure;
                Render();
            }
            else if (action.Kind == RunEntryActionKind.ConfirmRollback &&
                     _flow.CanRollbackFailedCheckpoint)
            {
                _localPage = RunEntryPage.MainMenu;
                _selectedHeroTemplateId = null;
                _flow.ExitPendingRunToMenu();
                Render();
            }
        }

        /// <summary>验证冻结候选并更新创建 Run 前唯一允许的临时选择。</summary>
        private void SelectHero(int heroTemplateId)
        {
            if (heroTemplateId != WarriorHeroTemplateId &&
                heroTemplateId != MachineGunnerHeroTemplateId)
            {
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            }

            _selectedHeroTemplateId = heroTemplateId;
            Render();
        }

        /// <summary>从当前 RunState 或本地预 Run 导航重建完整不可变页面投影。</summary>
        private void Render()
        {
            RunState state = _store.Current;
            RunEntryPage page = ResolvePage(state);
            int? selectedHero = state == null ? _selectedHeroTemplateId : state.HeroTemplateId;
            var texts = BuildTexts(state);
            RunMapViewModel map = state == null ? null : BuildMapViewModel(state);
            RunCardRewardViewModel cardReward = BuildCardRewardViewModel(state);
            RunHoldingsViewModel holdings = BuildHoldingsViewModel(state);
            RunRestViewModel rest = BuildRestViewModel(state);
            RunChestViewModel chest = BuildChestViewModel(state);
            RunShopViewModel shop = BuildShopViewModel(state);
            RunEventViewModel eventNode = BuildEventViewModel(state);

            _view.Render(new RunEntryViewModel(
                page,
                texts,
                selectedHero,
                confirmEnabled: state == null &&
                                page == RunEntryPage.HeroSelection &&
                                _selectedHeroTemplateId.HasValue,
                map,
                continueEnabled: state == null &&
                                 page == RunEntryPage.MainMenu &&
                                 _flow.Persistence.CanContinue,
                canRollbackFailedSave: state != null &&
                                        _flow.CanRollbackFailedCheckpoint,
                cardReward: cardReward,
                holdings: holdings,
                rest: rest,
                chest: chest,
                shop: shop,
                eventNode: eventNode));
        }

        /// <summary>按 Run 持有物领域顺序从 cfg.run 构建当前语言的金币、遗物与药水投影。</summary>
        private RunHoldingsViewModel BuildHoldingsViewModel(RunState state)
        {
            if (state == null)
                return null;

            Tables tables = _tablesProvider()
                ?? throw new InvalidOperationException(
                    "ConfigService must be initialized before rendering Run holdings.");
            RunHoldingItemViewModel[] relics = state.Holdings.Relics
                .Select(relic => BuildRelicHoldingItem(tables, relic))
                .ToArray();
            RunHoldingItemViewModel[] potions = state.Holdings.Potions
                .Select(potion => BuildPotionHoldingItem(tables, potion))
                .ToArray();
            return new RunHoldingsViewModel(
                _localize(
                    HoldingsGoldKey,
                    new Dictionary<string, object> { ["gold"] = state.Holdings.Gold }),
                Localize(HoldingsRelicsKey),
                Localize(HoldingsPotionsKey),
                Localize(HoldingsEmptyKey),
                relics,
                potions);
        }

        /// <summary>以 cfg.run 遗物模板和 strength Smart 参数投影一个已持有遗物。</summary>
        private RunHoldingItemViewModel BuildRelicHoldingItem(Tables tables, RunRelic relic)
        {
            cfg.run.Relic template = tables.TbRelic.GetOrDefault(relic.TemplateId)
                ?? throw new InvalidOperationException(
                    $"Relic template {relic.TemplateId} does not exist.");
            return new RunHoldingItemViewModel(
                template.Id,
                Localize(template.NameI18nKey),
                _localize(
                    template.DescriptionI18nKey,
                    new Dictionary<string, object>
                    {
                        ["strength"] = template.BattleStartStrength,
                    }));
        }

        /// <summary>以 cfg.run 药水模板和 heal Smart 参数投影一个药水槽位。</summary>
        private RunHoldingItemViewModel BuildPotionHoldingItem(Tables tables, RunPotion potion)
        {
            if (potion == null)
                throw new ArgumentNullException(nameof(potion));
            return BuildPotionTemplateItem(tables, potion.TemplateId);
        }

        /// <summary>以冻结药水模板身份和 heal Smart 参数投影一个尚未领取或已持有的药水。</summary>
        private RunHoldingItemViewModel BuildPotionTemplateItem(Tables tables, int templateId)
        {
            cfg.run.Potion template = tables.TbPotion.GetOrDefault(templateId)
                ?? throw new InvalidOperationException(
                    $"Potion template {templateId} does not exist.");
            return new RunHoldingItemViewModel(
                template.Id,
                Localize(template.NameI18nKey),
                _localize(
                    template.DescriptionI18nKey,
                    new Dictionary<string, object>
                    {
                        ["heal"] = template.HealAmount,
                    }));
        }

        /// <summary>让 RunState 决定地图或失败页；尚未创建 Run 时才使用场景内导航。</summary>
        private RunEntryPage ResolvePage(RunState state)
        {
            if (state == null)
                return _localPage;

            if (_flow.Persistence.Status == RunPersistenceStatus.CommitFailed)
            {
                return _localPage == RunEntryPage.RollbackConfirmation
                    ? RunEntryPage.RollbackConfirmation
                    : RunEntryPage.SaveFailure;
            }

            if (state.ProgressPhase == RunProgressPhase.Terminal)
                return RunEntryPage.Failure;
            if (state.ProgressPhase == RunProgressPhase.RewardPending)
                return RunEntryPage.CardReward;
            if (state.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                state.PendingNodeVisit?.Kind == MapNodeKind.Rest)
            {
                return RunEntryPage.Rest;
            }
            if (state.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                state.PendingNodeVisit?.Kind == MapNodeKind.Chest)
            {
                return RunEntryPage.Chest;
            }
            if (state.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                state.PendingNodeVisit?.Kind == MapNodeKind.Shop)
            {
                return RunEntryPage.Shop;
            }
            if (state.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                state.PendingNodeVisit?.Kind == MapNodeKind.Event)
            {
                return RunEntryPage.Event;
            }
            return RunEntryPage.Map;
        }

        /// <summary>按 Pending 冻结顺序投影 Rest 治疗量与当前配置终审后的实例升级动作。</summary>
        private RunRestViewModel BuildRestViewModel(RunState state)
        {
            if (state?.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                state.PendingNodeVisit?.Kind != MapNodeKind.Rest)
            {
                return null;
            }

            Tables tables = _tablesProvider()
                ?? throw new InvalidOperationException(
                    "ConfigService must be initialized before rendering Rest choices.");
            var catalog = new TablesRunSaveConfigurationCatalog(tables);
            PendingRunNodeVisit pending = state.PendingNodeVisit;
            bool actionsEnabled = _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                                  _flow.Persistence.Status != RunPersistenceStatus.CommitFailed;
            RunRestUpgradeCandidateViewModel[] candidates = pending.RestPayload
                .UpgradeCandidateInstanceIds
                .Select(instanceId =>
                {
                    RunCard card = state.RunDeck.Cards.Single(value => value.InstanceId == instanceId);
                    cfg.battle.Card template = tables.TbCard.GetOrDefault(card.TemplateId)
                        ?? throw new InvalidOperationException(
                            $"Card template {card.TemplateId} does not exist.");
                    int nextLevel = checked(card.UpgradeLevel + 1);
                    string text = _localize(
                        RestUpgradeKey,
                        new Dictionary<string, object>
                        {
                            ["card"] = Localize(template.NameI18nKey),
                            ["level"] = nextLevel,
                        });
                    return new RunRestUpgradeCandidateViewModel(
                        card.InstanceId,
                        card.TemplateId,
                        card.UpgradeLevel,
                        text,
                        actionsEnabled &&
                        catalog.IsCardUpgradeLevelValid(card.TemplateId, nextLevel));
                })
                .ToArray();
            string healText = _localize(
                RestHealKey,
                new Dictionary<string, object>
                {
                    ["amount"] = pending.RestPayload.HealAmount,
                });
            return new RunRestViewModel(
                pending.Id,
                pending.RestPayload.HealAmount,
                healText,
                actionsEnabled && state.CurrentHealth < state.MaxHealth,
                candidates);
        }

        /// <summary>从 Chest Pending 冻结模板构建本地化奖励，并仅以容量和存档状态控制双动作表现。</summary>
        private RunChestViewModel BuildChestViewModel(RunState state)
        {
            if (state?.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                state.PendingNodeVisit?.Kind != MapNodeKind.Chest)
            {
                return null;
            }

            Tables tables = _tablesProvider()
                ?? throw new InvalidOperationException(
                    "ConfigService must be initialized before rendering Chest choices.");
            PendingRunNodeVisit pending = state.PendingNodeVisit;
            bool actionsEnabled = _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                                  _flow.Persistence.Status != RunPersistenceStatus.CommitFailed;
            bool capacityFull = state.Holdings.Potions.Count >= 3;
            return new RunChestViewModel(
                pending.Id,
                BuildPotionTemplateItem(tables, pending.ChestPayload.PotionTemplateId),
                Localize(ChestClaimKey),
                Localize(ChestSkipKey),
                Localize(ChestFullKey),
                claimEnabled: actionsEnabled && !capacityFull,
                skipEnabled: actionsEnabled,
                isCapacityFull: capacityFull);
        }

        /// <summary>从 Shop Pending 构建固定三项库存，并以当前余额、容量、持有物和 Hero 配置投影门禁。</summary>
        private RunShopViewModel BuildShopViewModel(RunState state)
        {
            if (state?.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                state.PendingNodeVisit?.Kind != MapNodeKind.Shop)
            {
                return null;
            }

            Tables tables = _tablesProvider()
                ?? throw new InvalidOperationException(
                    "ConfigService must be initialized before rendering Shop stock.");
            var catalog = new TablesRunSaveConfigurationCatalog(tables);
            HeroCardRewardPool cardPool = TryCreateShopHeroCardPool(catalog, state.HeroTemplateId);
            PendingRunNodeVisit pending = state.PendingNodeVisit;
            bool actionsEnabled = _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                                  _flow.Persistence.Status != RunPersistenceStatus.CommitFailed;
            RunShopStockEntryViewModel[] entries = pending.ShopPayload.Entries
                .Select(entry => BuildShopStockEntryViewModel(
                    tables,
                    state,
                    entry,
                    cardPool,
                    actionsEnabled))
                .ToArray();
            return new RunShopViewModel(
                pending.Id,
                entries,
                Localize(ShopLeaveKey),
                leaveEnabled: actionsEnabled);
        }

        /// <summary>按库存种类解析名称与当前配置合法性，再叠加余额、容量、重复持有和 Purchased 门禁。</summary>
        private RunShopStockEntryViewModel BuildShopStockEntryViewModel(
            Tables tables,
            RunState state,
            RunShopStockEntry entry,
            HeroCardRewardPool cardPool,
            bool actionsEnabled)
        {
            string itemName;
            bool configurationAvailable;
            bool domainBlocked;
            switch (entry.Kind)
            {
                case RunShopStockKind.Relic:
                    cfg.run.Relic relic = tables.TbRelic.GetOrDefault(entry.TemplateId);
                    itemName = relic == null ? $"#{entry.TemplateId}" : Localize(relic.NameI18nKey);
                    configurationAvailable = relic != null;
                    domainBlocked = state.Holdings.Relics.Any(
                        held => held.TemplateId == entry.TemplateId);
                    break;
                case RunShopStockKind.Potion:
                    cfg.run.Potion potion = tables.TbPotion.GetOrDefault(entry.TemplateId);
                    itemName = potion == null ? $"#{entry.TemplateId}" : Localize(potion.NameI18nKey);
                    configurationAvailable = potion != null;
                    domainBlocked = state.Holdings.Potions.Count >= 3;
                    break;
                case RunShopStockKind.Card:
                    cfg.battle.Card card = tables.TbCard.GetOrDefault(entry.TemplateId);
                    itemName = card == null ? $"#{entry.TemplateId}" : Localize(card.NameI18nKey);
                    configurationAvailable = card != null &&
                                             cardPool != null &&
                                             cardPool.HeroTemplateId == state.HeroTemplateId &&
                                             cardPool.Candidates.Any(
                                                 candidate => candidate.TemplateId == entry.TemplateId);
                    domainBlocked = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry.Kind));
            }

            string text = entry.Purchased
                ? _localize(
                    ShopPurchasedKey,
                    new Dictionary<string, object> { ["item"] = itemName })
                : _localize(
                    ShopPurchaseKey,
                    new Dictionary<string, object>
                    {
                        ["item"] = itemName,
                        ["price"] = entry.Price,
                    });
            bool purchaseEnabled = actionsEnabled &&
                                   !entry.Purchased &&
                                   configurationAvailable &&
                                   !domainBlocked &&
                                   state.Holdings.Gold >= entry.Price;
            return new RunShopStockEntryViewModel(
                entry.EntryId,
                entry.Kind,
                entry.TemplateId,
                entry.Price,
                itemName,
                text,
                entry.Purchased,
                purchaseEnabled);
        }

        /// <summary>Hero 奖励池配置不完整时只返回空门禁结果，让 Shop 页面仍可显示冻结身份并禁用卡项。</summary>
        private static HeroCardRewardPool TryCreateShopHeroCardPool(
            TablesRunSaveConfigurationCatalog catalog,
            int heroTemplateId)
        {
            try
            {
                return catalog.CreateHeroCardRewardPool(heroTemplateId);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>从 Event Pending 冻结数值构建双选择文本，并按金币边界、余额、生命与存档状态门禁。</summary>
        private RunEventViewModel BuildEventViewModel(RunState state)
        {
            if (state?.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                state.PendingNodeVisit?.Kind != MapNodeKind.Event)
            {
                return null;
            }

            PendingRunNodeVisit pending = state.PendingNodeVisit;
            RunEventNodeVisitPayload payload = pending.EventPayload;
            bool actionsEnabled = _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                                  _flow.Persistence.Status != RunPersistenceStatus.CommitFailed;
            bool gainGoldSafe = (long)state.Holdings.Gold + payload.GainGoldAmount <= int.MaxValue;
            return new RunEventViewModel(
                pending.Id,
                payload.GainGoldAmount,
                payload.PaidHealCost,
                payload.PaidHealAmount,
                _localize(
                    EventGainGoldKey,
                    new Dictionary<string, object>
                    {
                        ["gold"] = payload.GainGoldAmount,
                    }),
                _localize(
                    EventPaidHealKey,
                    new Dictionary<string, object>
                    {
                        ["cost"] = payload.PaidHealCost,
                        ["heal"] = payload.PaidHealAmount,
                    }),
                gainGoldEnabled: actionsEnabled && gainGoldSafe,
                paidHealEnabled: actionsEnabled &&
                                 state.Holdings.Gold >= payload.PaidHealCost &&
                                 state.CurrentHealth < state.MaxHealth);
        }

        /// <summary>从 Pending 身份与卡牌配置构建当前语言的固定三候选投影。</summary>
        private RunCardRewardViewModel BuildCardRewardViewModel(RunState state)
        {
            if (state?.ProgressPhase != RunProgressPhase.RewardPending)
                return null;
            PendingCardReward pending = state.PendingCardReward
                ?? throw new InvalidOperationException("RewardPending Run must contain its frozen reward.");
            Tables tables = _tablesProvider()
                ?? throw new InvalidOperationException("ConfigService must be initialized before rendering card rewards.");
            RunCardRewardCandidateViewModel[] candidates = pending.CandidateTemplateIds
                .Select(templateId => BuildCardRewardCandidate(tables, templateId))
                .ToArray();
            bool actionsEnabled = _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                                  _flow.Persistence.Status != RunPersistenceStatus.CommitFailed;
            return new RunCardRewardViewModel(pending.Id, candidates, actionsEnabled);
        }

        /// <summary>把一个零升级奖励模板投影为名称、规则文本与费用文本。</summary>
        private RunCardRewardCandidateViewModel BuildCardRewardCandidate(
            Tables tables,
            int templateId)
        {
            cfg.battle.Card card = tables.TbCard.GetOrDefault(templateId)
                ?? throw new InvalidOperationException($"Card template {templateId} does not exist.");
            string costText = card.CostKind == cfg.battle.CardCostKind.X
                ? "X"
                : card.Cost.ToString(CultureInfo.InvariantCulture);
            return new RunCardRewardCandidateViewModel(
                card.Id,
                Localize(card.NameI18nKey),
                _localize(card.DescriptionI18nKey, BuildCardRewardDescriptionArguments(tables, card)),
                costText);
        }

        /// <summary>用静态效果基值填充奖励页规则参数，不读取或保存任何 Battle 临时事实。</summary>
        private IReadOnlyDictionary<string, object> BuildCardRewardDescriptionArguments(
            Tables tables,
            cfg.battle.Card card)
        {
            var arguments = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (cfg.battle.CardEffectBinding binding in card.EffectBindings)
            {
                if (string.IsNullOrWhiteSpace(binding.ArgumentKey) ||
                    arguments.ContainsKey(binding.ArgumentKey))
                {
                    throw new InvalidOperationException(
                        $"Card {card.Id} contains an invalid reward text argument key.");
                }

                cfg.battle.CardEffect effect = tables.TbCardEffect.GetOrDefault(binding.EffectId)
                    ?? throw new InvalidOperationException(
                        $"Card {card.Id} references missing effect {binding.EffectId}.");
                arguments.Add(binding.ArgumentKey, effect.Value);
            }

            arguments["keywordStrength"] = Localize(StrengthKeywordKey);
            arguments["keywordVulnerable"] = Localize(VulnerableKeywordKey);
            return arguments;
        }

        /// <summary>把冻结地图和当前唯一进度投影为 View 可直接绘制的完整明牌图。</summary>
        private RunMapViewModel BuildMapViewModel(RunState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            MapDefinition map = state.MapDefinition;
            bool selectionEnabled = state.ProgressPhase == RunProgressPhase.MapReady &&
                                    _flow.Persistence.Status != RunPersistenceStatus.CommitPending &&
                                    _flow.Persistence.Status != RunPersistenceStatus.CommitFailed;
            var selectable = selectionEnabled
                ? new HashSet<MapNodeId>(MapReachability.GetSelectableNodeIds(
                    map,
                    state.CurrentNodeId,
                    MapTraversalMode.Ordinary))
                : new HashSet<MapNodeId>();
            var completed = new HashSet<MapNodeId>(state.PathNodeIds);
            var completedEdgeKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 1; index < state.PathNodeIds.Count; index++)
            {
                completedEdgeKeys.Add(BuildEdgeKey(
                    state.PathNodeIds[index - 1],
                    state.PathNodeIds[index]));
            }

            RunMapNodeViewModel[] nodes = map.Nodes
                .OrderBy(node => node.Layer)
                .ThenBy(node => node.Slot)
                .Select(node => BuildMapNodeViewModel(
                    map,
                    node,
                    state,
                    selectable.Contains(node.Id),
                    completed.Contains(node.Id)))
                .ToArray();
            RunMapEdgeViewModel[] edges = map.Edges
                .Select(edge => new RunMapEdgeViewModel(
                    edge.FromNodeId.Value,
                    edge.ToNodeId.Value,
                    completedEdgeKeys.Contains(BuildEdgeKey(edge.FromNodeId, edge.ToNodeId))))
                .ToArray();

            return new RunMapViewModel(map.Fingerprint, nodes, edges);
        }

        /// <summary>投影一个节点的功能状态，并只为当前可选节点计算完整后半程。</summary>
        private RunMapNodeViewModel BuildMapNodeViewModel(
            MapDefinition map,
            MapNode node,
            RunState state,
            bool isSelectable,
            bool isCompleted)
        {
            MapDownstreamRoute route = isSelectable
                ? MapReachability.GetDownstreamRoute(map, node.Id)
                : null;
            string[] downstreamNodeIds = route == null
                ? Array.Empty<string>()
                : route.NodeIds.Select(nodeId => nodeId.Value).ToArray();
            string[] downstreamEdgeKeys = route == null
                ? Array.Empty<string>()
                : route.Edges.Select(edge => BuildEdgeKey(
                    edge.FromNodeId,
                    edge.ToNodeId)).ToArray();
            RunMapIdentityDescriptor identity = _mapIdentities.Resolve(
                node.Kind,
                node.ContentId);

            return new RunMapNodeViewModel(
                node.Id.Value,
                node.Layer,
                node.Slot,
                node.Kind,
                node.ContentId,
                identity.DisplayName,
                identity.VisualAnchorKind,
                ResolveMapNodePresentationState(node, state, isSelectable, isCompleted),
                downstreamNodeIds,
                downstreamEdgeKeys);
        }

        /// <summary>把节点当前事实归一化为互斥的 View 表现状态。</summary>
        private static RunMapNodePresentationState ResolveMapNodePresentationState(
            MapNode node,
            RunState state,
            bool isSelectable,
            bool isCompleted)
        {
            if (state.ProgressPhase == RunProgressPhase.BossGateReached &&
                node.Id == state.CurrentNodeId)
            {
                return RunMapNodePresentationState.BossGateReached;
            }
            if (node.Id == state.CurrentNodeId)
                return RunMapNodePresentationState.Current;
            if (isCompleted)
                return RunMapNodePresentationState.Completed;
            if (isSelectable)
                return RunMapNodePresentationState.Selectable;
            return RunMapNodePresentationState.Locked;
        }

        /// <summary>为地图边生成与 View 一致的稳定无歧义键。</summary>
        private static string BuildEdgeKey(MapNodeId fromNodeId, MapNodeId toNodeId)
        {
            return $"{fromNodeId.Value}>{toNodeId.Value}";
        }

        /// <summary>从 Luban Hero 键、当前语言与当前 Run 事实构建全部 TMP 文本。</summary>
        private IReadOnlyDictionary<RunEntryTextSlot, string> BuildTexts(RunState state)
        {
            Tables tables = _tablesProvider()
                ?? throw new InvalidOperationException("ConfigService must be initialized before rendering RunEntry.");
            cfg.battle.Hero warrior = tables.TbHero.GetOrDefault(WarriorHeroTemplateId)
                ?? throw new InvalidOperationException("Hero template 1001 does not exist.");
            cfg.battle.Hero machineGunner = tables.TbHero.GetOrDefault(MachineGunnerHeroTemplateId)
                ?? throw new InvalidOperationException("Hero template 1002 does not exist.");
            var healthArguments = new Dictionary<string, object>
            {
                ["current"] = state?.CurrentHealth ?? 0,
                ["max"] = state?.MaxHealth ?? 0,
            };
            bool deletingUnusableSave = _flow.Persistence.HasStoredData &&
                                        !_flow.Persistence.CanContinue;
            string saveIssue = BuildSaveIssueText();

            return new Dictionary<RunEntryTextSlot, string>
            {
                [RunEntryTextSlot.MainTitle] = Localize(MainTitleKey),
                [RunEntryTextSlot.StartGame] = Localize(StartGameKey),
                [RunEntryTextSlot.ContinueGame] = Localize(ContinueGameKey),
                [RunEntryTextSlot.Settings] = Localize(SettingsKey),
                [RunEntryTextSlot.Compendium] = Localize(CompendiumKey),
                [RunEntryTextSlot.Statistics] = Localize(StatisticsKey),
                [RunEntryTextSlot.Back] = Localize(BackKey),
                [RunEntryTextSlot.ComingSoon] = Localize(ComingSoonKey),
                [RunEntryTextSlot.SettingsTitle] = Localize(SettingsTitleKey),
                [RunEntryTextSlot.SettingsPlaceholder] = Localize(SettingsPlaceholderKey),
                [RunEntryTextSlot.HeroTitle] = Localize(HeroTitleKey),
                [RunEntryTextSlot.Hero1001Name] = Localize(warrior.NameI18nKey),
                [RunEntryTextSlot.Hero1002Name] = Localize(machineGunner.NameI18nKey),
                [RunEntryTextSlot.ConfirmHero] = Localize(HeroConfirmKey),
                [RunEntryTextSlot.FutureSlot] = Localize(FutureSlotKey),
                [RunEntryTextSlot.MapTitle] = Localize(MapTitleKey),
                [RunEntryTextSlot.BattleNode] = Localize(BattleNodeKey),
                [RunEntryTextSlot.Cleared] = Localize(ClearedKey),
                [RunEntryTextSlot.Health] = _localize(HealthKey, healthArguments),
                [RunEntryTextSlot.CardRewardTitle] = Localize(CardRewardTitleKey),
                [RunEntryTextSlot.SkipCardReward] = Localize(SkipCardRewardKey),
                [RunEntryTextSlot.RestTitle] = Localize(RestTitleKey),
                [RunEntryTextSlot.ChestTitle] = Localize(ChestTitleKey),
                [RunEntryTextSlot.ShopTitle] = Localize(ShopTitleKey),
                [RunEntryTextSlot.EventTitle] = Localize(EventTitleKey),
                [RunEntryTextSlot.FailureTitle] = Localize(FailureTitleKey),
                [RunEntryTextSlot.LeaveRun] = Localize(ExitKey),
                [RunEntryTextSlot.Cancel] = Localize(CancelKey),
                [RunEntryTextSlot.ConfirmationTitle] = Localize(
                    deletingUnusableSave ? DeleteTitleKey : AbandonTitleKey),
                [RunEntryTextSlot.ConfirmationMessage] = Localize(
                    deletingUnusableSave ? DeleteMessageKey : AbandonMessageKey),
                [RunEntryTextSlot.ConfirmationConfirm] = Localize(
                    deletingUnusableSave ? DeleteConfirmKey : AbandonConfirmKey),
                [RunEntryTextSlot.SaveIssueTitle] = saveIssue.Length == 0
                    ? string.Empty
                    : Localize(SaveIssueTitleKey),
                [RunEntryTextSlot.SaveIssue] = saveIssue,
                [RunEntryTextSlot.SaveFailureMessage] = Localize(CommitFailedKey),
                [RunEntryTextSlot.RetrySave] = Localize(RetrySaveKey),
                [RunEntryTextSlot.Exit] = Localize(ExitKey),
                [RunEntryTextSlot.RollbackTitle] = Localize(RollbackTitleKey),
                [RunEntryTextSlot.RollbackMessage] = Localize(RollbackMessageKey),
                [RunEntryTextSlot.RollbackConfirm] = Localize(RollbackConfirmKey),
            };
        }

        /// <summary>把类型化存档故障转换为玩家可见的当前语言说明。</summary>
        private string BuildSaveIssueText()
        {
            switch (_flow.Persistence.Status)
            {
                case RunPersistenceStatus.InvalidJson:
                    return Localize(InvalidJsonKey);
                case RunPersistenceStatus.InvalidDocument:
                    return Localize(InvalidDocumentKey);
                case RunPersistenceStatus.UnsupportedSchema:
                    return Localize(UnsupportedSchemaKey);
                case RunPersistenceStatus.InterruptedCommit:
                    return Localize(InterruptedCommitKey);
                case RunPersistenceStatus.IoFailure:
                    return Localize(IoFailureKey);
                case RunPersistenceStatus.DeleteFailed:
                    return Localize(DeleteFailedKey);
                case RunPersistenceStatus.MissingHeroTemplate:
                case RunPersistenceStatus.MissingDeckTemplate:
                case RunPersistenceStatus.MissingEncounterTemplate:
                    return _localize(
                        MissingConfigurationKey,
                        new Dictionary<string, object>
                        {
                            ["kind"] = _flow.Persistence.MissingConfigurationKind,
                            ["id"] = _flow.Persistence.MissingConfigurationId ?? 0,
                        });
                case RunPersistenceStatus.MissingMapProfile:
                    return Localize(InvalidDocumentKey);
                default:
                    return string.Empty;
            }
        }

        /// <summary>存档状态变化时重建当前入口页面投影。</summary>
        private void HandlePersistenceChanged()
        {
            if (!_disposed)
                Render();
        }

        /// <summary>读取无 Smart 参数的当前语言文本。</summary>
        private string Localize(string key)
        {
            return _localize(key, null);
        }

        /// <summary>从生产 ConfigService 延迟读取初始化完成后的 Luban 表。</summary>
        private static Func<Tables> CreateTablesProvider(ConfigService configs)
        {
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));

            return () => configs.Tables;
        }

        /// <summary>把生产 LocalizationService 适配为无 Unity 静态依赖的文本函数。</summary>
        private static Func<string, IReadOnlyDictionary<string, object>, string> CreateLocalizer(
            LocalizationService localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            return localization.GetString;
        }

        /// <summary>验证生产本地化服务并公开其语言变化流。</summary>
        private static Observable<Locale> RequireLocaleChanges(LocalizationService localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            return localization.LocaleChanged;
        }

        /// <summary>拒绝在场景级 Presenter 已释放后重新初始化。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RunEntryPresenter));
        }
    }
}
