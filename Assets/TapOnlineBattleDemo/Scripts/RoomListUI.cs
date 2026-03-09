/*
 * RoomListUI.cs
 * 用途：管理房间列表的显示、刷新和交互，负责ScrollView中房间条目的创建和管理
 */

using UnityEngine;
using UnityEngine.UI;
using TapTapMiniGame;
using LitJson;
using System.Collections.Generic;

namespace TapOnlineBattleDemo
{
    public class RoomListUI : MonoBehaviour
    {
        [Header("UI组件引用")]
        [SerializeField] private Transform contentTransform;       // ScrollView的Content容器
        [SerializeField] private GameObject roomItemPrefab;        // 房间条目预制体
        [SerializeField] private Button refreshButton;             // 刷新按钮

        [Header("房间列表配置")]
        [SerializeField] private int maxDisplayRooms = 50;         // 最多显示房间数量

        private const float ROOM_ITEM_HEIGHT = 118f;               // 房间条目高度
        private const float ROOM_ITEM_SPACING = 2f;                // 房间条目间距

        private List<GameObject> activeRoomItems = new List<GameObject>();  // 当前显示的房间条目
        private bool isLoading = false;                                      // 是否正在加载

        /// <summary>
        /// 获取当前房间列表数量
        /// </summary>
        public int GetRoomCount()
        {
            return activeRoomItems.Count;
        }

        private void Awake()
        {
            // 绑定按钮事件
            if (refreshButton != null)
            {
                refreshButton.onClick.AddListener(OnRefreshButtonClicked);
            }
        }

        private void OnDestroy()
        {
            // 解绑按钮事件
            if (refreshButton != null)
            {
                refreshButton.onClick.RemoveListener(OnRefreshButtonClicked);
            }
        }

        /// <summary>
        /// 显示房间列表面板并加载数据
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            RefreshRoomList();
        }

        /// <summary>
        /// 隐藏房间列表面板
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 刷新房间列表（调用SDK获取房间列表）
        /// </summary>
        public void RefreshRoomList()
        {
            if (isLoading)
            {
                Debug.Log("房间列表正在加载中，请稍候...");
                return;
            }

            Debug.Log("开始获取房间列表...");
            isLoading = true;

            // 禁用刷新按钮防止重复点击
            if (refreshButton != null)
            {
                refreshButton.interactable = false;
            }

            // 清空当前列表
            ClearRoomList();

            // 调用TapSDK获取房间列表
            var option = new GetRoomListOption
            {
                success = (result) =>
                {
                    Debug.Log($"✅ 获取房间列表成功，房间数量: {result.rooms?.Length ?? 0}");
                    OnRoomListReceived(result.rooms);
                },
                fail = (result) =>
                {
                    Debug.LogError($"❌ 获取房间列表失败: {result.errMsg}");
                    OnRoomListLoadFailed(result.errMsg);
                },
                complete = (result) =>
                {
                    isLoading = false;

                    // 重新启用刷新按钮
                    if (refreshButton != null)
                    {
                        refreshButton.interactable = true;
                    }
                }
            };

            TapBattleClient.GetRoomList(option);
        }

        /// <summary>
        /// 房间列表获取成功回调
        /// </summary>
        private void OnRoomListReceived(RoomListInfo[] rooms)
        {
            if (rooms == null || rooms.Length == 0)
            {
                Debug.Log("当前没有可用房间");
                UpdateContentHeight();
                return;
            }

            // 限制显示数量
            int displayCount = Mathf.Min(rooms.Length, maxDisplayRooms);

            for (int i = 0; i < displayCount; i++)
            {
                CreateRoomItem(rooms[i]);
            }

            // 更新Content高度
            UpdateContentHeight();

            Debug.Log($"✅ 已显示 {displayCount} 个房间");
        }

        /// <summary>
        /// 房间列表加载失败回调
        /// </summary>
        private void OnRoomListLoadFailed(string errorMessage)
        {
            Debug.LogError($"加载房间列表失败: {errorMessage}");
            // 可以在这里显示错误提示UI
        }

        /// <summary>
        /// 创建单个房间条目
        /// </summary>
        private void CreateRoomItem(RoomListInfo roomInfo)
        {
            if (roomItemPrefab == null || contentTransform == null)
            {
                Debug.LogError("房间条目预制体或Content容器未设置");
                return;
            }

            // RoomListInfo不包含完整players/ownerId/type，转换为RoomInfo供现有UI复用
            var fullRoomInfo = new RoomInfo
            {
                id = roomInfo.id,
                maxPlayerCount = roomInfo.maxPlayerCount,
                customProperties = roomInfo.customProperties,
                name = roomInfo.name,
                players = new PlayerInfo[roomInfo.playerCount]
            };

            // 实例化房间条目
            GameObject roomItem = Instantiate(roomItemPrefab, contentTransform);

            // 获取RoomItemUI组件并设置数据
            RoomItemUI roomItemUI = roomItem.GetComponent<RoomItemUI>();
            if (roomItemUI != null)
            {
                roomItemUI.Setup(fullRoomInfo);

                // 订阅加入房间事件
                roomItemUI.OnJoinRoomRequested += OnJoinRoomRequested;
            }
            else
            {
                Debug.LogError("房间条目预制体缺少RoomItemUI组件");
                Destroy(roomItem);
                return;
            }

            // 添加到活动列表
            activeRoomItems.Add(roomItem);
        }

