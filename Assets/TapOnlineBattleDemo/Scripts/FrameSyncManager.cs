using System;
using UnityEngine;
using TapTapMiniGame;

/// <summary>
/// 帧同步管理器 - TapSDK帧同步模式的完整参考实现
///
/// ═══════════════════════════════════════
/// 什么是帧同步？
/// ═══════════════════════════════════════
/// 帧同步是一种严格的多人游戏同步方式：
/// - 只发送输入指令（如"按下前进键"）
/// - 服务器收集所有输入并打包成"帧"
/// - 所有客户端收到相同的帧数据
/// - 所有客户端执行相同的计算逻辑
/// - 使用确定性随机数保证结果一致
///
/// vs 状态同步：
/// - 帧同步：只发输入，客户端自己计算（需要确定性）
/// - 状态同步：发送结果，客户端直接应用（不需要确定性）
///
/// ═══════════════════════════════════════
/// 核心API流程
/// ═══════════════════════════════════════
///
/// 开始游戏流程：
/// <code>
/// 1. UpdateRoomProperties({ battleStatus: "fighting" })
/// 2. TapBattleClient.StartBattle()
/// 3. 所有玩家收到OnBattleStart事件
/// 4. HandleBattleStart() 处理事件
/// 5. 切换到游戏UI
/// </code>
///
/// 游戏进行中：
/// <code>
/// 1. 玩家操作 → TapBattleClient.SendInput({ 输入数据 })
/// 2. 服务器收集所有输入
/// 3. 服务器打包成帧 → OnBattleFrame事件
/// 4. HandleBattleFrame() 处理帧数据
/// 5. 应用所有玩家的输入
/// </code>
///
/// 结束游戏流程：
/// <code>
/// 1. TapBattleClient.StopBattle()
/// 2. 所有玩家收到OnBattleStop事件
/// 3. HandleBattleStop() 处理事件
/// 4. 返回房间UI
/// </code>
///
/// ═══════════════════════════════════════
/// 关键特点
/// ═══════════════════════════════════════
/// - ✅ 所有玩家（包括发送者）都会收到OnBattleFrame
/// - ✅ 服务器权威，难以作弊
/// - ✅ 适合高频率操作的实时游戏
/// - ⚠️ 需要确定性随机数（使用seed）
/// - ⚠️ 需要严格的逻辑一致性
///
/// 本Demo中的使用：
/// - OnBattleStart → FrameSyncManager.HandleBattleStart
/// - OnBattleFrame → FrameSyncManager.HandleBattleFrame
/// - OnBattleStop → FrameSyncManager.HandleBattleStop
/// </summary>
public class FrameSyncManager : MonoBehaviour
{
    #region 单例模式

    private static FrameSyncManager _instance;
    public static FrameSyncManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<FrameSyncManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("FrameSyncManager");
                    _instance = go.AddComponent<FrameSyncManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 处理对战开始事件
    /// 由TapBattleEventHandler.OnBattleStart调用
    /// </summary>
    public void HandleBattleStart(FrameSyncStartInfo info)
    {
        string roomId = info.roomInfo != null ? info.roomInfo.id : "";
        Debug.Log($"[FrameSyncManager] 对战开始 - 房间ID:{roomId}, 种子:{info.seed}");

        // 切换状态
        TapSDKService.Instance.SetState(TapSDKService.GameState.InBattle);

        // 启动网络延迟测试
        NetworkLatencyStats.Instance.StartPingTest();
        Debug.Log("✅ 网络延迟测试已启动");

        // 切换UI
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.OnBattleStart();
        }
    }

    /// <summary>
    /// 处理对战帧数据
    /// 由TapBattleEventHandler.OnBattleFrame调用
    ///
    /// 优化：
    /// 1. 快速过滤空帧（字符串长度<20）
    /// 2. 只处理包含inputs的帧
    /// 3. 委托给BattleInteraction处理
    /// </summary>
    public void HandleBattleFrame(string frameData)
    {
        // 检查有效性
        if (string.IsNullOrEmpty(frameData))
        {
            return;
        }

        try
        {
            // 解析帧ID并统计
            BattleFrameData frameInfo = JsonUtility.FromJson<BattleFrameData>(frameData);
            if (frameInfo != null)
            {
                FrameRateStats.Instance?.RecordFrame(frameInfo.id);
            }
            else
            {
                FrameRateStats.Instance?.RecordFrame();
            }

            // 快速过滤空帧
            if (frameData.Length < 20)
            {
                return;
            }

            // 检查是否包含inputs
            if (!frameData.Contains("\"inputs\""))
            {
                return;
            }

            // 委托给BattleInteraction处理
            BattleInteraction battleInteraction = FindObjectOfType<BattleInteraction>();
            if (battleInteraction != null && frameInfo != null)
            {
                battleInteraction.ProcessFrameData(frameInfo);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[FrameSyncManager] 帧数据处理异常: {e.Message}");
        }
    }

    /// <summary>
    /// 处理对战停止事件
    /// 由TapBattleEventHandler.OnBattleStop调用
    /// </summary>
    public void HandleBattleStop(FrameSyncStopInfo info)
    {
        Debug.Log($"[FrameSyncManager] 对战停止 - 原因:{info.reason}");

        // 停止网络延迟测试
        NetworkLatencyStats.Instance.StopPingTest();
        Debug.Log("✅ 网络延迟测试已停止");

        // 切换状态
        TapSDKService.Instance.SetState(TapSDKService.GameState.InRoom);

        // 返回房间UI并刷新玩家列表
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ShowRoomUI();
            uiManager.UpdatePlayerList(TapSDKService.Instance.GetRoomPlayerList());
            Debug.Log("✅ 已返回房间UI");
        }
    }

    #endregion
}
