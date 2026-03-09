# TapOnlineBattleDemo - 多人联机游戏演示

## 📖 项目概述

这是一个使用**TapSDK多人联机功能**的完整demo，展示了从用户登录到实时对战的完整流程。

**核心特色**：
- ✅ **支持两种同步模式**：帧同步 + 状态同步（可切换）
- ✅ **完整的业务流程**：登录 → 匹配 → 对战 → 结束
- ✅ **网络头像加载**：带缓存机制，节省带宽
- ✅ **中途加入游戏**：状态同步模式支持玩家中途加入战斗
- ✅ **代码结构清晰**：职责分离，易于学习和扩展

**适用场景**：
- 🎮 学习TapSDK多人联机API的使用
- 📚 了解帧同步和状态同步的区别
- 🔧 作为自己项目的代码参考
- 🚀 快速接入多人联机功能

---

## 🎮 游戏玩法

### 核心功能
- **用户授权登录**：完整的TapSDK授权流程，获取用户头像和昵称
- **同步模式切换**：支持帧同步和状态同步两种模式（UI Toggle切换）
- **房间匹配**：支持创建房间和匹配房间
- **玩家列表**：实时显示房间内所有玩家（头像、昵称、房主标识）
- **实时对战**：点击屏幕移动玩家头像，实时看到其他玩家操作
- **房主控制**：只有房主可以启动游戏
- **中途加入**：状态同步模式支持战斗进行时加入

### 设计原则
- **超级简单**：只实现最核心的联机功能，不添加多余特性
- **最小实现**：专注于多人联机SDK的基本使用流程
- **易于理解**：代码结构清晰，注释详细，便于学习
- **模块化设计**：Manager分离，职责单一

---

## 🏗️ 技术架构

### 核心分层设计

```
┌─────────────────────────────────────────────────────────┐
│  UI层 (UIManager, BattleInteraction, RoomListUI)      │
│  - UI切换和显示                                        │
│  - 用户交互处理                                        │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────┴──────────────────────────────────┐
│  业务逻辑层 (GameManager, TapSDKService)               │
│  - 游戏流程控制                                        │
│  - SDK调用和事件处理                                   │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────┴──────────────────────────────────┐
│  同步管理层 (StateSyncManager, FrameSyncManager)       │
│  - 帧同步逻辑（FrameSyncManager）                      │
│  - 状态同步逻辑（StateSyncManager）                    │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────┴──────────────────────────────────┐
│  工具层 (AvatarManager, GameConfig)                    │
│  - 头像下载和缓存                                      │
│  - 全局配置管理                                        │
└─────────────────────────────────────────────────────────┘
```

---

## 📁 项目结构

```
Assets/TapOnlineBattleDemo/
├── Scripts/                            # 所有C#脚本
│   ├── 核心Manager/
│   │   ├── GameManager.cs              # 游戏主管理器（游戏入口）
│   │   ├── TapSDKService.cs            # TapSDK服务层（SDK核心）
│   │   ├── StateSyncManager.cs         # ✨状态同步管理器（新增）
│   │   ├── FrameSyncManager.cs         # ✨帧同步管理器（新增）
│   │   └── AvatarManager.cs            # ✨头像管理器（新增）
│   ├── UI组件/
│   │   ├── UIManager.cs                # UI管理器（3层UI切换）
│   │   ├── BattleInteraction.cs        # 对战交互（点击移动）
│   │   ├── PlayerComponent.cs          # 玩家UI组件
│   │   ├── RoomListUI.cs               # 房间列表UI
│   │   └── RoomItemUI.cs               # 房间项UI
│   ├── 事件处理/
│   │   └── TapBattleEventHandler.cs    # 多人联机事件处理器
│   ├── 数据结构/
│   │   ├── GameConfig.cs               # ✨游戏配置（同步模式）
│   │   ├── MoveData.cs                 # 移动数据结构
│   │   ├── RoomCustomProperties.cs     # 房间自定义属性
│   │   └── PlayerCustomProperties.cs   # 玩家自定义属性
│   └── 工具类/
│       ├── FrameRateStats.cs           # 帧率统计
│       ├── NetworkLatencyStats.cs      # 网络延迟统计
│       └── PingData.cs                 # Ping数据结构
├── Scenes/
│   └── TapOnlineBattle.unity           # 主游戏场景
├── Prefabs/
│   └── PlayerPrefab.prefab             # 玩家预制体
└── Fonts/                              # 字体资源
```

