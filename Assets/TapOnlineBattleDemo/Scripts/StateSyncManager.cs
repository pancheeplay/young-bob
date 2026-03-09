using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TapTapMiniGame;
using LitJson;

/// <summary>
/// 状态同步管理器 - TapSDK状态同步模式的完整参考实现
///
/// ═══════════════════════════════════════
/// 什么是状态同步？
/// ═══════════════════════════════════════
/// 状态同步是一种简单的多人游戏同步方式：
/// - 发送完整的状态信息（如"玩家A移动到(100, 50)位置"）
/// - 所有客户端收到消息后执行相同操作
/// - 不需要复杂的确定性计算
///
/// vs 帧同步：
/// - 帧同步：只发送输入（如"玩家A按下前进键"），所有客户端自己计算
/// - 状态同步：发送结果（如"玩家A在(100, 50)位置"），所有客户端直接应用
///
/// ═══════════════════════════════════════
/// 核心设计原则（重要！）
/// ═══════════════════════════════════════
///
/// 原则1：SendCustomMessage的type=0不包括发送者
/// -----------------------------------------------
/// TapBattleClient.SendCustomMessage({ type: 0 }) 只会发送给其他玩家，
/// 发送者自己不会收到OnCustomMessage回调！
///
/// 因此：发送者必须在success回调中本地立即执行操作
///
/// 代码模式：
/// <code>
/// TapBattleClient.SendCustomMessage({
///     success = (result) => {
///         // ⚠️ 发送者在这里本地执行
///         ExecuteSomeAction();
///     }
/// });
///
/// // 其他玩家在这里执行（收到消息后）
/// public void HandleCustomMessage(info) {
///     ExecuteSomeAction();  // 复用同一方法
/// }
/// </code>
///
/// 原则2：所有客户端执行相同逻辑
/// -----------------------------------------------
/// 消息必须包含完整信息，所有客户端收到后执行相同操作。
/// 例如：发送"玩家A移动到(100, 50)"，而不是"玩家A向前移动"
///
/// 原则3：不需要StartBattle/StopBattle
/// -----------------------------------------------
/// 状态同步模式下，通过SendCustomMessage发送游戏控制消息即可，
/// 不需要调用帧同步专用的StartBattle/StopBattle API
///
/// ═══════════════════════════════════════
/// 使用示例
/// ═══════════════════════════════════════
/// <code>
/// // 开始游戏（房主调用）
/// StateSyncManager.Instance.StartGame();
///
/// // 发送移动（任意玩家）
/// StateSyncManager.Instance.SendMove(moveData);
///
/// // 结束游戏（任意玩家）
/// StateSyncManager.Instance.StopGame();
/// </code>
///
/// ═══════════════════════════════════════
/// 支持的消息类型
/// ═══════════════════════════════════════
/// 1. START_GAME - 开始游戏（房主发送，所有玩家切换到游戏UI）
/// 2. STOP_GAME - 结束游戏（任意玩家发送，所有玩家返回房间UI）
/// 3. PLAYER_MOVE - 玩家移动（包含playerId, x, y, z坐标）
///
/// 可扩展：开发者可以添加更多消息类型，如ATTACK, SKILL, CHAT等
/// </summary>
public class StateSyncManager : MonoBehaviour
{
    #region 单例模式

    private static StateSyncManager _instance;
    public static StateSyncManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<StateSyncManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("StateSyncManager");
                    _instance = go.AddComponent<StateSyncManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    #endregion

    #region 公开接口

    /// <summary>
    /// 开始游戏（房主调用）
    ///
    /// 流程：
    /// 1. 更新房间属性 battleStatus = "fighting"
    /// 2. 发送START_GAME消息给其他玩家
    /// 3. 发送者本地立即进入战斗界面
    /// </summary>
    public void StartGame()
    {
        Debug.Log("[StateSyncManager] 房主开始游戏");

        // 第一步：更新房间属性为战斗状态
        UpdateRoomPropertiesToBattle(() => {
            // 第二步：发送START_GAME消息
            SendGameControlMessage("START_GAME", ExecuteStartBattle);
        });
    }

