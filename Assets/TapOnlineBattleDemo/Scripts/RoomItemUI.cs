/*
 * RoomItemUI.cs
 * 用途：控制单个房间条目的显示和交互，包括房间名称、房主信息、人数显示等
 */

using UnityEngine;
using UnityEngine.UI;
using TapTapMiniGame;
using LitJson;
using System;

namespace TapOnlineBattleDemo
{
    public class RoomItemUI : MonoBehaviour
    {
        [Header("UI组件引用")]
        [SerializeField] private Text roomNameText;
        [SerializeField] private Text ownerNameText;
        [SerializeField] private RawImage ownerAvatar;
        [SerializeField] private Text playerCountText;
        [SerializeField] private Text roomDetailsText;
        [SerializeField] private Button joinButton;

        private RoomInfo roomInfo;

        public event Action<RoomInfo> OnJoinRoomRequested;

        private void Awake()
        {
            if (joinButton != null)
            {
                joinButton.onClick.AddListener(OnJoinButtonClicked);
            }
        }

        private void OnDestroy()
        {
            if (joinButton != null)
            {
                joinButton.onClick.RemoveListener(OnJoinButtonClicked);
            }
        }

        /// <summary>
        /// 设置房间数据并刷新UI显示
        /// </summary>
        public void Setup(RoomInfo roomInfo)
        {
            this.roomInfo = roomInfo;
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (roomInfo == null) return;

            // 解析房间自定义属性
            RoomCustomProperties roomCustomProps = ParseRoomCustomProperties();

            // 房间名称（优先使用customProperties中的roomName）
            if (roomNameText != null)
            {
                string displayName = roomCustomProps?.roomName ?? roomInfo.name ?? "未命名房间";
                roomNameText.text = displayName;
            }

            // 房主名称（优先使用customProperties中的ownerName）
            if (ownerNameText != null)
            {
                string ownerName = roomCustomProps?.ownerName ?? GetOwnerName();
                ownerNameText.text = $"房主: {ownerName}";
            }

            // 房间人数
            if (playerCountText != null)
            {
                int currentCount = roomInfo.players?.Length ?? 0;
                int maxCount = roomInfo.maxPlayerCount;
                playerCountText.text = $"{currentCount}/{maxCount}";

                // 如果房间已满，改变颜色提示
                playerCountText.color = (currentCount >= maxCount) ? Color.red : Color.white;
            }

            // 房间详细信息（优先显示roomDescription）
            if (roomDetailsText != null)
            {
                string details = roomCustomProps?.roomDescription ?? roomInfo.type ?? "";
                roomDetailsText.text = details;
            }

            // 加入按钮状态
            if (joinButton != null)
            {
                int currentCount = roomInfo.players?.Length ?? 0;
                bool canJoin = currentCount < roomInfo.maxPlayerCount;
                joinButton.interactable = canJoin;
            }

            // 加载房主头像（优先使用customProperties中的ownerAvatarUrl）
            LoadOwnerAvatar(roomCustomProps);
        }

        /// <summary>
        /// 解析房间自定义属性
        /// </summary>
        private RoomCustomProperties ParseRoomCustomProperties()
        {
            if (roomInfo == null || string.IsNullOrEmpty(roomInfo.customProperties))
            {
                return null;
            }

            try
            {
                var customProps = JsonMapper.ToObject<RoomCustomProperties>(roomInfo.customProperties);
                return customProps;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"解析房间customProperties失败: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从房间玩家列表中获取房主名称
        /// </summary>
        private string GetOwnerName()
        {
            if (roomInfo.players == null || roomInfo.players.Length == 0)
            {
                return "未知";
            }

            // 查找房主
            foreach (var player in roomInfo.players)
            {
                if (player.id == roomInfo.ownerId)
                {
                    // 从customProperties中解析玩家名称
                    if (!string.IsNullOrEmpty(player.customProperties))
                    {
                        try
                        {
                            var customProps = JsonMapper.ToObject<PlayerCustomProperties>(player.customProperties);
                            return customProps.playerName ?? "未知";
                        }
                        catch
                        {
                            return "未知";
                        }
                    }
                }
            }

            return "未知";
        }

        /// <summary>
        /// 获取房主头像URL
        /// </summary>
        private string GetOwnerAvatarUrl()
        {
            if (roomInfo.players == null || roomInfo.players.Length == 0)
            {
                return "";
            }

            // 查找房主
            foreach (var player in roomInfo.players)
            {
                if (player.id == roomInfo.ownerId)
                {
                    // 从customProperties中解析头像URL
                    if (!string.IsNullOrEmpty(player.customProperties))
                    {
                        try
                        {
                            var customProps = JsonMapper.ToObject<PlayerCustomProperties>(player.customProperties);
                            return customProps.avatarUrl ?? "";
                        }
                        catch
                        {
                            return "";
                        }
                    }
                }
            }

            return "";
        }

        private void LoadOwnerAvatar(RoomCustomProperties roomCustomProps)
        {
            if (ownerAvatar == null) return;

            // 优先使用customProperties中的ownerAvatarUrl
            string avatarUrl = roomCustomProps?.ownerAvatarUrl;

            // 如果customProperties中没有，尝试从玩家列表中获取
            if (string.IsNullOrEmpty(avatarUrl))
            {
                avatarUrl = GetOwnerAvatarUrl();
            }

            // 如果有房主头像URL，加载头像
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                // 使用AvatarManager加载头像
                AvatarManager.Instance.LoadAvatar(avatarUrl,
                    (sprite) => {
                        // 加载成功，RawImage使用texture
                        if (ownerAvatar != null && sprite != null)
                        {
                            ownerAvatar.texture = sprite.texture;
                            ownerAvatar.color = Color.white;
                        }
                    },
                    () => {
                        // 加载失败，使用默认头像
                        if (ownerAvatar != null)
                        {
                            ownerAvatar.color = Color.gray;
                        }
                    }
                );
            }
            else
            {
                // 使用默认头像
                ownerAvatar.color = Color.gray;
            }
        }

        private void OnJoinButtonClicked()
        {
            if (roomInfo != null)
            {
                OnJoinRoomRequested?.Invoke(roomInfo);
            }
        }
    }
}