**✨ 新增文件说明**：
- `StateSyncManager.cs`：状态同步模式的完整实现（398行）
- `FrameSyncManager.cs`：帧同步模式的完整实现（~180行）
- `AvatarManager.cs`：统一的头像管理（~180行，消除重复代码）
- `GameConfig.cs`：全局配置管理（同步模式切换）

---

## 🎯 两种同步模式对比

### 帧同步模式（FrameSync）

**特点**：
- 只发送输入指令（如"按下前进键"）
- 服务器收集所有输入并打包广播
- 所有客户端执行相同计算
- 需要确定性随机数保证一致

**适用场景**：
- 射击游戏
- 格斗游戏
- MOBA、RTS等实时对战

**核心API**：
```csharp
// 开始游戏
TapBattleClient.StartBattle();

// 发送输入
TapBattleClient.SendInput({ inputData });

// 接收帧数据
OnBattleFrame(frameData);

// 结束游戏
TapBattleClient.StopBattle();
```

**参考代码**：`FrameSyncManager.cs`

---

### 状态同步模式（StateSync）✨

**特点**：
- 发送完整状态（如"玩家A在(100, 50)位置"）
- 服务器直接转发消息
- 所有客户端执行相同操作
- 不需要确定性，开发简单

**适用场景**：
- 五子棋、象棋
- 卡牌游戏
- 回合制RPG
- 休闲类游戏

**核心API**：
```csharp
// 发送自定义消息
TapBattleClient.SendCustomMessage({
    msg: JSON字符串,
    type: 0  // 发送给房间内所有人（不包括发送者）
});

// 接收自定义消息
OnCustomMessage(info);
```

**⚠️ 重要**：`type=0`不会触发发送者的`OnCustomMessage`，所以发送者需要在`success`回调中本地立即执行操作！

**参考代码**：`StateSyncManager.cs`

---

### 模式对比表

| 维度 | 帧同步 | 状态同步 |
|------|--------|---------|
| **开始游戏** | StartBattle() | SendCustomMessage("START_GAME") |
| **发送数据** | SendInput（输入指令） | SendCustomMessage（完整状态） |
| **接收数据** | OnBattleFrame（服务器打包） | OnCustomMessage（服务器转发） |
| **结束游戏** | StopBattle() | SendCustomMessage("STOP_GAME") |
| **中途加入** | ❌ 不支持 | ✅ 支持 |
| **确定性要求** | ⚠️ 需要 | ✅ 不需要 |
| **开发难度** | 高 | 低 |
| **适用场景** | 实时对战 | 回合制、休闲类 |

---

## 🚀 快速开始

### 1. 运行Demo

1. **打开场景**：`Assets/TapOnlineBattleDemo/Scenes/TapOnlineBattle.unity`
2. **运行游戏**：点击Unity编辑器的Play按钮
3. **查看流程**：
   - 自动初始化SDK
   - 自动用户登录
   - 显示房间UI
4. **测试功能**：
   - 点击"匹配房间"或"创建房间"
   - 等待其他玩家加入
   - 房主点击"开始游戏"
   - 点击屏幕移动玩家

### 2. 切换同步模式

**UI切换**：
1. 在房间UI顶部找到"模式切换"Toggle
2. **Toggle OFF** = 帧同步模式
3. **Toggle ON** = 状态同步模式（默认）

