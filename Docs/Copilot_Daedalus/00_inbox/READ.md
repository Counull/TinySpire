# 🪖 MarineSoldier · 杀戮尖塔2 陆战队员角色 Mod

把自制的类杀戮尖塔原型《机枪兵》移植为杀戮尖塔2 的新角色（陆战队员）。

> 基于 [RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)（内容注册/角色脚手架框架）+ [RitsuLibModTemplate](https://github.com/alkaid616/RitsuLibModTemplate)。

## ✅ 当前状态（2026-08-11 全机制实装）

- ✅ **角色**：陆战队员（70 血 / 3 能量，军绿→亮青主题色）
- ✅ **78 张卡**：5 初始 + 原型 58 奖励 + 13 张 mod 新卡（防御姿态/霸凌/幻彩射击/先发制人/紧急散热 + 让我抽抽抽/给你吸吸二手烟 + 狂轰滥炸/天空之怒 + 标记/铝热炸弹/踏碎 + 钢针风暴）+ 先古卡超级强化剂 + 临时卡机枪扫射
- ✅ **弹药系统**（RitsuLib SecondaryResource）：
  - 弹药上限 5、开局满弹、战斗内持久、每回合 +1（CMC 遗物）
  - 射击卡消耗弹药：射击1/扫射2/狙击2/钉刺1/击退1/精准3/战术六连1(+额外)
  - 猛烈发狂 = 发射全部弹药（弹药量决定伤害次数）；不解释12连 = 最多6发→换弹→最多6发（两波换弹）
  - 换弹/翻滚换弹 = 补满弹药
- ✅ **真燃烧**（MarineBurnPower）：回合末可被格挡的持续伤害，替代原版 PoisonPower 占位
- ✅ **射击分类词条**：shoot（射击）/sniper（狙击）/shotgun（霰弹占位）；另有 **support（支援）** 词条挂在火力支援/燃烧轰炸/女妖/三连击/钢针风暴上
- ✅ **支援类吃易伤**：火力支援/女妖战机/燃烧轰炸/三连击延迟狙击/钢针的延迟伤害吃受击方易伤（×1.5），仍只过格挡、不吃力量；**狂轰滥炸**（每层 +10% 支援伤害与状态层数）、**天空之怒**（每支援打击段触发一次随机+溅射）、**破甲**（三类伤害都增幅，只吃易伤）三大联动
- ✅ **原版 Power 适配**：虚弱/易伤/力量/格挡/缓冲(Buffer)/毒(Poison)/束缚/隐身等
- ✅ 中英文本地化、战斗弹药计数器 UI、先古升级系统（Orobas 奖励转换）
- ⏳ **待做**：卡图美术（GPT 账号额度到位后批量出，需求清单见 `docs/card-art-brief.md`）；专属敌人与地图（用户明确搁置）

**已修的坑（历史）**：原版 Power 不能 `new`/不能传 canonical/`ToMutable` 会崩（PoisonPower）——统一用 `MutableClone()+ApplyInternal`；卡牌升级数值用 DynamicVar（RepeatVar/IntVar/PowerVar）存、不能存私有字段；支援伤害不能用 `AttackCommand.FromCard(null)`（改 `CreatureCmd.Damage`）；Token 池必须注册为共享卡池（否则先古卡预览崩溃）；数值一律走卡牌变量不写死（便于后续加影响变量）。

## 📦 安装依赖

| 依赖 | 说明 | 状态 |
|---|---|---|
| 杀戮尖塔2（Steam） | 游戏本体，Early Access（需 ≥0.107.1） | ✅ 已装 |
| .NET SDK 9+ | 编译 C# mod（`net9.0`，用户级安装于 `C:\Users\Administrator\AppData\Local\Microsoft\dotnet`） | ✅ 已装 |
| Godot 4.5.1 Mono | 打 `.pck` 资源包（`E:\Godot\Godot_v4.5.1-stable_mono_win64\`） | ✅ 已装 |
| RitsuLib 运行时 | 游戏 mods 目录下的共享框架（**本机走 Steam Workshop 订阅**，`local.props` 设 `RitsuLibAutoCopy=false`，不自动部署） | ✅ 已订阅 |

## 🔧 构建

```powershell
# 1) 确保 local.props 里的游戏路径正确（已指向 E:\SteamLibrary\...）
# 2) 构建（自动：编译 → 导出 pck → 复制 dll+json 到游戏 mods/MarineSoldier/）
#    注意：必须设置 DOTNET_ROOT 指向 .NET 9，否则 Godot 无头导出 PCK 会报 hostfxr 错误
$env:DOTNET_ROOT = "C:\Users\Administrator\AppData\Local\Microsoft\dotnet"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
dotnet build .\MarineSoldier.csproj
```

- **构建前先停游戏**（dll 被占用会失败，必要时 `taskkill /PID x /F`）。
- **PCK 导出失败**（`hostfxr_initialize_for_runtime_config failed`）：就是没设 `DOTNET_ROOT`/PATH，PCK 会滞留旧版导致本地化改动不生效。
- 产物：`mods\MarineSoldier\MarineSoldier.dll` + `.json` + `.pck`，运行时依赖 `mods\STS2-RitsuLib`（Workshop 版）。

## 🎮 在游戏里启用

1. Steam → 杀戮尖塔2 → **Play with Mods** 启动（不能直接启动）
2. 首次启动接受"untrusted code"警告，完全重启一次
3. 主菜单 → **Mods** → 勾选 `MarineSoldier`（确认 `STS2-RitsuLib` 已订阅生效），再重启一次
4. 新游戏选角色 **陆战队员**

## 🗂 工程结构

```
sts2-marine-mod/
├── local.props            # 本机路径（游戏目录/RunPckExport/RitsuLibAutoCopy）
├── MarineSoldier.csproj   # 工程（引用游戏程序集 + RitsuLib）
├── MarineSoldier.json     # mod 清单（id/依赖）
├── MarineSoldierCode/     # C# 源码
│   ├── Entry.cs           # mod 入口 [ModInitializer]（词条/目标类型/弹药/UI/自动注册）
│   ├── Characters/        # 角色 + 卡池（奖励池/Token池）+ 遗物池/药水池
│   ├── Cards/             # 卡牌（72 张）
│   ├── Powers/            # power（燃烧/烟雾/兴奋剂/隐身/支援/女妖/开火/功夫机甲等）
│   └── Relics/            # 遗物（CmcArmorRelic=CMC-400动力装甲 / CmcRoyalGuardRelic=CMC皇家卫队）
└── MarineSoldier/         # Godot 资源（图片/场景/本地化，打进 .pck）
    └── localization/      # zhs/eng 的 cards/powers/relics/static_hover_tips.json
```

## ⚙️ 核心机制速查

- **CMC-400动力装甲**（唯一初始遗物）：每回合 +1 弹药、能量上限 +2（引擎3→5）+ 跨回合保留、每回合恢复 3。先古精炼（Orobas 奖励）→ **CMC皇家卫队**：+2 弹药/回合、能量上限 +4、恢复 4。
- **先古升级**：兴奋剂 → **超级强化剂**（0费、2恢复+抽3+1兴奋剂）；CMC-400 → CMC皇家卫队。属性标注自动注册，无需手动 API。
- **兴奋剂**：持续 N 回合，射击词条卡额外射击 1 发（耗 1 弹药，复制该卡虚弱/易伤）。
- **烟雾**：造成/受到攻击伤害每层 -1；默认玩家回合开始清空（减半），有烟雾弥漫则每回合 -1。敌人烟雾在敌人回合衰减。**狙击特性（2026-08-11）**：狙击不吃自身烟雾减输出（攻击者烟雾无效），只吃敌人身上烟雾减伤。
- **缓冲（全息诱饵/防御靶机）**：原版 BufferPower，完全抵挡一次攻击。防御靶机=每3弹1层缓冲（最多9弹，升级1费）。
- **破甲（MarineArmorBreakPower）**：每层使目标受到的攻击伤害+1（可叠加、不随回合衰减）；对攻击/支援/燃烧三类伤害都生效，只吃易伤，不吃力量/虚弱/烟雾；来源=标记/铝热炸弹/踏碎/钢针风暴。攻击走 ModifyDamageAdditive（格挡前参与），支援/燃烧走 `Entry.ArmorBreakBonus` 追加。
- **隐身**：受到攻击伤害减半，狙击视为易伤，回合结束/打攻击-1（狙击不减）。
- **支援伤害**：只过格挡、不吃力量，吃易伤 + 狂轰滥炸（每层+10%）+ 破甲（层数×易伤）；统一经 `Entry.SupportDamage(attacker, target, baseAmount)` 结算；天空之怒每支援打击段触发一次。
- **狂轰滥炸**：每层使支援类效果伤害与附加状态层数提高10%（可叠层、不随回合衰减）；`SupportStateAmount` 作用于附加状态（燃烧/浸油等）。
- **失去力量**：负 StrengthPower + 补回标记 power，回合结束恢复。
- **X 费卡**（猛烈发狂/疾风肘击）：`MockSetEnergyCost(costsX:true)` + `ResolveEnergyXValue()`。
- **最近/最远目标**：点击即打（多目标类型），OnPlay 内 `GetNearestEnemy`/`GetFarthestEnemy` 解析。

## 🚧 已知问题（2026-08-11）

- 卡图全部占位图（`docs/art-samples/` 有星际争霸2风格样张）
- 燃烧（MarineBurnPower）联机双倍结算隐患（敌人燃烧可能每轮结算 2 次，暂未改）
- 先古奖励已修复预览崩溃，尚待实际通关触发 Orobas 对话验证转换

## 📚 参考

- 杀戮尖塔2 mod 制作教程（中文）：https://tutorials.sts2modding.com/
- RitsuLib：https://github.com/BAKAOLC/STS2-RitsuLib
- 模板：https://github.com/alkaid616/RitsuLibModTemplate

> 详细开发记录 / 技术坑见 `HANDOFF.md`（新会话必读）。
