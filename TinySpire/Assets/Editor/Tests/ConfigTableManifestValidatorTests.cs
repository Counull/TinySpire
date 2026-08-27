using NUnit.Framework;

/// <summary>
/// 配置表清单漂移校验的构建期契约测试。
/// </summary>
public sealed class ConfigTableManifestValidatorTests
{
    /// <summary>运行时预加载清单必须显式包含两张 G5 Run 持有物表。</summary>
    [Test]
    public void RequiredRuntimeTables_ContainRunRelicAndPotionTables()
    {
        Assert.That(ConfigService.RequiredTableNames, Does.Contain("run_tbrelic"));
        Assert.That(ConfigService.RequiredTableNames, Does.Contain("run_tbpotion"));
    }

    /// <summary>当前项目中的 battle 与 run client 表必须在定义、生成代码、JSON 和运行时清单间完全一致。</summary>
    [Test]
    public void ValidateCurrentProject_WithGeneratedRunItemTables_HasNoManifestDrift()
    {
        Assert.DoesNotThrow(ConfigTableManifestValidator.ValidateCurrentProject);
    }

    /// <summary>验证缺少生成 JSON 时会给出包含遗漏表名的失败，而不是允许运行时清单漂移。</summary>
    [Test]
    public void ValidateSets_WhenGeneratedJsonMissesRuntimeTable_ThrowsDriftWithTableName()
    {
        const string heroTable = "battle_tbhero";
        const string enemyTable = "battle_tbenemy";

        ConfigTableManifestDriftException failure = Assert.Throws<ConfigTableManifestDriftException>(
            () => ConfigTableManifestValidator.ValidateSets(
                new[] { heroTable, enemyTable },
                new[] { heroTable, enemyTable },
                new[] { heroTable },
                new[] { heroTable, enemyTable }));

        Assert.That(failure.Message, Does.Contain(enemyTable));
        Assert.That(failure.Message, Does.Contain("generated JSON"));
    }

    /// <summary>验证手写运行时清单的重复项会被构建期校验直接拒绝，而不是被集合去重掩盖。</summary>
    [Test]
    public void ValidateSets_WhenRuntimeTableListContainsDuplicate_ThrowsDriftWithTableName()
    {
        const string heroTable = "battle_tbhero";
        const string enemyTable = "battle_tbenemy";

        Assert.That(
            () => ConfigTableManifestValidator.ValidateSets(
                new[] { heroTable, heroTable, enemyTable },
                new[] { heroTable, enemyTable },
                new[] { heroTable, enemyTable },
                new[] { heroTable, enemyTable }),
            Throws.TypeOf<ConfigTableManifestDriftException>()
                .With.Message.Contains("runtime TableNames has duplicate")
                .And.Message.Contains(heroTable));
    }
}
