using System;

/// <summary>
/// 玩家移动数据类 - 帧同步数据结构
///
/// 用途：
/// 1. 封装玩家移动指令数据
/// 2. 通过TapBattleClient.SendInput发送到服务器
/// 3. 通过帧同步广播给所有玩家
/// 4. 使用JsonUtility或LitJson序列化/反序列化
///
/// 注意：字段名必须与服务器约定一致
/// </summary>
[Serializable]
public class MoveData
{
    public string action;
    public float x;
    public float y;
    public float z;
    public long timestamp;
    public string playerId;
    
    public MoveData()
    {
        action = "move";
    }
    
    public MoveData(float x, float y, float z, string playerId = "", long timestamp = 0)
    {
        this.action = "move";
        this.x = x;
        this.y = y;
        this.z = z;
        this.playerId = playerId;
        this.timestamp = timestamp;
    }
}

/// <summary>
/// 帧数据中的玩家输入信息
/// </summary>
[Serializable]
public class PlayerFrameInput
{
    public string playerId;
    public string data;           // ⚠️ 服务器返回的字段名是 "data"，不是 "inputData"
    public string serverTms;      // ⚠️ 服务器返回的字段名是 "serverTms"，不是 "inputTime"
}

/// <summary>
/// 对战帧数据
/// </summary>
[Serializable]
public class BattleFrameData
{
    public int id;                // ⚠️ 服务器返回的字段名是 "id"，不是 "frameId"
    public PlayerFrameInput[] inputs;
}