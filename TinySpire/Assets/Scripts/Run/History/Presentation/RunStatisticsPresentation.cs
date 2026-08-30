using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using R3;
using VContainer;
using VContainer.Unity;

namespace TinySpire.Run.History.Presentation
{
    /// <summary>统计页面当前只读加载状态。</summary>
    public enum RunStatisticsViewStatus
    {
        Ready,
        Unavailable,
    }

    /// <summary>Statistics View 可按稳定槽位读取的本地化文本。</summary>
    public enum RunStatisticsTextSlot
    {
        Title,
        TotalRunsLabel,
        VictoryLabel,
        DefeatLabel,
        AbandonedLabel,
        VictoryRateLabel,
        EmptyHistory,
    }

    /// <summary>Statistics 页面与 i18n 表共同使用的稳定 key。</summary>
    public static class RunStatisticsLocalizationKeys
    {
        public const string Title = "app.statistics.title";
        public const string TotalRuns = "app.statistics.total_runs";
        public const string Victory = "app.statistics.victory";
        public const string Defeat = "app.statistics.defeat";
        public const string Abandoned = "app.statistics.abandoned";
        public const string VictoryRate = "app.statistics.victory_rate";
        public const string Hero = "app.statistics.hero";
        public const string Empty = "app.statistics.empty";
        public const string LoadFailure = "app.statistics.failure.load";
    }

    /// <summary>Statistics 页面中一行按英雄派生的不可变投影。</summary>
    public sealed class StatisticsHeroRowViewModel
    {
        /// <summary>英雄模板身份。</summary>
        public int HeroTemplateId { get; }

        /// <summary>带英雄参数的已本地化行标题。</summary>
        public string HeroText { get; }

        /// <summary>该英雄全部终局数。</summary>
        public int TotalRuns { get; }

        /// <summary>该英雄 Victory 数。</summary>
        public int VictoryCount { get; }

        /// <summary>该英雄 Defeat 数。</summary>
        public int DefeatCount { get; }

        /// <summary>该英雄 Abandoned 数。</summary>
        public int AbandonedCount { get; }

        /// <summary>该英雄胜率，范围为零到一。</summary>
        public double VictoryRate { get; }

        /// <summary>使用稳定数值格式的胜率显示文本。</summary>
        public string VictoryRateText { get; }

        /// <summary>冻结一行已验证的英雄统计投影。</summary>
        internal StatisticsHeroRowViewModel(
            int heroTemplateId,
            string heroText,
            int totalRuns,
            int victoryCount,
            int defeatCount,
            int abandonedCount,
            double victoryRate,
            string victoryRateText)
        {
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            if (string.IsNullOrEmpty(heroText))
                throw new ArgumentException("Statistics hero text cannot be empty.", nameof(heroText));
            if (totalRuns < 0 || victoryCount < 0 || defeatCount < 0 || abandonedCount < 0)
                throw new ArgumentOutOfRangeException(nameof(totalRuns));
            if (totalRuns != checked(victoryCount + defeatCount + abandonedCount))
                throw new ArgumentException("Statistics hero outcome counts must sum to total runs.");
            if (victoryRate < 0d || victoryRate > 1d)
                throw new ArgumentOutOfRangeException(nameof(victoryRate));
            if (string.IsNullOrEmpty(victoryRateText))
                throw new ArgumentException("Statistics victory rate text cannot be empty.", nameof(victoryRateText));

            HeroTemplateId = heroTemplateId;
            HeroText = heroText;
            TotalRuns = totalRuns;
            VictoryCount = victoryCount;
            DefeatCount = defeatCount;
            AbandonedCount = abandonedCount;
            VictoryRate = victoryRate;
            VictoryRateText = victoryRateText;
        }
    }

    /// <summary>Statistics View 一次完整替换使用的不可变投影。</summary>
    public sealed class StatisticsViewModel
    {
        private readonly IReadOnlyDictionary<RunStatisticsTextSlot, string> _texts;
        private readonly ReadOnlyCollection<StatisticsHeroRowViewModel> _heroRows;

        /// <summary>当前历史加载状态。</summary>
        public RunStatisticsViewStatus Status { get; }

