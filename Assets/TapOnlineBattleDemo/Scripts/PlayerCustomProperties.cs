using System;

/// <summary>
/// 玩家自定义属性数据类
///
/// 用途：
/// 1. 存储在PlayerConfig.customProperties字段中（匹配房间时）
/// 2. 从RoomInfo.players[].customProperties中解析（获取房间玩家信息时）
/// 3. 包含玩家昵称和头像URL，用于UI显示
///
/// 使用LitJson序列化/反序列化
/// </summary>
[Serializable]
public class PlayerCustomProperties
{
    public string playerName;
    public string avatarUrl;
    
    public PlayerCustomProperties()
    {
    }
    
    public PlayerCustomProperties(string playerName, string avatarUrl)
    {
        this.playerName = playerName;
        this.avatarUrl = avatarUrl;
    }
}