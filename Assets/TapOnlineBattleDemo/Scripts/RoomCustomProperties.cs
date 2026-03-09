/*
 * RoomCustomProperties.cs
 * 用途：封装房间的自定义属性，用于创建/匹配房间时传递，以及从房间列表中解析
 */

using System;

namespace TapOnlineBattleDemo
{
    [Serializable]
    public class RoomCustomProperties
    {
        /// <summary>
        /// 游戏模式
        /// </summary>
        public string gameMode;

        /// <summary>
        /// 房主名称
        /// </summary>
        public string ownerName;

        /// <summary>
        /// 房间名称
        /// </summary>
        public string roomName;

        /// <summary>
        /// 房主头像URL
        /// </summary>
        public string ownerAvatarUrl;

        /// <summary>
        /// 房间描述
        /// </summary>
        public string roomDescription;

        /// <summary>
        /// 战斗状态
        /// "idle" - 空闲（可加入）
        /// "fighting" - 战斗中
        /// </summary>
        public string battleStatus;

        public RoomCustomProperties()
        {
            battleStatus = "idle";  // 默认空闲状态
        }

        public RoomCustomProperties(string gameMode, string ownerName, string roomName, string ownerAvatarUrl, string roomDescription, string battleStatus = "idle")
        {
            this.gameMode = gameMode;
            this.ownerName = ownerName;
            this.roomName = roomName;
            this.ownerAvatarUrl = ownerAvatarUrl;
            this.roomDescription = roomDescription;
            this.battleStatus = battleStatus;
        }
    }
}