        /// <summary>Ready 时的全部终局数；Unavailable 时为空。</summary>
        public int? TotalRuns { get; }

        /// <summary>Ready 时的 Victory 数；Unavailable 时为空。</summary>
        public int? VictoryCount { get; }

        /// <summary>Ready 时的 Defeat 数；Unavailable 时为空。</summary>
        public int? DefeatCount { get; }

        /// <summary>Ready 时的 Abandoned 数；Unavailable 时为空。</summary>
        public int? AbandonedCount { get; }

        /// <summary>Ready 时范围零到一的胜率；Unavailable 时为空。</summary>
        public double? VictoryRate { get; }

        /// <summary>Ready 时使用稳定数值格式的胜率文本；Unavailable 时为空。</summary>
        public string VictoryRateText { get; }

        /// <summary>Ready 时按 HeroTemplateId 排序的只读英雄行。</summary>
        public IReadOnlyList<StatisticsHeroRowViewModel> HeroRows => _heroRows;

        /// <summary>Unavailable 时的本地化故障文本；Ready 时为空。</summary>
        public string FailureText { get; }

        /// <summary>只有成功读取且确实没有逐局历史时才为真。</summary>
        public bool IsEmpty => Status == RunStatisticsViewStatus.Ready && TotalRuns == 0;