**代码切换**：
```csharp
// 设置为帧同步
GameConfig.SetSyncMode(SyncMode.FrameSync);

// 设置为状态同步
GameConfig.SetSyncMode(SyncMode.StateSync);
```

### 3. 测试建议

**帧同步测试**：
1. 切换到帧同步模式
2. 两台设备进入房间
3. 开始游戏 → 点击移动 → 结束游戏

**状态同步测试**：
1. 切换到状态同步模式（默认）
2. 两台设备进入房间
3. 开始游戏 → 点击移动 → 结束游戏

**中途加入测试（状态同步特有）**：
1. 设备A单独开始游戏
2. 设备B后加入房间
3. 设备B自动进入战斗界面

---

## 📚 核心组件详解

### 1. GameManager（游戏入口）

**职责**：控制游戏初始化流程

**初始化流程**：
```
1. TapSDK初始化
2. 用户登录（Tap.Login）
3. 获取用户信息（Tap.GetUserInfo，完整授权流程）
4. 多人联机初始化（TapBattleClient.Initialize）
5. 连接多人联机服务（TapBattleClient.Connect）
6. 显示房间UI
```

**代码位置**：`GameManager.cs:InitializeGameFlow()`

---

### 2. TapSDKService（SDK核心服务）

**职责**：
- SDK初始化和用户授权
- 房间管理（创建、匹配、加入、离开）
- 玩家管理（进入、离开、踢出）
- 房间属性管理（battleStatus）
- 分享功能

**优化后代码量**：1795行（原2320行，减少22.6%）

**核心方法**：
```csharp
// SDK初始化
InitializeTapSDK()
LoginUser()
LoadUserInfo()

// 房间管理
StartMatchRoom()      // 匹配房间
CreateRoom()          // 创建房间
JoinSharedRoom()      // 加入分享的房间
LeaveRoom()           // 离开房间

// 房间属性
UpdateRoomPropertiesToBattle()  // 更新房间为战斗状态

// 玩家管理
HandlePlayerEnterRoom()   // 处理玩家进入
HandlePlayerLeaveRoom()   // 处理玩家离开
HandlePlayerKicked()      // 处理玩家被踢

// 发送移动（自动根据模式选择API）
SendPlayerMove(position)
```

**代码位置**：`TapSDKService.cs`

---

### 3. StateSyncManager（状态同步管理器）✨

**职责**：管理状态同步模式下的完整游戏流程

**核心原则**：
> ⚠️ SendCustomMessage的`type=0`不会触发发送者的OnCustomMessage！
> 所以发送者必须在`success`回调中本地立即执行操作。

**代码模式**：
```csharp
// 发送消息
TapBattleClient.SendCustomMessage({
    data = { eventCode: "START_GAME", ... },
    type = 0,
    success = (result) => {
        // ⚠️ 发送者在这里本地执行
        ExecuteStartBattle();
    }
});

// 接收消息
public void HandleCustomMessage(info) {
    if (eventCode == "START_GAME") {
        ExecuteStartBattle();  // 接收者也执行
    }
}
```

**公开接口**：
```csharp
// 开始游戏（房主调用）
StateSyncManager.Instance.StartGame();

// 发送移动（任意玩家）
StateSyncManager.Instance.SendMove(moveData);

// 结束游戏（任意玩家）
StateSyncManager.Instance.StopGame();

// 处理收到的消息（由EventHandler调用）
StateSyncManager.Instance.HandleCustomMessage(info);

// 处理房间属性变化（由EventHandler调用）
StateSyncManager.Instance.HandleRoomPropertiesChange(info);

// 延迟自动进入战斗（中途加入时）
StartCoroutine(StateSyncManager.Instance.AutoEnterBattleAfterDelay(2f));
```

**支持的消息类型**：
- `START_GAME`：开始游戏
- `STOP_GAME`：结束游戏
- `PLAYER_MOVE`：玩家移动

**代码位置**：`StateSyncManager.cs`（398行）