        /// <summary>
        /// 清空房间列表
        /// </summary>
        public void ClearRoomList()
        {
            // 销毁所有房间条目
            foreach (GameObject item in activeRoomItems)
            {
                if (item != null)
                {
                    RoomItemUI roomItemUI = item.GetComponent<RoomItemUI>();
                    if (roomItemUI != null)
                    {
                        roomItemUI.OnJoinRoomRequested -= OnJoinRoomRequested;
                    }

                    Destroy(item);
                }
            }

            activeRoomItems.Clear();

            // 更新Content高度
            UpdateContentHeight();

            Debug.Log("已清空房间列表");
        }

        /// <summary>
        /// 更新Content高度
        /// 高度计算公式：房间数量 * (房间高度 + 间距) - 间距
        /// </summary>
        private void UpdateContentHeight()
        {
            if (contentTransform == null) return;

            int roomCount = activeRoomItems.Count;
            float totalHeight = 0f;

            if (roomCount > 0)
            {
                // 总高度 = 房间数量 * 房间高度 + (房间数量 - 1) * 间距
                totalHeight = roomCount * ROOM_ITEM_HEIGHT + (roomCount - 1) * ROOM_ITEM_SPACING;
            }

            // 设置Content的高度
            RectTransform rectTransform = contentTransform as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, totalHeight);
            }

            Debug.Log($"Content高度已更新: {totalHeight} (房间数量: {roomCount})");
        }

        /// <summary>
        /// 加入房间请求回调
        /// </summary>
        private void OnJoinRoomRequested(RoomInfo roomInfo)
        {
            Debug.Log($"请求加入房间: {roomInfo.name} (ID: {roomInfo.id})");

            // 调用TapSDK加入房间
            JoinRoom(roomInfo.id);
        }

        /// <summary>
        /// 加入指定房间
        /// </summary>
        private void JoinRoom(string roomId)
        {
            if (string.IsNullOrEmpty(roomId))
            {
                Debug.LogError("房间ID为空，无法加入");
                return;
            }

            Debug.Log($"正在加入房间: {roomId}");

            var option = new JoinRoomOption
            {
                data = new JoinRoomRequest
                {
                    roomId = roomId,
                    playerCfg = new PlayerConfig
                    {
                        customStatus = 0,
                        customProperties = JsonMapper.ToJson(new PlayerCustomProperties(
                            TapSDKService.Instance.playerName,
                            TapSDKService.Instance.playerAvatarUrl
                        ))
                    }
                },
                success = (result) =>
                {
                    Debug.Log($"✅ 成功加入房间: {result.roomInfo.name}");

                    // 切换到房间UI
                    TapSDKService.Instance.SetState(TapSDKService.GameState.InRoom);

                    // 通知UIManager更新UI（假设TapSDKService会处理RoomInfo）
                    // 这里需要调用与匹配房间相同的处理逻辑
                    HandleJoinRoomSuccess(result.roomInfo);

                    // 隐藏房间列表
                    Hide();
                },
                fail = (result) =>
                {
                    Debug.LogError($"❌ 加入房间失败: {result.errMsg}");
                },
                complete = (result) =>
                {
                    Debug.Log("加入房间操作完成");
                }
            };

            TapBattleClient.JoinRoom(option);
        }

        /// <summary>
        /// 处理加入房间成功
        /// 直接调用TapSDKService的HandleRoomPlayersInfo复用所有逻辑
        /// </summary>
        private void HandleJoinRoomSuccess(RoomInfo roomInfo)
        {
            Debug.Log("RoomListUI: 加入房间成功，调用TapSDKService.HandleRoomPlayersInfo处理");

            // ✅ 直接调用TapSDKService的公开方法
            // 这样可以复用所有逻辑，包括battleStatus检测
            if (TapSDKService.Instance != null)
            {
                TapSDKService.Instance.HandleRoomPlayersInfo(roomInfo);
            }
            else
            {
                Debug.LogError("TapSDKService.Instance is null");
            }
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private void OnRefreshButtonClicked()
        {
            Debug.Log("刷新房间列表");
            RefreshRoomList();
        }
    }
}
