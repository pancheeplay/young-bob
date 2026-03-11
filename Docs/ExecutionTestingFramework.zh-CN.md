# Young Bob 执行-测试框架（LLM 自循环）

## 1. 目标

为本项目建立可自动闭环的流程：

1. LLM 修改数据与代码
2. 自动执行单机/联机场景测试
3. 自动断言结果并输出失败原因
4. 基于失败信息继续修复并重试

核心要求：

- 非交互、可脚本化（适配 LLM/CI）
- 可复现（固定随机种子 + 明确步骤）
- 可断言（结果不是“看日志判断”，而是结构化 pass/fail）
- 可扩展（新增卡牌/怪物/效果时，只需新增场景与断言）

## 2. 分层设计

### 2.1 Domain Core（现有）

目录：`Assets/Scripts/YoungBob/Prototype/Battle`

职责：

- 战斗规则与状态推进
- 不依赖 UI
- 不依赖网络传输实现

约束：

- 所有新效果（如 Vulnerable、加诅咒）必须在这一层可表达和可测试

### 2.2 Test Driver API（新增）

目录：`Assets/Scripts/YoungBob/Prototype/Testing`

职责：

- 提供可编排 API：创建房间、加入房间、开战、出牌、结束回合、读取状态、等待同步
- 返回结构化结果，供断言与报告
- 支持单客户端和多客户端模式

约束：

- 不做 REPL 输入循环
- 不把业务逻辑写进 CLI

### 2.3 Scenario Spec（新增）

目录：`Assets/Resources/TestScenarios`

职责：

- 用 JSON 定义 Given/When/Then
- 让 LLM 修改后可自动新增或更新场景，而不是手写流程脚本

内容：

- meta：场景名、模式（single/multi）、seed
- setup：encounter、deck、players
- steps：动作序列
- assertions：状态断言、跨客户端一致性断言

### 2.4 Scenario Runner（新增）

目录：`Assets/Scripts/YoungBob/Prototype/Testing`

职责：

- 加载场景
- 调用 Test Driver 执行动作
- 执行断言
- 输出结构化报告（JSON）

### 2.5 Thin CLI（后续可选）

目录建议：`Tools/BattleScenarioRunner`

职责：

- 解析参数并调用 Scenario Runner
- 例如：`--scenario curse_multi --report /tmp/young-bob-report.json`

约束：

- 只做入口层，不承载规则判断

## 3. 执行流水线

## 3.1 每次功能改动的标准流程

1. 修改游戏数据（如 `cards.json` / `encounters.json`）
2. 修改 Battle 规则代码
3. 更新/新增场景 JSON
4. 执行 scenario runner（single + multi）
5. 读取报告并判断 pass/fail
6. 失败则进入下一轮修复

## 3.2 必须覆盖的验证维度

- 功能正确性：效果是否触发
- 目标合法性：例如诅咒是否只能对队友
- 状态同步：host/client 最终状态是否一致
- 稳定性：同一 seed 多次结果一致

## 4. 诅咒示例（你给的需求）

需求：

- 怪物攻击时给玩家手牌加入一张诅咒卡
- 诅咒卡只能对队友打出
- 诅咒命中后目标获得 Vulnerable

对应断言：

1. 怪物技能后，目标玩家手牌包含 `curse_*`
2. 对自己使用诅咒应失败（返回错误码/错误文本）
3. 对队友使用诅咒应成功
4. 队友状态中 `Vulnerable` 层数按预期变化
5. 双客户端最终快照 hash 一致

## 5. 目录建议

- `Assets/Scripts/YoungBob/Prototype/Testing/BattleTestDriverContracts.cs`
- `Assets/Scripts/YoungBob/Prototype/Testing/BattleScenarioModels.cs`
- `Assets/Scripts/YoungBob/Prototype/Testing/BattleScenarioRunner.cs`
- `Assets/Resources/TestScenarios/curse_single.json`
- `Assets/Resources/TestScenarios/curse_multi.json`

## 6. 渐进实施顺序

1. 先做 Test Driver Contracts + Scenario Models（今天可完成）
2. 再做 Runner 的动作执行与基础断言
3. 最后接入真实网络多客户端启动（DebugRelay / TapTap）

这样可以先把规则正确性跑通，再逐步放大到联机端到端验证。

## 7. 命令入口（当前版本）

当前已提供 Unity 可调用入口：

- `YoungBob.Prototype.Testing.BattleScenarioCommandEntry.Run`

参数：

- `--scenario TestScenarios/curse_single`
- `--report /tmp/young-bob-scenario-report.json`

示例（Unity 命令行）：

```bash
Unity \
  -batchmode \
  -quit \
  -projectPath /Users/usr/Documents/unity_projects/young-bob \
  -executeMethod YoungBob.Prototype.Testing.BattleScenarioCommandEntry.Run \
  --scenario TestScenarios/curse_multi \
  --report /tmp/young-bob-reports/curse_multi.json
```

注意：

- 若当前机器上 Unity Editor 被占用，可在空闲时段执行该命令；
- 日常开发仍可通过 `BattleScenarioExecutionService.RunScenarioFromResourcesAsJson(...)` 在编辑器内触发。