---

### 4. FrameSyncManager（帧同步管理器）✨

**职责**：管理帧同步模式下的完整游戏流程

**核心流程**：
```
1. StartBattle → OnBattleStart事件
2. SendInput → 服务器收集 → OnBattleFrame事件
3. StopBattle → OnBattleStop事件
```

**公开接口**：
```csharp
// 处理对战开始（由EventHandler调用）
FrameSyncManager.Instance.HandleBattleStart(info);

// 处理帧数据（由EventHandler调用）
FrameSyncManager.Instance.HandleBattleFrame(frameData);

// 处理对战停止（由EventHandler调用）
FrameSyncManager.Instance.HandleBattleStop(info);
```

**性能优化**：
- ✅ 快速过滤空帧（字符串长度<20）
- ✅ 只处理包含inputs的帧
- ✅ 复用已解析的帧数据

**代码位置**：`FrameSyncManager.cs`（~180行）

---

### 5. AvatarManager（头像管理器）✨

**职责**：统一管理头像下载和缓存

**设计目的**：
- 消除重复代码（原本有3处头像加载逻辑）
- 统一接口，全局缓存
- 节省带宽，提升性能

**使用方式**：
```csharp
// 加载头像（带自动缓存）
AvatarManager.Instance.LoadAvatar(url,
    (sprite) => {
        // 成功回调
        playerComponent.SetAvatarSprite(sprite);
    },
    () => {
        // 失败回调（可选）
        UseDefaultAvatar();
    }
);

// 检查缓存
if (AvatarManager.Instance.HasCachedAvatar(url)) {
    Sprite sprite = AvatarManager.Instance.GetCachedAvatar(url);
}
```

**优势**：
- ✅ 全局缓存，同一URL只下载一次
- ✅ 回调模式，使用简单
- ✅ 易于复用到其他项目

**代码位置**：`AvatarManager.cs`（~180行）

---

### 6. UIManager（UI管理器）

**职责**：管理3个UI层的显示切换

**UI层结构**：
- **GameLobbyLayer**：游戏大厅（房间列表）
- **RoomUILayer**：房间UI（玩家列表、按钮）
- **InBattleUILayer**：游戏中UI（全屏点击、玩家移动）

**核心功能**：
- 玩家列表显示和更新
- 房主权限控制（只有房主可启动游戏）
- 同步模式切换UI
- UI层切换管理

**代码位置**：`UIManager.cs`（722行）

---

### 7. BattleInteraction（对战交互）

**职责**：处理对战中的玩家交互和显示

**主要功能**：
- 全屏点击检测（IPointerClickHandler）
- 创建和管理对战玩家对象
- 处理移动数据，更新玩家位置
- 平滑移动动画（Lerp插值）
- 动态添加中途加入的玩家

**工作原理**：
```
1. 玩家点击屏幕 → 转换为本地坐标
2. 发送移动 → TapSDKService.SendPlayerMove()
3. 接收数据 → ProcessFrameData()
4. 更新目标位置
5. Update()中平滑移动
```

**代码位置**：`BattleInteraction.cs`（506行）

---

## 🎮 完整游戏流程

### 帧同步模式流程

```
【开始游戏】
房主点击"开始游戏"
  ↓
UpdateRoomProperties({ battleStatus: "fighting" })
  ↓ success
TapBattleClient.StartBattle()
  ↓
所有玩家收到 OnBattleStart 事件
  ↓
FrameSyncManager.HandleBattleStart()
  ↓
切换到游戏UI

【游戏进行】
玩家点击屏幕
  ↓
TapBattleClient.SendInput({ moveData })
  ↓
服务器收集并打包
  ↓
所有玩家收到 OnBattleFrame 事件
  ↓
FrameSyncManager.HandleBattleFrame()
  ↓
BattleInteraction.ProcessFrameData()
  ↓
应用移动

【结束游戏】
TapBattleClient.StopBattle()
  ↓
所有玩家收到 OnBattleStop 事件
  ↓
FrameSyncManager.HandleBattleStop()
  ↓
返回房间UI
```

