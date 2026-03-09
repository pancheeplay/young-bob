# 本地 Debug Relay 使用说明

## 目标

这套调试链路用于避免频繁打包 TapTap。

适用场景：

- Unity Editor 本地联调
- 两个开发者各自运行 Unity 工程
- 先验证房间、消息、战斗流程

当前不替代 TapTap 真机验证。

## 结构

服务端：

- `Tools/DebugRelayServer/server.mjs`

Unity 客户端 transport：

- `Assets/Scripts/YoungBob/Prototype/Multiplayer/DebugRelayMultiplayerService.cs`

切换入口：

- `Assets/Scripts/YoungBob/Prototype/Scene/YoungBobGameManager.cs`

## 启动服务端

在项目根目录执行：

```bash
cd Tools/DebugRelayServer
npm install
npm start
```

默认监听：

```text
ws://127.0.0.1:8787
```

如果你希望局域网设备访问，可以把 Unity 里的地址改成你电脑的局域网 IP，例如：

```text
ws://192.168.1.10:8787
```

## Unity 侧配置

在场景中的 `YoungBobManagers` 对象上：

- `Transport Mode` 设为 `DebugRelay`
- `Debug Relay Url` 设为本机或局域网可访问地址

默认值：

```text
ws://127.0.0.1:8787
```

## 当前支持的能力

- Connect
- Disconnect
- CreateRoom
- MatchRoom
- GetRoomList
- JoinRoom
- LeaveRoom
- Room custom message relay

这已经足够支持当前原型：

- 房间流程
- 战斗开始
- 出牌
- 结束回合
- 战斗结束

## 当前限制

- 服务端只做内存房间
- 不做持久化
- 不做鉴权
- 重启服务后房间全部消失
- 主要用于开发调试，不用于正式环境

## 后续升级方向

如果以后要做真正的公网联调：

1. 把这个 Node 服务部署到云主机
2. 把 `Debug Relay Url` 改成公网地址
3. 增加日志、鉴权、超时清理、重连支持

