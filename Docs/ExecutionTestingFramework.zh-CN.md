# Young Bob 执行-测试框架（LLM 自循环）

## 1. 目标

为本项目建立可自动闭环流程：

1. LLM 修改数据与代码
2. 自动执行场景测试（single + multi 语义）
3. 自动断言并产出 JSON 报告
4. 基于失败信息继续修复并重跑

核心要求：

- 非交互、可脚本化（适配 LLM/CI）
- 可复现（固定 seed + 场景步骤）
- 可断言（结构化 pass/fail，而不是人工看日志）

## 2. 当前落地结构

### 2.1 战斗域（Domain Core）

目录：`Assets/Scripts/YoungBob/Prototype/Battle`

职责：

- 战斗状态与规则推进
- 卡牌效果、怪物技能、目标合法性
- 与 UI、网络层解耦

### 2.2 场景定义（Scenario Spec）

目录：`Assets/Resources/TestScenarios`

当前场景：

- `curse_single.json`
- `curse_multi.json`

结构：

- `setup`：`stageId`/`monsterId`/deck/players/seed（`monsterId` 用于快速直测某个怪物）
- `steps`：`end_turn` / `play_card` / `snapshot`
- `steps` 也支持 debug 指令（用于规则回归，不改正式战斗数据）：
  - `debug_damage_monster`（配合 `debugValue`）
  - `debug_set_player_hp`（`targetUnitId` + `debugValue`）
- `assertions`：`snapshot_contains` / `snapshot_error_contains` / `snapshot_hash_equals` 等

### 2.3 纯 dotnet 执行器（主入口）

目录：`Tools/DotnetVerifier`

文件：

- `DotnetVerifier.csproj`
- `Program.cs`
- `JsonGameDataRepository.cs`

职责：

- 加载 GameData 与 Scenario JSON
- 直接调用 `BattleEngine` 执行步骤
- 生成快照、执行断言、输出汇总报告 JSON

说明：

- 该入口不依赖 Unity batch，不依赖旧 REPL CLI
- 适合 LLM 自循环与 CI

### 2.4 Unity 入口（保留）

目录：`Assets/Scripts/YoungBob/Prototype/Testing`

说明：

- Unity 侧 runner 保留用于编辑器内触发
- 当前自动化主路径以 `Tools/DotnetVerifier` 为准

## 3. 命令用法（推荐）

运行全部场景并输出报告：

```bash
dotnet run --project Tools/DotnetVerifier/DotnetVerifier.csproj -- --scenario all --report /tmp/young-bob-dotnet-verifier-report.json
```

只跑单个场景：

```bash
dotnet run --project Tools/DotnetVerifier/DotnetVerifier.csproj -- --scenario curse_single --report /tmp/curse_single_report.json
```

参数：

- `--scenario`：`all` / `curse_single` / `curse_multi` / 自定义 JSON 路径
- `--report`：报告输出路径

退出码：

- `0`：全部通过
- `2`：存在失败用例
- `1`：执行异常

## 4. 诅咒功能（当前实现）

当前规则：

- 怪物通过**指定技能** `curse_prayer` 施加诅咒（不是普通攻击）
- `curse_prayer` 的施放动作：`castPoseId = prayer`
- 普通攻击 `normal_attack` 的施放动作：`castPoseId = idle`
- 诅咒卡 `curse_betrayal` 只能对队友打出，命中后目标获得 Vulnerable

关键数据：

- `Assets/Resources/GameData/encounters.json`
- `Assets/Resources/GameData/cards.json`

关键断言：

1. 怪物技能后手牌出现 `curse_betrayal`
2. 对自己出诅咒失败（错误包含 `Invalid curse target.`）
3. 对队友出诅咒成功并提升易伤层数
4. single/multi 场景均通过

## 5. 边界与下一步

当前 dotnet 执行器验证的是“战斗域规则正确性”，不覆盖：

- 真实网络收发与房间同步
- Unity UI 点击链路

后续可并行增加联机 E2E 层（DebugRelay/TapTap），与当前规则层验证互补。