---

### 状态同步模式流程

```
【开始游戏】
房主点击"开始游戏"
  ↓
UpdateRoomProperties({ battleStatus: "fighting" })
  ↓ success
StateSyncManager.StartGame()
  ↓
SendCustomMessage({ eventCode: "START_GAME" })
  ↓
房主A：success回调，本地立即切换UI ✅
玩家B：收到OnCustomMessage，切换UI ✅

【游戏进行】
玩家点击屏幕
  ↓
StateSyncManager.SendMove(moveData)
  ↓
SendCustomMessage({ eventCode: "PLAYER_MOVE", x, y, z })
  ↓
发送者：success回调，本地立即应用移动 ✅
其他玩家：收到OnCustomMessage，应用移动 ✅

【结束游戏】
StateSyncManager.StopGame()
  ↓
SendCustomMessage({ eventCode: "STOP_GAME" })
  ↓
发送者：success回调，本地立即返回房间UI ✅
其他玩家：收到OnCustomMessage，返回房间UI ✅

【中途加入（特有功能）】
玩家B匹配房间
  ↓
HandleRoomPlayersInfo检测 battleStatus = "fighting"
  ↓
先显示房间UI，加载头像（2秒）
  ↓
Toast提示："房间正在战斗中，即将自动进入"
  ↓
自动进入战斗界面 ✅
```

---

## 📋 数据结构说明

### RoomCustomProperties（房间自定义属性）

```csharp
public class RoomCustomProperties
{
    public string gameMode;         // 游戏模式
    public string ownerName;        // 房主名称
    public string roomName;         // 房间名称
    public string ownerAvatarUrl;   // 房主头像
    public string roomDescription;  // 房间描述
    public string battleStatus;     // ✨战斗状态（"idle" or "fighting"）
}
```

**battleStatus说明**：
- `"idle"`：空闲状态，玩家可以加入房间
- `"fighting"`：战斗中，状态同步模式下新玩家会自动进入战斗

---

### PlayerCustomProperties（玩家自定义属性）

```csharp
public class PlayerCustomProperties
{
    public string playerName;  // 玩家昵称
    public string avatarUrl;   // 头像URL
}
```

---

### MoveData（移动数据）

```csharp
public class MoveData
{
    public string action = "move";  // 操作类型
    public float x, y, z;           // 目标位置
    public long timestamp;          // 时间戳
    public string playerId;         // 玩家ID
}
```

---

## 🔧 开发者指南

### 如何添加新的状态同步消息？

**步骤1**：在StateSyncManager中添加发送方法
```csharp
public void SendAttack(AttackData attackData)
{
    SendGameControlMessage("PLAYER_ATTACK", () => {
        ExecuteAttack(attackData);  // 发送者本地执行
    }, attackData);
}
```

**步骤2**：在HandleCustomMessage中添加处理
```csharp
if (eventCode == "PLAYER_ATTACK") {
    var attackData = ParseAttackData(data);
    ExecuteAttack(attackData);  // 接收者执行
}
```

**步骤3**：实现ExecuteAttack方法
```csharp
private void ExecuteAttack(AttackData attackData) {
    // 所有客户端执行相同逻辑
    PlayAttackAnimation(attackData.playerId);
    ApplyDamage(attackData.targetId, attackData.damage);
}
```

---

### 如何使用AvatarManager？

**场景1**：在房间UI中加载玩家头像
```csharp
AvatarManager.Instance.LoadAvatar(player.avatarUrl,
    (sprite) => {
        playerComponent.SetAvatarSprite(sprite);
    },
    () => {
        // 加载失败，使用随机颜色
        playerComponent.SetAvatarColor(RandomColor());
    }
);
```

