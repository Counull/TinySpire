using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TinySpire.Battle
{
    /// <summary>一次卡区写操作产生的明确、冻结且按发生顺序排列的结果。</summary>
    public sealed class BattleCardZoneOperationResult
    {
        /// <summary>指定移动是否满足前置条件；批量抽牌与弃手操作始终成功。</summary>
        public bool Succeeded { get; }

        /// <summary>本次操作实际产生的跨卡区移动记录数量；同一张牌可在重洗后再次移动。</summary>
        public int MovedCardCount { get; }

        /// <summary>可直接并入当前权威命令的冻结卡区结算记录。</summary>
        public IReadOnlyList<BattleSettlementRecord> Settlements { get; }

        /// <summary>复制并冻结一次卡区操作结果，同时统计明确的卡牌移动数量。</summary>
        internal BattleCardZoneOperationResult(
            bool succeeded,
            IEnumerable<BattleSettlementRecord> settlements)
        {
            if (settlements == null)
            {
                throw new ArgumentNullException(nameof(settlements));
            }

            var frozen = new List<BattleSettlementRecord>(settlements);
            if (!succeeded && frozen.Count > 0)
            {
                throw new ArgumentException("失败的卡区操作不能携带结算记录。", nameof(settlements));
            }

            int movedCardCount = 0;
            foreach (BattleSettlementRecord settlement in frozen)
            {
                if (settlement == null)
                {
                    throw new ArgumentException("卡区结算记录不能包含 null。", nameof(settlements));
                }

                if (settlement is BattleCardMovedSettlement)
                {
                    movedCardCount++;
                }
                else if (!(settlement is BattleCardsReshuffledSettlement))
                {
                    throw new ArgumentException(
                        "卡区操作结果只能包含移动或重洗记录。",
                        nameof(settlements));
                }
            }

            Succeeded = succeeded;
            MovedCardCount = movedCardCount;
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(frozen);
        }
    }
}
