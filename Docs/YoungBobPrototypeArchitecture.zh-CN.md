# Young Bob 原型：架构说明

## 1. 项目目标

这是一个最小化的双人合作回合制打牌战斗原型。

当前目标：

- 使用 TapTap 房间制多人联机
- 尽量把玩法放在纯 C# 逻辑层
- UI 仍然以代码驱动为主
- 快速验证完整流程：
  - 连接
  - 创建/加入/匹配房间
  - 进入战斗
  - 出牌
  - 结束我方队伍回合
  - 怪物行动
  - 结束战斗
  - 返回大厅

## 2. 核心文件

### 场景 / 生命周期

- `Assets/Scripts/YoungBob/Prototype/Scene/YoungBobGameManager.cs`
- `Assets/Scripts/YoungBob/Prototype/Scene/YoungBobUiManager.cs`

### 应用 / 会话层

- `Assets/Scripts/YoungBob/Prototype/App/PrototypeSessionController.cs`

### 战斗逻辑

- `Assets/Scripts/YoungBob/Prototype/Battle/BattleTypes.cs`
- `Assets/Scripts/YoungBob/Prototype/Battle/BattleEngine.cs`

### 数据

- `Assets/Scripts/YoungBob/Prototype/Data/GameDataRepository.cs`
- `Assets/Resources/GameData/cards.json`
- `Assets/Resources/GameData/decks.json`
- `Assets/Resources/GameData/encounters.json`

### 多人联机抽象 / TapTap 适配层

- `Assets/Scripts/YoungBob/Prototype/Multiplayer/MultiplayerContracts.cs`
- `Assets/Scripts/YoungBob/Prototype/Multiplayer/TapTapRuntimeMultiplayerService.cs`
- `Assets/Scripts/YoungBob/Prototype/Multiplayer/TapTapMultiplayerService.cs`

## 3. 分层结构

### A 层：场景与生命周期层

负责人：

- `YoungBobGameManager`
- `YoungBobUiManager`

职责：

- 创建运行时依赖
- 控制启动流程
- 连接 UI 与应用状态
- 切换页面：大厅 / 房间 / 战斗

规则：

- 这里不写战斗规则
- 这里不展开 TapTap SDK 细节，只做装配

### B 层：应用 / 会话层

负责人：

- `PrototypeSessionController`

职责：

- 管理当前房间状态
- 管理当前战斗状态
- 把 UI 操作转换为网络消息
- 接收网络消息并触发应用行为
- 把状态变化广播给 UI

规则：

- 这是协调层
- 它可以依赖传输接口和战斗引擎
- 它不应该直接写原始 TapTap SDK 调用

### C 层：战斗逻辑层

负责人：

- `BattleEngine`
- `BattleTypes`

职责：

- 定义战斗状态
- 校验战斗命令
- 推进回合流程
- 应用卡牌效果
- 应用怪物行为
- 产出战斗事件

规则：

- 尽量保持为纯 C#
- 不要放 TapTap SDK 代码
- 不要放 UI 代码

### D 层：数据与传输适配层

负责人：

- `GameDataRepository`
- `IMultiplayerService`
- `TapTapRuntimeMultiplayerService`

职责：

- 加载静态内容数据
- 定义联机抽象接口
- 把 TapTap SDK 适配成项目内部的联机事件与操作

规则：

- TapTap 的平台差异、坑、兼容处理都放在这里
- 不要把 TapTap 特有逻辑泄露到战斗规则层

## 4. 当前战斗规则

### 队伍共享回合模型

当前玩家回合是“整个队伍共享一个回合”。

规则：

- 所有存活玩家都可以在玩家阶段行动
- 一张牌打出后立即生效
- 每个玩家各自维护 `hasEndedTurn`
- 某个玩家点击 `End Turn` 只结束自己的行动资格
- 只有当所有存活玩家都结束后，才进入怪物回合
- 怪物回合结束后，重置所有存活玩家的行动资格，回到队伍回合

### 当前简化内容

卡牌：

- `Strike`：对怪物造成伤害
- `Heal`：治疗玩家

怪物：

- 简单攻击行为

胜利条件：

- 怪物 HP 归零

失败条件：

- 全体玩家死亡

## 5. UI 结构

UI 仍然是代码驱动，但由管理器统一拥有。

主管理器：

- `YoungBobUiManager`

页面：

- `LobbyPage`
- `RoomPage`
- `BattlePage`

规则：

- 页面可以是类
- UI 元素可以继续在代码中动态创建
- 不要把玩法状态机放进页面类
- UI 负责渲染状态，不负责定义规则

## 6. 数据规则

当前游戏数据从这里读取：

- `Resources/GameData`

当前这样做的原因：

- 相比直接读 `StreamingAssets`，在移动端 / Tap 运行环境更稳定

规则：

- 新卡牌 / 新遭遇 / 新起始牌组优先作为数据配置增加
- 不要回到 Inspector 驱动玩法逻辑

## 7. 多人联机抽象

内部联机入口：

