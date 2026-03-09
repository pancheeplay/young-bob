using UnityEngine;

/// <summary>
/// 同步模式枚举
///
/// TapSDK多人联机支持两种同步方式：
/// 1. 帧同步（FrameSync）- 适合实时对战游戏
/// 2. 状态同步（StateSync）- 适合回合制、卡牌类游戏
/// </summary>
public enum SyncMode
{
    /// <summary>
    /// 帧同步模式
    ///
    /// 特点：
    /// - 使用StartBattle/SendInput/StopBattle API
    /// - 服务器收集所有输入并打包广播
    /// - 所有客户端根据相同输入计算结果
    /// - 需要确定性随机数保证一致性
    ///
    /// 适用场景：射击、格斗、MOBA等实时对战游戏
    /// </summary>
    FrameSync,

    /// <summary>
    /// 状态同步模式
    ///
    /// 特点：
    /// - 使用SendCustomMessage API
    /// - 消息包含完整状态信息
    /// - 所有客户端执行相同逻辑
    /// - 支持中途加入游戏
    ///
    /// 适用场景：五子棋、象棋、卡牌、回合制RPG等
    /// </summary>
    StateSync
}

/// <summary>
/// 游戏配置 - 全局同步模式管理
///
/// 职责：
/// - 管理当前使用的同步模式（帧同步 or 状态同步）
/// - 提供模式切换和查询接口
///
/// 使用示例：
/// <code>
/// // 获取当前模式
/// if (GameConfig.CurrentSyncMode == SyncMode.StateSync) { ... }
///
/// // 切换模式
/// GameConfig.SetSyncMode(SyncMode.FrameSync);
///
/// // 获取模式名称
/// string modeName = GameConfig.GetModeName();  // "帧同步" or "状态同步"
/// </code>
///
/// 重要提示：
/// - 默认使用状态同步模式（更灵活，支持中途加入）
/// - 建议在进入房间前选择模式，进入房间后不要切换
/// - 房间创建时会保存battleStatus状态，后续玩家需要跟随房间模式
/// </summary>
public static class GameConfig
{
    /// <summary>
    /// 当前同步模式（默认状态同步）
    ///
    /// 默认值说明：
    /// - 状态同步更灵活，支持更多游戏类型
    /// - 支持中途加入游戏（类似"蛇蛇大作战"）
    /// - 开发成本更低，不需要处理确定性问题
    /// </summary>
    public static SyncMode CurrentSyncMode = SyncMode.StateSync;

    /// <summary>
    /// 切换同步模式（FrameSync ⇄ StateSync）
    /// </summary>
    public static void ToggleSyncMode()
    {
        CurrentSyncMode = CurrentSyncMode == SyncMode.FrameSync
            ? SyncMode.StateSync
            : SyncMode.FrameSync;

        Debug.Log($"✅ 同步模式切换到：{GetModeName()}");
    }

    /// <summary>
    /// 获取当前模式的中文名称
    /// </summary>
    /// <returns>"帧同步" 或 "状态同步"</returns>
    public static string GetModeName()
    {
        return CurrentSyncMode == SyncMode.FrameSync ? "帧同步" : "状态同步";
    }

    /// <summary>
    /// 设置同步模式
    /// </summary>
    /// <param name="mode">要设置的模式</param>
    public static void SetSyncMode(SyncMode mode)
    {
        if (CurrentSyncMode != mode)
        {
            CurrentSyncMode = mode;
            Debug.Log($"✅ 同步模式设置为：{GetModeName()}");
        }
    }
}
