using Newtonsoft.Json.Linq;
using NUnit.Framework;

public sealed class BattleHeroResourceProfileBuildValidatorTests
{
    /// <summary>验证一份同时包含默认战士和未来机枪兵数值形状的 Hero 表可以通过构建期门禁。</summary>
    [Test]
    public void Validate_AcceptsBoundedEnergyAndOptionalAmmoProfiles()
    {
        JObject heroes = new JObject
        {
            ["1001"] = CreateHero(1001, 3, 3, 3, 0, 0, 0),
            ["1002"] = CreateHero(1002, 3, 5, 3, 5, 5, 1),
        };

        Assert.DoesNotThrow(() => BattleHeroResourceProfileBuildValidator.Validate(heroes));
    }

    /// <summary>验证能量初始值越过上限时在 Addressables 构建前被拒绝。</summary>
    [Test]
    public void Validate_RejectsEnergyInitialValueAboveMaximum()
    {
        JObject heroes = new JObject
        {
            ["1001"] = CreateHero(1001, 4, 3, 3, 0, 0, 0),
        };

        System.InvalidOperationException exception = Assert.Throws<System.InvalidOperationException>(
            () => BattleHeroResourceProfileBuildValidator.Validate(heroes));
        StringAssert.Contains("invalid Energy profile", exception.Message);
    }

    /// <summary>验证禁用弹药的 Hero 不得留下弹药初始值或回合补充。</summary>
    [Test]
    public void Validate_RejectsDisabledAmmoWithNonZeroGain()
    {
        JObject heroes = new JObject
        {
            ["1001"] = CreateHero(1001, 3, 3, 3, 0, 0, 1),
        };

        System.InvalidOperationException exception = Assert.Throws<System.InvalidOperationException>(
            () => BattleHeroResourceProfileBuildValidator.Validate(heroes));
        StringAssert.Contains("invalid Ammo profile", exception.Message);
    }

    /// <summary>创建只含资源字段的最小生成 Hero 记录，保持每个测试只变化一个约束。</summary>
    private static JObject CreateHero(
        int id,
        int initialEnergy,
        int maxEnergy,
        int energyGainPerRound,
        int initialAmmo,
        int maxAmmo,
        int ammoGainPerRound)
    {
        return new JObject
        {
            ["id"] = id,
            ["initial_energy"] = initialEnergy,
            ["max_energy"] = maxEnergy,
            ["energy_gain_per_round"] = energyGainPerRound,
            ["initial_ammo"] = initialAmmo,
            ["max_ammo"] = maxAmmo,
            ["ammo_gain_per_round"] = ammoGainPerRound,
        };
    }
}