- `IMultiplayerService`

当前事件：

- `Connected`
- `RoomJoined`
- `RoomListUpdated`
- `MessageReceived`

当前操作：

- `Connect`
- `Disconnect`
- `CreateRoom`
- `MatchOrCreateRoom`
- `RefreshRoomList`
- `JoinRoom`
- `LeaveRoom`
- `Send`

规则：

- 游戏 / 会话层依赖 `IMultiplayerService`
- 不直接依赖 TapTap SDK

## 8. TapTap 多人联机注意事项

这些是当前项目里最重要的 TapTap 规则。

### 规则 1：使用房间 + 自定义消息

对于这个原型：

- 使用 TapTap 房间 API
- 使用 `SendCustomMessage`
- 不使用帧同步做核心玩法

原因：

- 这是回合制游戏
- 消息频率低
- 状态同步方案更简单

### 规则 2：`SendCustomMessage(type=0)` 不会回给发送者

含义：

- 发送者不会收到自己的 `OnCustomMessage`
- 只有其他玩家会收到

项目后果：

- 发送者必须在发送成功后本地立即执行相同行为
- 否则发送端和接收端状态会分叉

当前项目已经在 Tap 传输层处理了这件事。

### 规则 3：房间请求结构尽量贴近官方 demo

对于房间接口，请保持这些结构显式存在：

- `roomCfg`
- `playerCfg`
- `customProperties`
- `matchParams`

不要过度简化请求对象。

原因：

- Tap 的桥接层 / runtime 对结构是比较严格的

### 规则 4：避免并发 `GetRoomList`

Tap 的房间列表请求如果上一个还没结束，下一个可能直接失败。

项目后果：

- 刷新房间列表必须节流
- 自动刷新不能叠加并发请求

### 规则 5：自定义消息结构要显式

建议外层固定使用这种信封结构：

- `messageId`
- `type`
- `senderPlayerId`
- `roomId`
- `seq`
- `payload`

避免：

- 依赖不稳定的隐式序列化
- 过深的临时嵌套
- 不同消息类型随意变形

### 规则 6：离房流程要通过服务确认完成

不要过早清空本地房间状态。

错误模式：

- 点击离开房间
- 立刻清本地状态
- 之后又收到 Tap 的房间事件
- UI 出现抖动或回弹

正确模式：

- 发起离房请求
- 等待服务成功 / 房间事件确认后再完成状态切换

### 规则 7：TapTap 特有逻辑只放在适配层

TapTap 特有逻辑应该只存在于：

- `TapTapRuntimeMultiplayerService`

不应该扩散到：

- `BattleEngine`
- 战斗状态定义
- UI 页面

## 9. 当前战斗同步模型

项目现在使用“房主权威”模型。

### 消息类型

- `battle.start`
- `battle.command`
- `battle.commit`
- `battle.finish`

### `battle.start`

发送者：

- 只有房主

作用：

- 让所有客户端建立初始战斗

### `battle.command`

发送者：

- 任意客户端

作用：

- 只表达玩家意图
- 例如：
  - 出牌
  - 结束回合

规则：

- 不把它当作最终战斗结果

### `battle.commit`

发送者：

- 只有房主

作用：

- 广播某条命令的权威结算结果

当前 payload 包含：

- 源命令 id
- 战斗状态快照
- 战斗事件列表

规则：

- 所有客户端只应用 `battle.commit`
- 只有房主执行真正的权威状态推进

### `battle.finish`

发送者：

- 只有房主

作用：

- 结束战斗
- 驱动返回大厅流程

## 10. 重要不变量

下面这些约束应尽量一直保持成立。

### 架构不变量

- 战斗规则只在纯 C# 战斗层里
- UI 不拥有玩法状态机
- TapTap SDK 代码只在传输适配层
- 会话层负责协调，但不要膨胀成无边界的大对象

### 同步不变量

- 客户端发送意图
- 房主校验并执行
- 房主广播权威 commit
- 所有端应用 commit 后再渲染

### 数据不变量

- 新内容优先从数据定义扩展
- 新卡牌 / 新怪物 / 新遭遇不依赖 Inspector 业务逻辑

## 11. 建议的后续工作

最值得继续做的方向：

1. 在当前队伍回合模型下继续打磨战斗交互体验
2. 继续收紧 Host / Non-host 的非法操作 UI 状态
3. 如果战斗复杂度继续增长，进一步规范 `battle.commit` 的快照与字段
4. 增加一个非 Tap 的调试 relay transport，方便多人开发联调

## 12. 极短总结

如果只记一个最短心智模型：

- `GameManager`：启动和装配
- `UIManager`：页面切换和输入
- `SessionController`：房间 + 战斗协调
- `BattleEngine`：真正的玩法规则
- `TapTapRuntimeMultiplayerService`：Tap 房间与消息适配

而当前最重要的多人联机规则是：

- 客户端发送 `battle.command`
- 房主执行
- 房主广播 `battle.commit`
- 所有人应用 commit