**场景2**：在战斗中加载新玩家头像
```csharp
// 先检查缓存
Sprite cachedSprite = AvatarManager.Instance.GetCachedAvatar(url);
if (cachedSprite != null) {
    // 有缓存，立即使用
    UseAvatar(cachedSprite);
} else {
    // 无缓存，异步加载
    AvatarManager.Instance.LoadAvatar(url, UseAvatar);
}
```

---

## ⚠️ 重要注意事项

### 状态同步模式（必读！）

**1. SendCustomMessage的type=0行为**
```csharp
TapBattleClient.SendCustomMessage({
    type = 0  // 发送给房间内所有人（不包括发送者）
});

// ⚠️ 发送者不会收到OnCustomMessage！
// ✅ 必须在success回调中本地执行操作
```

**2. 消息必须包含完整信息**
```csharp
// ❌ 错误：只发送操作类型
{ "action": "move" }

// ✅ 正确：包含完整状态
{
    "eventCode": "PLAYER_MOVE",
    "playerId": "xxx",
    "x": 100.5,
    "y": 50.2,
    "z": 0,
    "timestamp": 1234567890
}
```

**3. 所有客户端执行相同逻辑**
- 发送者在success回调中执行
- 接收者在HandleCustomMessage中执行
- 两者必须调用相同的方法（如ExecuteStartBattle）

---

### 帧同步模式

**1. 确定性要求**
- 所有客户端必须执行相同的计算
- 随机数必须使用确定性随机数生成器
- 使用`info.seed`创建生成器

**2. 数据类型注意**
- ⚠️ `battleId`是`int`类型（最近SDK更新）
- 不是`string`类型！

---

## 🧪 测试说明

### Unity编辑器测试（单机）
1. 打开场景运行
2. 自动初始化并显示房间UI
3. 可以测试UI流程，但无法测试真实联机

### WebGL真机测试（推荐）
1. 构建WebGL版本
2. 部署到服务器
3. 多个设备同时访问
4. 测试真实多人联机

### 测试场景

**场景1：帧同步基础流程**
1. 两台设备切换到帧同步模式
2. 匹配进入房间
3. 房主开始游戏
4. 点击移动
5. 结束游戏

**场景2：状态同步基础流程**
1. 两台设备使用状态同步模式（默认）
2. 匹配进入房间
3. 房主开始游戏
4. 点击移动
5. 结束游戏

**场景3：状态同步中途加入**
1. 设备A单独开始游戏（进入战斗）
2. 设备B匹配房间（加入设备A）
3. 设备B先显示房间UI（加载头像）
4. 2秒后Toast提示
5. 自动进入战斗界面
6. 设备A看到设备B的头像出现

---

## 📊 代码质量

### 优化成果

| 文件 | 优化前 | 优化后 | 改进 |
|------|--------|--------|------|
| TapSDKService.cs | 2320行 | 1795行 | -22.6% |
| UIManager.cs | 837行 | 722行 | -13.7% |
| BattleInteraction.cs | 542行 | 506行 | -6.6% |
| **新增** StateSyncManager | 0 | 398行 | 状态同步参考实现 |
| **新增** FrameSyncManager | 0 | ~180行 | 帧同步参考实现 |
| **新增** AvatarManager | 0 | ~180行 | 头像统一管理 |
| **删除** PlayerController | 21行 | 0 | 删除垃圾代码 |

**总代码量**：~5800行（消除重复后）

**架构改进**：
- ✅ 职责分离清晰（Manager模式）
- ✅ 消除代码重复（头像加载×3 → 1个Manager）
- ✅ 最大文件1795行（符合代码规范）
- ✅ 易于理解和维护

---

## 📖 学习路径

### 新手开发者（第一次接触多人联机）

**推荐阅读顺序**：
1. **README.md**（本文档）- 了解整体架构
2. **GameConfig.cs**（59行）- 了解同步模式概念
3. **StateSyncManager.cs**（398行）- 学习状态同步实现（推荐从这个开始）
4. **FrameSyncManager.cs**（~180行）- 学习帧同步实现
5. **TapSDKService.cs**（1795行）- 了解SDK集成细节

