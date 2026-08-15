using System;
using System.Collections.Generic;

namespace TinySpire.Battle
{
    /// <summary>
    /// 单场战斗中 CombatantId 到参与者数据的唯一事实映射。
    /// </summary>
    public sealed class BattleCombatantsData : IDisposable
    {
        private readonly Dictionary<CombatantId, CombatantData> _combatants = new Dictionary<CombatantId, CombatantData>();
        private int _nextCombatantId = 1;

        /// <summary>
        /// 本场全部参与者的只读映射。玩家、敌人及存活列表应由此按需派生。
        /// </summary>
        public IReadOnlyDictionary<CombatantId, CombatantData> All => _combatants;

        /// <summary>
        /// 根据玩家模板的入场基础属性创建并加入玩家参与者。
        /// </summary>
        public PlayerCombatantData AddPlayer(int templateId, int maxHealth, int strength)
        {
            return AddPlayer(templateId, maxHealth, maxHealth, strength);
        }

        /// <summary>根据 Run 当前生命与 Hero 上限创建并加入玩家参与者。</summary>
        public PlayerCombatantData AddPlayer(
            int templateId,
            int currentHealth,
            int maxHealth,
            int strength)
        {
            var player = new PlayerCombatantData(
                AllocateCombatantId(),
                templateId,
                currentHealth,
                maxHealth,
                strength);
            Add(player);
            return player;
        }

        /// <summary>
        /// 根据敌人模板的入场基础属性创建并加入敌人参与者。
        /// </summary>
        public EnemyCombatantData AddEnemy(int templateId, int maxHealth, int strength)
        {
            var enemy = new EnemyCombatantData(AllocateCombatantId(), templateId, maxHealth, strength);
            Add(enemy);
            return enemy;
        }

        /// <summary>
        /// 按战斗内标识查找参与者，不生成任何派生集合。
        /// </summary>
        public bool TryGet(CombatantId id, out CombatantData combatant)
        {
            return _combatants.TryGetValue(id, out combatant);
        }

        /// <summary>
        /// 释放全部参与者的响应式资源。
        /// </summary>
        public void Dispose()
        {
            foreach (CombatantData combatant in _combatants.Values)
                combatant.Dispose();
        }

        /// <summary>分配只在本场战斗内有效的下一个参与者标识。</summary>
        private CombatantId AllocateCombatantId()
        {
            return new CombatantId(_nextCombatantId++);
        }

        /// <summary>将已创建的参与者写入唯一事实字典。</summary>
        private void Add(CombatantData combatant)
        {
            _combatants.Add(combatant.Id, combatant);
        }
    }
}
