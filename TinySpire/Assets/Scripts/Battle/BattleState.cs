using System;
using System.Collections.Generic;

namespace TinySpire.Battle
{
    /// <summary>
    /// 一场战斗内 CombatantId 到参与者的唯一权威映射。
    /// 阵营与存活视图均从该字典的值按需派生，不保存镜像集合。
    /// </summary>
    public sealed class BattleState
    {
        private readonly Dictionary<CombatantId, CombatantState> _combatants = new Dictionary<CombatantId, CombatantState>();
        private int _nextCombatantId = 1;

        public event Action Changed;

        public IReadOnlyDictionary<CombatantId, CombatantState> Combatants => _combatants;

        public PlayerCombatantState AddPlayer(int templateId, int maxHealth, int strength)
        {
            var player = new PlayerCombatantState(AllocateCombatantId(), templateId, maxHealth, strength);
            AddCombatant(player);
            return player;
        }

        public EnemyCombatantState AddEnemy(int templateId, int maxHealth, int strength)
        {
            var enemy = new EnemyCombatantState(AllocateCombatantId(), templateId, maxHealth, strength);
            AddCombatant(enemy);
            return enemy;
        }

        public bool TryGetCombatant(CombatantId id, out CombatantState combatant)
        {
            return _combatants.TryGetValue(id, out combatant);
        }

        public bool ApplyDamage(CombatantId targetId, int damage)
        {
            if (!TryGetCombatant(targetId, out CombatantState target))
                return false;

            if (!target.ApplyDamage(damage))
                return false;

            Changed?.Invoke();
            return true;
        }

        private CombatantId AllocateCombatantId()
        {
            return new CombatantId(_nextCombatantId++);
        }

        private void AddCombatant(CombatantState combatant)
        {
            _combatants.Add(combatant.Id, combatant);
            Changed?.Invoke();
        }
    }
}
