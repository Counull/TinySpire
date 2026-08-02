using System;

namespace TinySpire.Battle
{
    /// <summary>从当前存活阵营即时派生的中立规则结果，不保存到 Turn。</summary>
    internal enum BattleTerminalOutcome
    {
        Ongoing,
        Victory,
        Defeat,
        InvalidFacts,
    }

    /// <summary>只读取本场参与者事实并即时派生是否终局及胜负。</summary>
    internal sealed class BattleTerminalRules
    {
        private readonly BattleCombatantsData _combatants;

        /// <summary>绑定本场唯一参与者聚合，不缓存任何存活数量或 outcome。</summary>
        internal BattleTerminalRules(BattleCombatantsData combatants)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
        }

        /// <summary>从调用时的当前 Health 派生 ongoing、victory、defeat 或未定义的空阵营事实。</summary>
        internal BattleTerminalOutcome Evaluate()
        {
            bool hasLivingPlayer = false;
            bool hasLivingEnemy = false;
            foreach (CombatantData combatant in _combatants.All.Values)
            {
                if (!combatant.IsAlive)
                    continue;
                if (combatant is PlayerCombatantData)
                    hasLivingPlayer = true;
                else if (combatant is EnemyCombatantData)
                    hasLivingEnemy = true;
            }

            if (hasLivingPlayer && hasLivingEnemy)
                return BattleTerminalOutcome.Ongoing;
            if (hasLivingPlayer)
                return BattleTerminalOutcome.Victory;
            if (hasLivingEnemy)
                return BattleTerminalOutcome.Defeat;
            return BattleTerminalOutcome.InvalidFacts;
        }
    }
}
