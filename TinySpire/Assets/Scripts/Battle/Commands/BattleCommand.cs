using System;

namespace TinySpire.Battle
{
    /// <summary>
    /// 战斗命令的稳定类型；调用方只表达意图，不携带执行期规则结果。
    /// </summary>
    public enum BattleCommandType
    {
        StartBattle,
        PlayCard,
        EndPlayerAction,
        CompleteEnemyAction
    }

    /// <summary>
    /// 所有共享战斗写入意图的抽象根。
    /// </summary>
    public abstract class BattleCommand
    {
        /// <summary>命令的稳定领域类型。</summary>
        public abstract BattleCommandType Type { get; }

        /// <summary>提交该命令的参与者；系统命令没有提交者。</summary>
        public abstract CombatantId? SubmitterId { get; }
    }

    /// <summary>
    /// 表达初始化战斗回合事实的系统命令。
    /// </summary>
    public sealed class StartBattleCommand : BattleCommand
    {
        /// <summary>返回开始战斗命令类型。</summary>
        public override BattleCommandType Type => BattleCommandType.StartBattle;

        /// <summary>开始战斗由系统提交，因此没有参与者提交者。</summary>
        public override CombatantId? SubmitterId => null;
    }

    /// <summary>
    /// 表达玩家尝试打出一张运行时卡牌实例的意图。
    /// </summary>
    public sealed class PlayCardCommand : BattleCommand
    {
        /// <summary>尝试出牌的玩家。</summary>
        public CombatantId ActorId { get; }

        /// <summary>尝试打出的运行时卡牌实例。</summary>
        public CardInstanceId CardId { get; }

        /// <summary>返回出牌命令类型。</summary>
        public override BattleCommandType Type => BattleCommandType.PlayCard;

        /// <summary>返回尝试出牌的玩家。</summary>
        public override CombatantId? SubmitterId => ActorId;

        /// <summary>创建出牌意图并拒绝无效参与者或卡牌实例标识。</summary>
        public PlayCardCommand(CombatantId actorId, CardInstanceId cardId)
        {
            if (actorId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(actorId));
            if (cardId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(cardId));

            ActorId = actorId;
            CardId = cardId;
        }
    }

    /// <summary>
    /// 表达单名玩家结束本轮行动的意图。
    /// </summary>
    public sealed class EndPlayerActionCommand : BattleCommand
    {
        /// <summary>声明结束行动的玩家。</summary>
        public CombatantId ActorId { get; }

        /// <summary>返回结束玩家行动命令类型。</summary>
        public override BattleCommandType Type => BattleCommandType.EndPlayerAction;

        /// <summary>返回声明结束行动的玩家。</summary>
        public override CombatantId? SubmitterId => ActorId;

        /// <summary>创建玩家结束行动意图并拒绝无效参与者标识。</summary>
        public EndPlayerActionCommand(CombatantId actorId)
        {
            if (actorId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(actorId));

            ActorId = actorId;
        }
    }

    /// <summary>
    /// 表达当前敌人权威行动已经完成的系统意图。
    /// </summary>
    public sealed class CompleteEnemyActionCommand : BattleCommand
    {
        /// <summary>完成当前行动的敌人。</summary>
        public CombatantId EnemyId { get; }

        /// <summary>返回完成敌人行动命令类型。</summary>
        public override BattleCommandType Type => BattleCommandType.CompleteEnemyAction;

        /// <summary>敌人行动完成由系统提交，因此没有参与者提交者。</summary>
        public override CombatantId? SubmitterId => null;

        /// <summary>创建敌人行动完成意图并拒绝无效参与者标识。</summary>
        public CompleteEnemyActionCommand(CombatantId enemyId)
        {
            if (enemyId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(enemyId));

            EnemyId = enemyId;
        }
    }
}