    /// <summary>
    /// 结束游戏（任意玩家可调用）
    ///
    /// 流程：
    /// 1. 发送STOP_GAME消息给其他玩家
    /// 2. 发送者本地立即返回房间UI
    /// </summary>
    public void StopGame()
    {
        Debug.Log("[StateSyncManager] 结束游戏");
        SendGameControlMessage("STOP_GAME", ExecuteStopBattle);
    }

    /// <summary>
    /// 发送移动数据
    /// </summary>
    public void SendMove(MoveData moveData)
    {
        SendGameControlMessage("PLAYER_MOVE", () => {
            ExecuteApplyMove(moveData);
        }, moveData);
    }

    /// <summary>
    /// 处理接收到的自定义消息
    /// 由TapBattleEventHandler.OnCustomMessage调用
    /// </summary>
    public void HandleCustomMessage(CustomMessageNotification info)
    {
        Debug.Log($"[StateSyncManager] 收到自定义消息 from {info.playerId}: {info.msg}");

        try
        {
            // 解析消息
            var data = JsonMapper.ToObject(info.msg);
            string eventCode = data["eventCode"]?.ToString();

            if (eventCode == "START_GAME")
            {
                // 开始游戏消息
                Debug.Log("[StateSyncManager] 收到开始游戏消息");

                // 检查当前状态，防止重复切换
                if (TapSDKService.Instance.CurrentState == TapSDKService.GameState.InBattle)
                {
                    Debug.Log("⚠️ 已经在战斗状态，忽略START_GAME消息");
                    return;
                }

                ExecuteStartBattle();
            }
            else if (eventCode == "STOP_GAME")
            {
                // 结束游戏消息
                Debug.Log("[StateSyncManager] 收到结束游戏消息");
                ExecuteStopBattle();
            }
            else if (eventCode == "PLAYER_MOVE")
            {
                // 玩家移动消息
                var moveData = new MoveData
                {
                    playerId = data["playerId"].ToString(),
                    x = float.Parse(data["x"].ToString()),
                    y = float.Parse(data["y"].ToString()),
                    z = float.Parse(data["z"].ToString()),
                    timestamp = long.Parse(data["timestamp"].ToString())
                };

                Debug.Log($"[StateSyncManager] 收到移动消息: 玩家{moveData.playerId} → ({moveData.x:F2}, {moveData.y:F2})");
                ExecuteApplyMove(moveData);
            }
            else
            {
                Debug.LogWarning($"[StateSyncManager] 未知的消息类型: {eventCode}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[StateSyncManager] 解析自定义消息失败: {e.Message}\n消息内容: {info.msg}");
        }
    }

    /// <summary>
    /// 处理房间属性变化事件
    /// 已在房间的玩家，当房主开始游戏时会收到此事件
    ///
    /// ⚠️ 重要：只在状态同步模式下处理
    /// 帧同步模式下，应该等OnBattleStart事件，不要在这里切换UI
    /// </summary>
    public void HandleRoomPropertiesChange(RoomPropertiesNotification info)
    {
        // ✅ 只处理状态同步模式
        if (GameConfig.CurrentSyncMode != SyncMode.StateSync)
        {
            Debug.Log($"[StateSyncManager] 当前是帧同步模式，忽略房间属性变化事件（等待OnBattleStart）");
            return;
        }

        try
        {
            Debug.Log($"[StateSyncManager] 房间属性变化 - 房间ID: {info.id}");

            if (!string.IsNullOrEmpty(info.customProperties))
            {
                Debug.Log($"🔍 customProperties: {info.customProperties}");

                try
                {
                    var roomProps = JsonMapper.ToObject<TapOnlineBattleDemo.RoomCustomProperties>(info.customProperties);
                    Debug.Log($"🔍 battleStatus: {roomProps.battleStatus}");

                    // 如果房间变为战斗状态 && 当前不在战斗中
                    if (!string.IsNullOrEmpty(roomProps.battleStatus) &&
                        roomProps.battleStatus == "fighting" &&
                        TapSDKService.Instance.CurrentState != TapSDKService.GameState.InBattle)
                    {
                        Debug.Log("[StateSyncManager] 房间属性变为fighting，自动进入战斗");
                        ExecuteStartBattle();
                    }
                }
                catch (Exception parseEx)
                {
                    Debug.LogError($"[StateSyncManager] 解析customProperties失败: {parseEx.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[StateSyncManager] 处理房间属性变化失败: {e.Message}");
        }
    }

    #endregion

    #region 私有方法 - 发送消息

    /// <summary>
    /// 发送游戏控制消息（通用方法）
    ///
    /// 这是状态同步的核心方法，统一了所有SendCustomMessage的调用模式
    ///
    /// 设计模式：
    /// 1. 构造包含eventCode的消息数据
    /// 2. 调用TapBattleClient.SendCustomMessage发送
    /// 3. ⚠️ 关键：在success回调中，发送者本地立即执行操作
    /// 4. 其他玩家收到OnCustomMessage后，执行相同操作
    ///
    /// 为什么发送者要在success中执行？
    /// -----------------------------------------------
    /// SendCustomMessage的type=0只发送给其他玩家（不包括发送者），
    /// 所以发送者不会收到OnCustomMessage回调，必须本地立即执行。
    ///
    /// 数据流向：
    /// <code>
    /// 发送者（房主A）:
    ///   SendCustomMessage({ eventCode: "START_GAME" })
    ///   → success回调
    ///   → onLocalSuccess执行
    ///   → 本地切换到游戏UI ✅
    ///
    /// 接收者（玩家B）:
    ///   收到OnCustomMessage事件
    ///   → HandleCustomMessage解析eventCode
    ///   → 执行相同操作
    ///   → 切换到游戏UI ✅
    /// </code>
    ///
    /// 消息格式：
    /// {
    ///   "eventCode": "START_GAME" | "STOP_GAME" | "PLAYER_MOVE",
    ///   "senderId": "玩家ID",
    ///   "timestamp": 时间戳,
    ///   ...extraData  // PLAYER_MOVE时包含x,y,z坐标
    /// }
    /// </summary>
    /// <param name="eventCode">事件码（START_GAME, STOP_GAME, PLAYER_MOVE）</param>
    /// <param name="onLocalSuccess">发送者本地立即执行的逻辑（重要！）</param>
    /// <param name="extraData">额外数据（PLAYER_MOVE时传入MoveData）</param>
    private void SendGameControlMessage(string eventCode, Action onLocalSuccess, object extraData = null)
    {
        // 构造消息
        var messageData = new Dictionary<string, object>
        {
            { "eventCode", eventCode },
            { "senderId", TapSDKService.Instance.GetCurrentPlayerId() },
            { "timestamp", DateTimeOffset.Now.ToUnixTimeMilliseconds() }
        };

        // 如果有额外数据，合并进去
        if (extraData != null)
        {
            if (extraData is MoveData moveData)
            {
                messageData["playerId"] = moveData.playerId;
                messageData["x"] = moveData.x;
                messageData["y"] = moveData.y;
                messageData["z"] = moveData.z;
            }
        }

        string json = JsonMapper.ToJson(messageData);

        var data = new SendCustomMessageData
        {
            msg = json,
            type = 0,  // 0 = 发送给房间内所有人（不包括发送者）
            receivers = new string[0]
        };

        TapBattleClient.SendCustomMessage(new SendCustomMessageOption
        {
            data = data,
            success = (result) =>
            {
                Debug.Log($"[StateSyncManager] {eventCode} 消息发送成功");

                // ⚠️ 关键：发送者本地立即执行
                onLocalSuccess?.Invoke();
            },
            fail = (result) =>
            {
                Debug.LogError($"[StateSyncManager] {eventCode} 消息发送失败 - {result.errNo}: {result.errMsg}");
            }
        });
    }

    /// <summary>
    /// 更新房间属性为战斗状态
    /// </summary>
    private void UpdateRoomPropertiesToBattle(Action onSuccess)
    {
        Debug.Log("[StateSyncManager] 更新房间属性为战斗状态");

        TapSDKService.Instance.UpdateRoomPropertiesToBattle(onSuccess);
    }

    #endregion

    #region 私有方法 - 执行游戏状态切换

    /// <summary>
    /// 执行开始游戏的状态切换
    ///
    /// 用途：
    /// 发送者和接收者都调用这个方法，确保逻辑一致
    /// </summary>
    private void ExecuteStartBattle()
    {
        Debug.Log("[StateSyncManager] 执行开始游戏");

        // 切换状态
        TapSDKService.Instance.SetState(TapSDKService.GameState.InBattle);

        // 切换UI
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.OnBattleStart();
        }
    }

    /// <summary>
    /// 执行结束游戏的状态切换
    ///
    /// 用途：
    /// 发送者和接收者都调用这个方法，确保逻辑一致
    /// </summary>
    private void ExecuteStopBattle()
    {
        Debug.Log("[StateSyncManager] 执行结束游戏");

        // 切换状态
        TapSDKService.Instance.SetState(TapSDKService.GameState.InRoom);

        // 切换UI并刷新玩家列表
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ShowRoomUI();
            uiManager.UpdatePlayerList(TapSDKService.Instance.GetRoomPlayerList());
        }
    }

    /// <summary>
    /// 执行应用移动
    ///
    /// 用途：
    /// 发送者和接收者都调用这个方法，确保逻辑一致
    /// </summary>
    private void ExecuteApplyMove(MoveData moveData)
    {
        Debug.Log($"[StateSyncManager] 应用移动: 玩家{moveData.playerId} → ({moveData.x:F2}, {moveData.y:F2})");

        // 构造帧数据格式，复用BattleInteraction的ProcessFrameData
        BattleFrameData frameData = new BattleFrameData
        {
            id = 0,  // 状态同步不使用帧ID
            inputs = new PlayerFrameInput[]
            {
                new PlayerFrameInput
                {
                    playerId = moveData.playerId,
                    data = JsonMapper.ToJson(moveData),
                    serverTms = moveData.timestamp.ToString()
                }
            }
        };

        BattleInteraction battleInteraction = FindObjectOfType<BattleInteraction>();
        if (battleInteraction != null)
        {
            battleInteraction.ProcessFrameData(frameData);
        }
    }

    #endregion

    #region 中途加入战斗

    /// <summary>
    /// 延迟自动进入战斗（中途加入房间时）
    ///
    /// 流程：
    /// 1. 先显示房间UI，加载头像（2秒）
    /// 2. 弹出toast提示："房间正在战斗中，即将自动进入"
    /// 3. 等待0.5秒
    /// 4. 自动进入战斗界面
    /// </summary>
    public IEnumerator AutoEnterBattleAfterDelay(float delaySeconds)
    {
        Debug.Log($"[StateSyncManager] 启动自动进入战斗，延迟{delaySeconds}秒");

        // 等待头像加载
        yield return new WaitForSeconds(delaySeconds);

        // 弹出toast提示
        ShowToastOption toastOption = new ShowToastOption
        {
            title = "房间正在战斗中，即将自动进入",
            duration = 2,
            icon = "success"
        };
        Tap.ShowToast(toastOption);

        Debug.Log("[StateSyncManager] 显示toast提示");

        // 等待用户看到提示
        yield return new WaitForSeconds(0.5f);

        // 自动进入战斗
        ExecuteStartBattle();
    }

    #endregion
}