        /// <summary>冻结状态、可空统计、英雄行与全部本地化文本。</summary>
        private StatisticsViewModel(
            RunStatisticsViewStatus status,
            int? totalRuns,
            int? victoryCount,
            int? defeatCount,
            int? abandonedCount,
            double? victoryRate,
            string victoryRateText,
            IEnumerable<StatisticsHeroRowViewModel> heroRows,
            IReadOnlyDictionary<RunStatisticsTextSlot, string> texts,
            string failureText)
        {
            if (!Enum.IsDefined(typeof(RunStatisticsViewStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (heroRows == null)
                throw new ArgumentNullException(nameof(heroRows));
            if (texts == null)
                throw new ArgumentNullException(nameof(texts));
            if (victoryRateText == null)
                throw new ArgumentNullException(nameof(victoryRateText));
            if (failureText == null)
                throw new ArgumentNullException(nameof(failureText));

            StatisticsHeroRowViewModel[] frozenRows = heroRows.ToArray();
            if (frozenRows.Any(row => row == null))
                throw new ArgumentException("Statistics hero rows cannot contain null.", nameof(heroRows));
            bool readyShape = totalRuns.HasValue &&
                              victoryCount.HasValue &&
                              defeatCount.HasValue &&
                              abandonedCount.HasValue &&
                              victoryRate.HasValue;
            if ((status == RunStatisticsViewStatus.Ready) != readyShape)
                throw new ArgumentException("Statistics values do not match the view status.", nameof(status));
            if (status == RunStatisticsViewStatus.Unavailable && frozenRows.Length > 0)
                throw new ArgumentException("Unavailable statistics cannot publish hero rows.", nameof(heroRows));

            Status = status;
            TotalRuns = totalRuns;
            VictoryCount = victoryCount;
            DefeatCount = defeatCount;
            AbandonedCount = abandonedCount;
            VictoryRate = victoryRate;
            VictoryRateText = victoryRateText;
            _heroRows = Array.AsReadOnly(
                frozenRows.OrderBy(row => row.HeroTemplateId).ToArray());
            _texts = new ReadOnlyDictionary<RunStatisticsTextSlot, string>(
                new Dictionary<RunStatisticsTextSlot, string>(texts));
            FailureText = failureText;
        }

        /// <summary>从完整领域统计建立正常只读投影。</summary>
        internal static StatisticsViewModel Ready(
            RunHistoryStatistics statistics,
            IEnumerable<StatisticsHeroRowViewModel> heroRows,
            IReadOnlyDictionary<RunStatisticsTextSlot, string> texts,
            string victoryRateText)
        {
            if (statistics == null)
                throw new ArgumentNullException(nameof(statistics));

            return new StatisticsViewModel(
                RunStatisticsViewStatus.Ready,
                statistics.TotalRuns,
                statistics.VictoryCount,
                statistics.DefeatCount,
                statistics.AbandonedCount,
                statistics.VictoryRate,
                victoryRateText,
                heroRows,
                texts,
                string.Empty);
        }

        /// <summary>建立不携带任何零统计假象的明确不可用投影。</summary>
        internal static StatisticsViewModel Unavailable(
            IReadOnlyDictionary<RunStatisticsTextSlot, string> texts,
            string failureText)
        {
            if (string.IsNullOrEmpty(failureText))
                throw new ArgumentException("Statistics failure text cannot be empty.", nameof(failureText));

            return new StatisticsViewModel(
                RunStatisticsViewStatus.Unavailable,
                null,
                null,
                null,
                null,
                null,
                string.Empty,
                Array.Empty<StatisticsHeroRowViewModel>(),
                texts,
                failureText);
        }

        /// <summary>按稳定槽位读取已本地化文本，并拒绝不完整模型。</summary>
        public string GetText(RunStatisticsTextSlot slot)
        {
            if (!_texts.TryGetValue(slot, out string value))
                throw new InvalidOperationException($"Run statistics text slot '{slot}' is missing.");

            return value;
        }
    }

    /// <summary>Statistics Presenter 与 Unity View 之间唯一的只读渲染 seam。</summary>
    public interface IRunStatisticsView
    {
        /// <summary>用完整不可变投影替换当前 Statistics 页面。</summary>
        void Render(StatisticsViewModel model);
    }

    /// <summary>从逐局历史即时派生并本地化只读 Statistics 页面。</summary>
    public sealed class RunStatisticsPresenter : IInitializable, IDisposable
    {
        private readonly IRunStatisticsView _view;
        private readonly RunHistoryService _history;
        private readonly Func<string, IReadOnlyDictionary<string, object>, string> _localize;
        private readonly Func<Action, IDisposable> _subscribeLocaleChanged;

        private bool _initialized;
        private bool _disposed;
        private IDisposable _localeSubscription;
        private RunHistoryStatisticsLoadResult _currentStatistics;

        /// <summary>以生产 LocalizationService 创建 VContainer 可驱动 Presenter。</summary>
        [Inject]
        public RunStatisticsPresenter(
            IRunStatisticsView view,
            RunHistoryService history,
            LocalizationService localization)
            : this(
                view,
                history,
                CreateLocalizer(localization),
                CreateLocaleSubscription(localization))
        {
        }

        /// <summary>以可替换本地化 seam 创建可直接 EditMode 验证的 Presenter。</summary>
        public RunStatisticsPresenter(
            IRunStatisticsView view,
            RunHistoryService history,
            Func<string, IReadOnlyDictionary<string, object>, string> localize,
            Func<Action, IDisposable> subscribeLocaleChanged)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _history = history ?? throw new ArgumentNullException(nameof(history));
            _localize = localize ?? throw new ArgumentNullException(nameof(localize));
            _subscribeLocaleChanged = subscribeLocaleChanged ??
                throw new ArgumentNullException(nameof(subscribeLocaleChanged));
        }

        /// <summary>一次性读取统计快照并订阅 locale 与完整历史统计变化。</summary>
        public void Initialize()
        {
            ThrowIfDisposed();
            if (_initialized)
                return;

            _initialized = true;
            _currentStatistics = _history.LoadStatistics();
            _history.StatisticsChanged += AcceptStatistics;
            try
            {
                _localeSubscription = _subscribeLocaleChanged(RenderCurrent) ??
                    throw new InvalidOperationException("Locale subscription cannot be null.");
            }
            catch
            {
                _history.StatisticsChanged -= AcceptStatistics;
                _currentStatistics = null;
                _initialized = false;
                throw;
            }

            RenderCurrent();
        }

        /// <summary>解除 locale 与历史订阅并停止后续重绘。</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _history.StatisticsChanged -= AcceptStatistics;
            _localeSubscription?.Dispose();
            _localeSubscription = null;
            _currentStatistics = null;
        }

        /// <summary>接收 owner 发布的完整统计结果并按当前 locale 替换页面。</summary>
        private void AcceptStatistics(RunHistoryStatisticsLoadResult statistics)
        {
            _currentStatistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
            RenderCurrent();
        }

        /// <summary>使用已缓存统计结果按当前 locale 构造完整不可变模型。</summary>
        private void RenderCurrent()
        {
            RunHistoryStatisticsLoadResult load = _currentStatistics ??
                throw new InvalidOperationException("Statistics snapshot is not initialized.");
            IReadOnlyDictionary<RunStatisticsTextSlot, string> texts = BuildTexts();
            if (load.Status == RunHistoryStatisticsLoadStatus.Success)
            {
                StatisticsHeroRowViewModel[] rows = load.Statistics.Heroes
                    .Select(CreateHeroRow)
                    .ToArray();
                _view.Render(StatisticsViewModel.Ready(
                    load.Statistics,
                    rows,
                    texts,
                    FormatVictoryRate(load.Statistics.VictoryRate)));
                return;
            }

            var failureArguments = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["detail"] = load.Detail,
            };
            _view.Render(StatisticsViewModel.Unavailable(
                texts,
                _localize(RunStatisticsLocalizationKeys.LoadFailure, failureArguments)));
        }

