namespace TinySpire.Battle
{
    /// <summary>一次命令结算中的记录类别。</summary>
    public enum BattleSettlementRecordType
    {
        EnergySpent,
        AmmoSpent,
        DamageApplied,
        BlockGained,
        AttributeModified,
        StatusApplied,
        CardMoved,
        CardsReshuffled,
        OperationSkipped,
        BlockCleared,
        StatusReduced,
        EnergyRefilled,
        AmmoRefilled,
        EnemyIntentAdvanced,
        EnemyActionSkipped,
        BattlePhaseChanged,
        EnergyGained,
        CardCreated,
        HealthRestored,
        PoisonTicked,
        PotionConsumed,
    }

    /// <summary>M7 可被 Effect 修改的参与者属性。</summary>
    public enum BattleAttributeType
    {
        Strength,
    }

    /// <summary>M7 可被 Effect 施加的参与者状态。</summary>
    public enum BattleStatusType
    {
        Vulnerable,
        Poison,
        BlockRetention,
        Garrison,
        BlockGainDamageTrigger,
        FatalOrBlockBreakCardTrigger,
    }

    /// <summary>卡牌移动记录使用的权威卡区名称。</summary>
    public enum BattleCardZone
    {
        DrawPile,
        Hand,
        DiscardPile,
        ExhaustPile,
        PowerPile,
    }

    /// <summary>合法命令中跳过单个操作的明确原因。</summary>
    public enum BattleOperationSkipReason
    {
        TargetNotAlive,
    }

    /// <summary>整次敌人行动未执行的明确规则原因。</summary>
    public enum BattleEnemyActionSkipReason
    {
        SourceNotAlive,
    }
}