**为什么先学状态同步？**
- ✅ 更简单，容易理解
- ✅ 开发成本低
- ✅ 适合大多数游戏类型
- ✅ 不需要处理确定性问题

---

### 有经验的开发者

**直接查看**：
- **StateSyncManager.cs**：状态同步的完整实现和最佳实践
- **FrameSyncManager.cs**：帧同步的性能优化技巧
- **AvatarManager.cs**：网络资源管理的参考实现

**快速集成到自己项目**：
1. 复制对应的Manager文件
2. 修改业务逻辑（ExecuteXXX方法）
3. 添加自己的消息类型
4. 完成！

---

## 🐛 常见问题

### Q1：为什么状态同步模式下，发送者点击后没有移动？

**A**：忘记在`success`回调中本地执行操作。

```csharp
// ❌ 错误：只发送消息
TapBattleClient.SendCustomMessage({ data, type: 0 });

// ✅ 正确：发送后本地立即执行
TapBattleClient.SendCustomMessage({
    data, type: 0,
    success = () => {
        ApplyMove(moveData);  // 发送者本地执行
    }
});
```

---

### Q2：为什么帧同步模式下，房主开始游戏后还在房间界面？

**A**：`BattleStartInfo.battleId`类型错误。

服务器返回的是`int`类型，如果代码中定义为`string`，会导致JSON反序列化失败。

```csharp
// ❌ 错误定义
public string battleId;

// ✅ 正确定义
public int battleId;  // 与服务器返回一致
```

---

### Q3：如何在Unity编辑器中测试？

**A**：编辑器中使用模拟数据。

在`TapSDKService.LoadUserInfo()`中有编辑器模拟逻辑：
```csharp
#if UNITY_EDITOR && TAP_DEBUG_ENABLE
    playerName = $"测试玩家{randomNumber}";
    playerAvatarUrl = "";  // 使用随机颜色
#endif
```

---

### Q4：头像加载失败怎么办？

**A**：AvatarManager会自动调用失败回调。

```csharp
AvatarManager.Instance.LoadAvatar(url,
    (sprite) => { /* 成功 */ },
    () => {
        // 失败时使用随机颜色或默认头像
        playerComponent.SetAvatarColor(RandomColor());
    }
);
```

---

## 📚 参考文档

### TapSDK官方文档
- **TapMiniGameDemo.cs**：完整的TapSDK使用示例
- **Tap小游戏多人联机API参数手册.md**：API详细说明
- **Tap小游戏多人联机业务流程.md**：业务流程说明

### 项目内部文档
- **CLAUDE.md**：项目开发上下文（Claude辅助开发用）
- **CLAUDE.local.md**：本地开发配置
- **代码质量分析与优化方案.md**：代码优化过程记录
- **代码优化完成报告.md**：优化成果总结

---

## 🎉 总结

这个demo展示了TapSDK多人联机的两种同步模式：

**帧同步**：
- ✅ 适合实时对战游戏
- ✅ 服务器权威，安全性高
- ⚠️ 开发难度高，需要确定性

**状态同步**：
- ✅ 适合回合制、休闲类游戏
- ✅ 开发简单，容易理解
- ✅ 支持中途加入游戏
- ✅ **推荐新手从这个开始学习**

**代码质量**：
- ✅ 架构清晰，职责分离
- ✅ 注释详细，易于理解
- ✅ Manager模式，易于复用
- ✅ 消除重复，代码简洁

**适合人群**：
- 🎓 第一次接入TapSDK多人联机的开发者
- 📚 想了解帧同步和状态同步区别的开发者
- 🔧 需要参考代码的独立开发者

---

**最后更新**：2025-11-12
**当前版本**：v2.0（新增状态同步支持）
**维护状态**：✅ 活跃维护中
