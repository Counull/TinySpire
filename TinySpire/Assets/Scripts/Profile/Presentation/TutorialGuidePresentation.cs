using System;
using TinySpire.Settings;

namespace TinySpire.Profile.Presentation
{
    /// <summary>全局教程层使用的只读应用可访问性投影，不持有或回写设置事实。</summary>
    public sealed class TutorialGuideAccessibilityViewModel
    {
        /// <summary>教程文字使用的离散缩放档位。</summary>
        public AppTextScale TextScale { get; }

        /// <summary>是否启用教程高对比配色。</summary>
        public bool HighContrast { get; }

        /// <summary>是否移除教程按钮的非必要颜色过渡。</summary>
        public bool ReducedMotion { get; }

        /// <summary>冻结一份只读可访问性投影。</summary>
        public TutorialGuideAccessibilityViewModel(
            AppTextScale textScale,
            bool highContrast,
            bool reducedMotion)
        {
            if (!Enum.IsDefined(typeof(AppTextScale), textScale))
                throw new ArgumentOutOfRangeException(nameof(textScale));

            TextScale = textScale;
            HighContrast = highContrast;
            ReducedMotion = reducedMotion;
        }
    }

    /// <summary>教程 View 使用的稳定 i18n key 集合，不在本 seam 解析具体语言文本。</summary>
    public static class TutorialGuideTextKeys
    {
        public const string MainMenuWelcome = "tutorial.guide.main_menu_welcome";
        public const string HeroSelection = "tutorial.guide.hero_selection";
        public const string MapRoute = "tutorial.guide.map_route";
        public const string BattleBasics = "tutorial.guide.battle_basics";
        public const string CardReward = "tutorial.guide.card_reward";
        public const string NonCombatNode = "tutorial.guide.non_combat_node";
        public const string RunOutcome = "tutorial.guide.run_outcome";
        public const string Confirm = "tutorial.guide.confirm";
        public const string Skip = "tutorial.guide.skip";
        public const string Reset = "tutorial.guide.reset";

        /// <summary>把封闭提示身份映射为唯一稳定正文 key。</summary>
        public static string GetPromptTextKey(TutorialPromptId promptId)
        {
            switch (promptId)
            {
                case TutorialPromptId.MainMenuWelcome:
                    return MainMenuWelcome;
                case TutorialPromptId.HeroSelection:
                    return HeroSelection;
                case TutorialPromptId.MapRoute:
                    return MapRoute;
                case TutorialPromptId.BattleBasics:
                    return BattleBasics;
                case TutorialPromptId.CardReward:
                    return CardReward;
                case TutorialPromptId.NonCombatNode:
                    return NonCombatNode;
                case TutorialPromptId.RunOutcome:
                    return RunOutcome;
                default:
                    throw new ArgumentOutOfRangeException(nameof(promptId), promptId, null);
            }
        }
    }

    /// <summary>教程阻挡层一次渲染使用的完整不可变投影。</summary>
    public sealed class TutorialGuideViewModel
    {
        private static readonly TutorialGuideViewModel HiddenModel =
            new TutorialGuideViewModel(
                isVisible: false,
                blocksInput: false,
                promptId: null,
                promptTextKey: string.Empty);

        /// <summary>当前是否应该显示教程层。</summary>
        public bool IsVisible { get; }

        /// <summary>当前教程层是否阻挡其下方正常输入。</summary>
        public bool BlocksInput { get; }

        /// <summary>可见时对应的唯一提示身份；隐藏时为空。</summary>
        public TutorialPromptId? PromptId { get; }

        /// <summary>可见提示正文的稳定 i18n key；隐藏时为空串。</summary>
        public string PromptTextKey { get; }

        /// <summary>确认按钮的稳定 i18n key。</summary>
        public string ConfirmTextKey => TutorialGuideTextKeys.Confirm;

        /// <summary>跳过按钮的稳定 i18n key。</summary>
        public string SkipTextKey => TutorialGuideTextKeys.Skip;

        /// <summary>重置按钮的稳定 i18n key。</summary>
        public string ResetTextKey => TutorialGuideTextKeys.Reset;

        /// <summary>所有无提示上下文共用的不阻挡隐藏投影。</summary>
        public static TutorialGuideViewModel Hidden => HiddenModel;

        /// <summary>冻结一份可见或隐藏的完整教程投影。</summary>
        private TutorialGuideViewModel(
            bool isVisible,
            bool blocksInput,
            TutorialPromptId? promptId,
            string promptTextKey)
        {
            if (isVisible && !promptId.HasValue)
                throw new ArgumentException("Visible tutorial model requires a prompt id.", nameof(promptId));
            if (isVisible && string.IsNullOrWhiteSpace(promptTextKey))
                throw new ArgumentException("Visible tutorial model requires a text key.", nameof(promptTextKey));
            if (!isVisible && (blocksInput || promptId.HasValue || promptTextKey.Length > 0))
                throw new ArgumentException("Hidden tutorial model cannot retain blocking prompt state.");

            IsVisible = isVisible;
            BlocksInput = blocksInput;
            PromptId = promptId;
            PromptTextKey = promptTextKey ?? throw new ArgumentNullException(nameof(promptTextKey));
        }

        /// <summary>为当前有序提示创建阻挡输入的可见投影。</summary>
        internal static TutorialGuideViewModel Visible(TutorialPromptDefinition prompt)
        {
            if (prompt == null)
                throw new ArgumentNullException(nameof(prompt));

            return new TutorialGuideViewModel(
                isVisible: true,
                blocksInput: true,
                promptId: prompt.Id,
                promptTextKey: TutorialGuideTextKeys.GetPromptTextKey(prompt.Id));
        }
    }

    /// <summary>Presenter 与真实教程层之间唯一、无玩法事实的 View seam。</summary>
    public interface ITutorialGuideView
    {
        /// <summary>玩家确认当前提示时发布无 payload 事件。</summary>
        event Action ConfirmRequested;

        /// <summary>玩家跳过余下教程时发布无 payload 事件。</summary>
        event Action SkipRequested;

        /// <summary>玩家重置教程时发布无 payload 事件。</summary>
        event Action ResetRequested;

        /// <summary>用完整不可变投影替换当前教程层。</summary>
        void Render(TutorialGuideViewModel model);

        /// <summary>应用当前应用设置派生的只读教程可访问性投影。</summary>
        void ApplyAccessibility(TutorialGuideAccessibilityViewModel model);
    }
}
