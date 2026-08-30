using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TinySpire.Run.History
{
    /// <summary>一个英雄从逐局历史派生的只读终局统计。</summary>
    public sealed class RunHistoryHeroStatistics
    {
        /// <summary>英雄模板身份。</summary>
        public int HeroTemplateId { get; }

        /// <summary>该英雄全部终局数。</summary>
        public int TotalRuns { get; }

        /// <summary>该英雄 Victory 数。</summary>
        public int VictoryCount { get; }

        /// <summary>该英雄 Defeat 数。</summary>
        public int DefeatCount { get; }

        /// <summary>该英雄 Abandoned 数。</summary>
        public int AbandonedCount { get; }

        /// <summary>该英雄胜率，范围为零到一。</summary>
        public double VictoryRate => TotalRuns == 0 ? 0d : (double)VictoryCount / TotalRuns;

        /// <summary>冻结一组经过和式校验的英雄统计。</summary>
        internal RunHistoryHeroStatistics(
            int heroTemplateId,
            int totalRuns,
            int victoryCount,
            int defeatCount,
            int abandonedCount)
        {
            if (heroTemplateId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroTemplateId));
            ValidateCounts(totalRuns, victoryCount, defeatCount, abandonedCount);

            HeroTemplateId = heroTemplateId;
            TotalRuns = totalRuns;
            VictoryCount = victoryCount;
            DefeatCount = defeatCount;
            AbandonedCount = abandonedCount;
        }

        /// <summary>验证总数恰好由三种封闭 outcome 组成。</summary>
        private static void ValidateCounts(
            int totalRuns,
            int victoryCount,
            int defeatCount,
            int abandonedCount)
        {
            if (totalRuns < 0 || victoryCount < 0 || defeatCount < 0 || abandonedCount < 0)
                throw new ArgumentOutOfRangeException(nameof(totalRuns));
            if (totalRuns != checked(victoryCount + defeatCount + abandonedCount))
                throw new ArgumentException("Run history outcome counts must sum to total runs.");
        }
    }

    /// <summary>从全部逐局摘要派生、从不单独持久化的只读统计投影。</summary>
    public sealed class RunHistoryStatistics
    {
        private readonly ReadOnlyCollection<RunHistoryHeroStatistics> _heroes;

        /// <summary>全部终局数。</summary>
        public int TotalRuns { get; }

        /// <summary>全部 Victory 数。</summary>
        public int VictoryCount { get; }

        /// <summary>全部 Defeat 数。</summary>
        public int DefeatCount { get; }

        /// <summary>全部 Abandoned 数。</summary>
        public int AbandonedCount { get; }

        /// <summary>全局胜率，范围为零到一。</summary>
        public double VictoryRate => TotalRuns == 0 ? 0d : (double)VictoryCount / TotalRuns;

        /// <summary>按 HeroTemplateId 升序冻结的分英雄统计。</summary>
        public IReadOnlyList<RunHistoryHeroStatistics> Heroes => _heroes;

        /// <summary>冻结经过和式校验的全局与分英雄统计。</summary>
        internal RunHistoryStatistics(
            int totalRuns,
            int victoryCount,
            int defeatCount,
            int abandonedCount,
            IEnumerable<RunHistoryHeroStatistics> heroes)
        {
            if (heroes == null)
                throw new ArgumentNullException(nameof(heroes));
            if (totalRuns < 0 || victoryCount < 0 || defeatCount < 0 || abandonedCount < 0)
                throw new ArgumentOutOfRangeException(nameof(totalRuns));
            if (totalRuns != checked(victoryCount + defeatCount + abandonedCount))
                throw new ArgumentException("Run history outcome counts must sum to total runs.");

            RunHistoryHeroStatistics[] frozenHeroes = heroes.ToArray();
            if (frozenHeroes.Any(hero => hero == null))
                throw new ArgumentException("Run history hero statistics cannot contain null.", nameof(heroes));
            if (frozenHeroes.Select(hero => hero.HeroTemplateId).Distinct().Count() != frozenHeroes.Length)
                throw new ArgumentException("Run history hero statistics must be unique.", nameof(heroes));
            if (frozenHeroes.Sum(hero => hero.TotalRuns) != totalRuns ||
                frozenHeroes.Sum(hero => hero.VictoryCount) != victoryCount ||
                frozenHeroes.Sum(hero => hero.DefeatCount) != defeatCount ||
                frozenHeroes.Sum(hero => hero.AbandonedCount) != abandonedCount)
            {
                throw new ArgumentException("Hero statistics must sum to global Run history totals.", nameof(heroes));
            }

            TotalRuns = totalRuns;
            VictoryCount = victoryCount;
            DefeatCount = defeatCount;
            AbandonedCount = abandonedCount;
            _heroes = Array.AsReadOnly(
                frozenHeroes.OrderBy(hero => hero.HeroTemplateId).ToArray());
        }

        /// <summary>按英雄身份查找一组统计；没有该英雄历史时返回空。</summary>
        public RunHistoryHeroStatistics FindHero(int heroTemplateId)
        {
            return _heroes.SingleOrDefault(hero => hero.HeroTemplateId == heroTemplateId);
        }
    }

    /// <summary>把唯一逐局历史集合纯计算为全局和分英雄统计。</summary>
    public static class RunHistoryStatisticsProjector
    {
        /// <summary>按 RunId 去重同内容摘要并拒绝冲突摘要后生成统计。</summary>
        public static RunHistoryStatistics Project(IEnumerable<RunSummary> summaries)
        {
            if (summaries == null)
                throw new ArgumentNullException(nameof(summaries));

            var unique = new Dictionary<RunId, RunSummary>();
            foreach (RunSummary summary in summaries)
            {
                if (summary == null)
                    throw new ArgumentException("Run history cannot contain null summaries.", nameof(summaries));
                if (unique.TryGetValue(summary.RunId, out RunSummary existing))
                {
                    if (!existing.Equals(summary))
                    {
                        throw new InvalidOperationException(
                            "Conflicting summaries cannot be projected for the same Run id.");
                    }

                    continue;
                }

                unique.Add(summary.RunId, summary);
            }

            RunSummary[] frozen = unique.Values.ToArray();
            int victories = frozen.Count(summary => summary.OutcomeKind == RunOutcomeKind.Victory);
            int defeats = frozen.Count(summary => summary.OutcomeKind == RunOutcomeKind.Defeat);
            int abandoned = frozen.Count(summary => summary.OutcomeKind == RunOutcomeKind.Abandoned);
            RunHistoryHeroStatistics[] heroes = frozen
                .GroupBy(summary => summary.HeroTemplateId)
                .Select(group => new RunHistoryHeroStatistics(
                    group.Key,
                    group.Count(),
                    group.Count(summary => summary.OutcomeKind == RunOutcomeKind.Victory),
                    group.Count(summary => summary.OutcomeKind == RunOutcomeKind.Defeat),
                    group.Count(summary => summary.OutcomeKind == RunOutcomeKind.Abandoned)))
                .OrderBy(hero => hero.HeroTemplateId)
                .ToArray();

            return new RunHistoryStatistics(
                frozen.Length,
                victories,
                defeats,
                abandoned,
                heroes);
        }
    }
}
