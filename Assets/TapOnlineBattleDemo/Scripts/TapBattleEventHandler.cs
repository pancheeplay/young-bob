using System;
using UnityEngine;
using TapTapMiniGame;

/// <summary>
/// Tap多人联机事件处理器
/// </summary>
public class TapBattleEventHandler : ITapBattleEventHandler
{
    public void OnDisconnected(DisconnectedInfo info)
    {
        Debug.Log($"🔌 多人联机连接断开: {info.reason} (代码: {info.code})");
    }

    public void OnBattleServiceError(BattleServiceErrorInfo info)
    {
        Debug.LogError($"❌ 多人联机服务错误: {info.errorMessage} (代码: {info.errorCode})");
    }

    public void OnRoomPropertiesChange(RoomPropertiesNotification info)
    {
        Debug.Log($"🏠 房间属性变化: 房间ID={info.id}, 房间名={info.name}");

        // 转发到StateSyncManager处理（状态同步需要检测battleStatus变化）
        StateSyncManager.Instance?.HandleRoomPropertiesChange(info);
    }

    public void OnPlayerCustomPropertiesChange(PlayerCustomPropertiesNotification info)
    {
        Debug.Log($"👤 玩家属性变化: 玩家ID={info.playerId}");
    }

    public void OnPlayerCustomStatusChange(PlayerCustomStatusNotification info)
    {
        Debug.Log($"⚡ 玩家状态变化: 玩家ID={info.playerId}, 状态={info.status}");
    }

    public void OnFrameSyncStop(FrameSyncStopInfo info)
    {
        Debug.Log($"🛑 对战停止事件触发: 房间ID={info.roomId}, 对战ID={info.battleId}, 原因={info.reason}");

        // 转发到FrameSyncManager处理（帧同步专用）
        FrameSyncManager.Instance?.HandleBattleStop(info);
    }

    public void OnFrameInput(string frameData)
    {
        // 转发到FrameSyncManager处理（帧同步专用）
        FrameSyncManager.Instance?.HandleBattleFrame(frameData);
    }

    public void OnFrameSyncStart(FrameSyncStartInfo info)
    {
        string roomId = info.roomInfo != null ? info.roomInfo.id : "";
        Debug.Log($"▶️ 对战开始事件触发: 房间ID={roomId}, 对战ID={info.battleId}, 随机种子={info.seed}");
        Debug.Log($"所有玩家（包括房主和非房主）都会收到这个事件");

        // 转发到FrameSyncManager处理（帧同步专用）
        FrameSyncManager.Instance?.HandleBattleStart(info);
    }

    public void OnPlayerOffline(PlayerOfflineNotification info)
    {
        Debug.Log($"📱 玩家离线: {info.playerId}");
    }

    public void OnPlayerLeaveRoom(LeaveRoomNotification info)
    {
        Debug.Log($"🚪 玩家离开房间: {info.playerId}");

        // 通过单例调用TapSDKService的玩家离开处理
        TapSDKService.Instance?.HandlePlayerLeaveRoom(info);
    }

    public void OnPlayerEnterRoom(EnterRoomNotification info)
    {
        Debug.Log($"🚪 玩家进入房间: {info.playerInfo.id}");

        // 通过单例调用TapSDKService的玩家进入处理
        TapSDKService.Instance?.HandlePlayerEnterRoom(info);
    }

    public void OnCustomMessage(CustomMessageNotification info)
    {
        Debug.Log($"💬 自定义消息: 来自{info.playerId}, 内容={info.msg}");

        // 转发到StateSyncManager处理（状态同步专用）
        StateSyncManager.Instance?.HandleCustomMessage(info);
    }

    public void OnPlayerKicked(PlayerKickedInfo info)
    {
        Debug.Log($"👢 玩家被踢: {info.playerId}");

        // 通过单例调用TapSDKService的玩家被踢处理
        TapSDKService.Instance?.HandlePlayerKicked(info);
    }
}
