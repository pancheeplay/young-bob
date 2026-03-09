using System;

/// <summary>
/// Ping测试数据类 - 用于网络延迟测试
///
/// 用途：
/// 1. 封装ping测试消息数据
/// 2. 通过TapBattleClient.SendInput发送到服务器
/// 3. 通过帧同步广播给所有玩家
/// 4. 收到自己发送的ping消息时计算往返延迟(RTT)
/// 5. 使用JsonUtility或LitJson序列化/反序列化
///
/// 注意：字段名必须与服务器约定一致
/// </summary>
[Serializable]
public class PingData
{
    public string action;       // 动作类型，固定为 "ping"
    public int pingId;          // ping消息的唯一编号（自增）
    public long timestamp;      // 发送时的Unix毫秒时间戳
    public string senderId;     // 发送者的playerId
    
    public PingData()
    {
        action = "ping";
    }
    
    public PingData(int pingId, long timestamp, string senderId)
    {
        this.action = "ping";
        this.pingId = pingId;
        this.timestamp = timestamp;
        this.senderId = senderId;
    }
}