        /// <summary>把一组领域英雄统计转换为已本地化只读行。</summary>
        private StatisticsHeroRowViewModel CreateHeroRow(
            RunHistoryHeroStatistics hero)
        {
            var arguments = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["hero_template_id"] = hero.HeroTemplateId,
            };
            return new StatisticsHeroRowViewModel(
                hero.HeroTemplateId,
                _localize(RunStatisticsLocalizationKeys.Hero, arguments),
                hero.TotalRuns,
                hero.VictoryCount,
                hero.DefeatCount,
                hero.AbandonedCount,
                hero.VictoryRate,
                FormatVictoryRate(hero.VictoryRate));
        }

        /// <summary>按稳定 key 解析页面全部固定文本。</summary>
        private IReadOnlyDictionary<RunStatisticsTextSlot, string> BuildTexts()
        {
            return new Dictionary<RunStatisticsTextSlot, string>
            {
                [RunStatisticsTextSlot.Title] = Localize(RunStatisticsLocalizationKeys.Title),
                [RunStatisticsTextSlot.TotalRunsLabel] = Localize(RunStatisticsLocalizationKeys.TotalRuns),
                [RunStatisticsTextSlot.VictoryLabel] = Localize(RunStatisticsLocalizationKeys.Victory),
                [RunStatisticsTextSlot.DefeatLabel] = Localize(RunStatisticsLocalizationKeys.Defeat),
                [RunStatisticsTextSlot.AbandonedLabel] = Localize(RunStatisticsLocalizationKeys.Abandoned),
                [RunStatisticsTextSlot.VictoryRateLabel] = Localize(RunStatisticsLocalizationKeys.VictoryRate),
                [RunStatisticsTextSlot.EmptyHistory] = Localize(RunStatisticsLocalizationKeys.Empty),
            };
        }

        /// <summary>读取一个无动态参数的稳定 i18n key。</summary>
        private string Localize(string key)
        {
            return _localize(key, null);
        }

        /// <summary>把零到一胜率格式化为不受 locale 数字符号影响的百分比。</summary>
        private static string FormatVictoryRate(double victoryRate)
        {
            if (victoryRate < 0d || victoryRate > 1d)
                throw new ArgumentOutOfRangeException(nameof(victoryRate));

            double percent = Math.Round(victoryRate * 100d, 1, MidpointRounding.AwayFromZero);
            return percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>把生产本地化服务收窄为带可选参数的函数 seam。</summary>
        private static Func<string, IReadOnlyDictionary<string, object>, string> CreateLocalizer(
            LocalizationService localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            return localization.GetString;
        }

        /// <summary>把生产 locale 流收窄为无载荷重绘订阅 seam。</summary>
        private static Func<Action, IDisposable> CreateLocaleSubscription(
            LocalizationService localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            return handler => localization.LocaleChanged.Subscribe(_ => handler());
        }

        /// <summary>Presenter 释放后拒绝重新初始化。</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RunStatisticsPresenter));
        }
    }
}
