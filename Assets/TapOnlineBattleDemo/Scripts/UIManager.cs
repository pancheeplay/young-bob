using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TapTapMiniGame;

[System.Serializable]
public class PlayerDisplayInfo
{
    public string playerName;
    public string avatarUrl;
    public string playerId;
    public bool isOwner;
    
    public PlayerDisplayInfo(string name, string url, string id = "", bool owner = false)
    {
        playerName = name;
        avatarUrl = url;
        playerId = id;
        isOwner = owner;
    }
}

/// <summary>
/// UI管理器
/// 功能：管理3个UI层的显示切换
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("UI层")]
    [SerializeField] private GameObject gameLobbyLayer;
    [SerializeField] private GameObject roomUILayer;
    [SerializeField] private GameObject inBattleUILayer;

    [Header("GameLobby层")]
    [SerializeField] private TapOnlineBattleDemo.RoomListUI roomListUI;  // 房间列表UI组件
    [SerializeField] private Button disconnectButton;  // 断开连接按钮

    [Header("RoomUI层")]
    [SerializeField] private RectTransform playerListRoot;  // 当前房间玩家列表容器
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button matchRoomButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveRoomButton;  // 离开房间按钮
    [SerializeField] private Button shareRoomButton;  // 分享房间按钮
    [SerializeField] private Button closeButton;  // 关闭按钮，断开连接并退出游戏

    [Header("同步模式切换")]
    [SerializeField] private Toggle syncModeToggle;  // 同步模式切换Toggle
    [SerializeField] private Text syncModeText;  // 同步模式显示文本
    
    [Header("InBattleUI")]
    [SerializeField] private PlayerComponent playerPrefab; // player prefab 用来显示当前房间所有玩家的prefab
    [SerializeField] private BattleInteraction battleInteraction; // 对战交互脚本（挂载在inBattleUILayer上）

    private List<PlayerComponent> currentPlayerList = new List<PlayerComponent>();

    // 当前房间玩家信息（用于开始游戏时传递给BattleController）
    private List<PlayerDisplayInfo> currentRoomPlayers = new List<PlayerDisplayInfo>();

    // 自动刷新房间列表的协程
    private Coroutine autoRefreshCoroutine = null;
    
    private void Start()
    {
        // 初始化按钮事件
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);

        if (matchRoomButton != null)
            matchRoomButton.onClick.AddListener(OnMatchRoomClicked);

        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);

        if (leaveRoomButton != null)
            leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);

        if (shareRoomButton != null)
            shareRoomButton.onClick.AddListener(OnShareRoomClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseButtonClicked);

        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(OnDisconnectButtonClicked);

        // 初始化同步模式切换
        if (syncModeToggle != null)
        {
            syncModeToggle.onValueChanged.AddListener(OnSyncModeToggled);
        }

        // 更新同步模式UI显示
        UpdateSyncModeUI();

        // 初始隐藏所有UI层，等待SDK初始化完成
        HideAllLayers();
    }
    
    /// <summary>
    /// 显示游戏大厅UI
    /// </summary>
    public void ShowGameLobby()
    {
        SetActiveLayer(gameLobbyLayer);

        // 确保roomListUI可见
        if (roomListUI != null && roomListUI.gameObject != null)
        {
            roomListUI.gameObject.SetActive(true);
        }

        // 重置房间UI按钮状态（显示创建房间和匹配房间按钮，隐藏开始游戏按钮）
        if (createRoomButton != null) createRoomButton.gameObject.SetActive(true);
        if (matchRoomButton != null) matchRoomButton.gameObject.SetActive(true);
        if (startGameButton != null) startGameButton.gameObject.SetActive(false);

        // 清空房间玩家列表
        ClearPlayerList();

        // 启动自动刷新（内部会判断是否已启动）
        StartAutoRefresh();
    }
    
    /// <summary>
    /// 显示房间UI
    /// </summary>
    public void ShowRoomUI()
    {
        SetActiveLayer(roomUILayer);

        // 停止自动刷新
        StopAutoRefresh();
    }
    
    /// <summary>
    /// 显示游戏中UI
    /// </summary>
    public void ShowInBattleUI()
    {
        SetActiveLayer(inBattleUILayer);

        // 停止自动刷新
        StopAutoRefresh();
    }

    /// <summary>
    /// 启动自动刷新房间列表（防止重复启动）
    /// </summary>
    private void StartAutoRefresh()
    {
        // 如果协程已在运行，不重复启动
        if (autoRefreshCoroutine != null)
        {
            return;
        }

        // 立即刷新一次
        if (roomListUI != null)
        {
            roomListUI.RefreshRoomList();
        }

        // 启动定时刷新协程
        autoRefreshCoroutine = StartCoroutine(AutoRefreshRoomList());
    }

    /// <summary>
    /// 停止自动刷新房间列表
    /// </summary>
    private void StopAutoRefresh()
    {
        if (autoRefreshCoroutine != null)
        {
            StopCoroutine(autoRefreshCoroutine);
            autoRefreshCoroutine = null;
        }
    }

    /// <summary>
    /// 自动刷新房间列表协程（每0.5秒检查一次）
    /// 策略：只有在gameLobbyLayer且房间列表为空时才执行刷新
    /// </summary>
    private IEnumerator AutoRefreshRoomList()
    {
        while (true)
        {
            // 等待0.5秒
            yield return new WaitForSeconds(0.5f);

            // 检查是否在gameLobbyLayer且房间列表为空
            if (gameLobbyLayer != null && gameLobbyLayer.activeSelf && roomListUI != null)
            {
                int roomCount = roomListUI.GetRoomCount();

                // 只有房间列表为空时才刷新
                if (roomCount == 0)
                {
                    roomListUI.RefreshRoomList();
                }
            }
        }
    }
    
    /// <summary>
    /// 设置激活的UI层
    /// </summary>
    private void SetActiveLayer(GameObject activeLayer)
    {
        if (gameLobbyLayer != null) gameLobbyLayer.SetActive(false);
        if (roomUILayer != null) roomUILayer.SetActive(false);
        if (inBattleUILayer != null) inBattleUILayer.SetActive(false);
        
        if (activeLayer != null) activeLayer.SetActive(true);
    }
    
    /// <summary>
    /// 隐藏所有UI层
    /// </summary>
    private void HideAllLayers()
    {
        if (gameLobbyLayer != null) gameLobbyLayer.SetActive(false);
        if (roomUILayer != null) roomUILayer.SetActive(false);
        if (inBattleUILayer != null) inBattleUILayer.SetActive(false);
    }
    
    /// <summary>
    /// SDK初始化完成后显示游戏大厅
    /// </summary>
    public void OnSDKInitialized()
    {
        ShowGameLobby();
    }
    
    /// <summary>
    /// 匹配成功后的处理
    /// </summary>
    public void OnMatchRoomSuccess()
    {
        // 隐藏房间按钮，显示开始游戏按钮
        if (createRoomButton != null) createRoomButton.gameObject.SetActive(false);
        if (matchRoomButton != null) matchRoomButton.gameObject.SetActive(false);
        if (startGameButton != null) startGameButton.gameObject.SetActive(true);
        
        // 注意：开始游戏按钮的启用状态将在 UpdatePlayerList 中根据当前用户是否是房主来设置
    }
    
    /// <summary>
    /// 更新房间玩家列表
    /// </summary>
    /// <param name="playerInfos">玩家信息列表</param>
    public void UpdatePlayerList(List<PlayerDisplayInfo> playerInfos)
    {
        // 清理现有玩家列表
        ClearPlayerList();
        
        bool currentUserIsOwner = false;

        // ✅ 正确：通过playerId判断当前用户是否是房主
        // playerId是Connect时服务器返回的全局唯一ID
        if (TapSDKService.Instance != null)
        {
            string myPlayerId = TapSDKService.Instance.GetCurrentPlayerId();

            foreach (PlayerDisplayInfo playerInfo in playerInfos)
            {
                // 通过playerId匹配当前用户
                if (playerInfo.playerId == myPlayerId && playerInfo.isOwner)
                {
                    currentUserIsOwner = true;
                    Debug.Log($"✅ 当前用户 (playerId:{myPlayerId}) 是房主");
                    break;
                }
            }

            if (!currentUserIsOwner)
            {
                Debug.Log($"❌ 当前用户 (playerId:{myPlayerId}) 不是房主");
            }
        }
        
        // 创建新的玩家组件
        if (playerPrefab != null && playerListRoot != null)
        {
            // 保存原始尺寸 (260x260)
            Vector2 originalSize = new Vector2(180, 180);
            
            foreach (PlayerDisplayInfo playerInfo in playerInfos)
            {
                PlayerComponent newPlayer = Instantiate(playerPrefab, playerListRoot);
                
                // 保持原始尺寸，防止Layout Group压缩
                RectTransform playerRect = newPlayer.GetComponent<RectTransform>();
                if (playerRect != null)
                {
                    
                    // 确保缩放为1
                    playerRect.localScale = Vector3.one;
                    
                    // 设置尺寸
                    playerRect.sizeDelta = originalSize;
                    
                    // 添加或确保有LayoutElement组件来覆盖Layout Group的控制
                    UnityEngine.UI.LayoutElement layoutElement = newPlayer.GetComponent<UnityEngine.UI.LayoutElement>();
                    if (layoutElement == null)
                    {
                        layoutElement = newPlayer.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                    }
                    
                    // 设置首选尺寸，强制覆盖Layout Group
                    layoutElement.preferredWidth = originalSize.x;
                    layoutElement.preferredHeight = originalSize.y;
                    layoutElement.minWidth = originalSize.x;
                    layoutElement.minHeight = originalSize.y;
                    
                    Debug.Log($"设置玩家组件尺寸: {originalSize}, 实际尺寸: {playerRect.sizeDelta}");
                }
                
                // 设置房间列表模式（放大2.6倍）
                // newPlayer.SetRoomListMode();

                // 设置玩家信息，包含playerId和房主权限状态
                newPlayer.SetPlayerInfo(
                    playerInfo.playerName,
                    null,
                    playerInfo.isOwner,
                    playerInfo.playerId,
                    currentUserIsOwner
                );

                // 使用AvatarManager加载头像
                if (!string.IsNullOrEmpty(playerInfo.avatarUrl))
                {
                    AvatarManager.Instance.LoadAvatar(playerInfo.avatarUrl,
                        (sprite) => {
                            if (newPlayer != null)
                            {
                                newPlayer.SetAvatarSprite(sprite);
                            }
                        },
                        () => {
                            // 加载失败，使用随机颜色
                            SetRandomAvatarColor(newPlayer);
                        }
                    );
                }

                currentPlayerList.Add(newPlayer);
                
                // 延迟一帧再次设置尺寸，确保Layout Group不会覆盖
                StartCoroutine(DelaySetPlayerSize(newPlayer, originalSize));
            }
        }
        
        // 根据当前用户是否是房主来设置开始游戏按钮状态
        UpdateStartGameButtonState(currentUserIsOwner);
        
        // 保存当前房间玩家信息
        currentRoomPlayers.Clear();
        currentRoomPlayers.AddRange(playerInfos);
    }
    
    /// <summary>
    /// 更新开始游戏按钮状态
    ///
    /// 权限控制逻辑：
    /// - 房主：启用按钮，白色显示
    /// - 非房主：禁用按钮，灰色显示
    /// </summary>
    /// <param name="isCurrentUserOwner">当前用户是否为房主</param>
    private void UpdateStartGameButtonState(bool isCurrentUserOwner)
    {
        if (startGameButton != null)
        {
            // 只有房主才能启用开始游戏按钮
            startGameButton.interactable = isCurrentUserOwner;
            
            // 根据状态改变按钮颜色
            var colors = startGameButton.colors;
            if (isCurrentUserOwner)
            {
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
                Debug.Log("开始游戏按钮已启用 - 当前用户是房主");
            }
            else
            {
                colors.normalColor = Color.gray;
                colors.highlightedColor = Color.gray;
                Debug.Log("开始游戏按钮已禁用 - 当前用户不是房主");
            }
            startGameButton.colors = colors;
        }
    }
    
    
    /// <summary>
    /// 设置随机头像颜色（作为备用方案）
    /// </summary>
    private void SetRandomAvatarColor(PlayerComponent player)
    {
        if (player != null)
        {
            Color randomColor = new Color(
                UnityEngine.Random.Range(0.3f, 1f),
                UnityEngine.Random.Range(0.3f, 1f),
                UnityEngine.Random.Range(0.3f, 1f)
            );
            player.SetAvatarColor(randomColor);
            Debug.Log("使用随机颜色作为头像备用方案");
        }
    }
    
    /// <summary>
    /// 延迟设置玩家组件尺寸（确保不被Layout Group覆盖）
    /// </summary>
    private IEnumerator DelaySetPlayerSize(PlayerComponent player, Vector2 targetSize)
    {
        // 等待一帧，让Layout Group完成初始布局
        yield return null;
        
        if (player != null)
        {
            RectTransform playerRect = player.GetComponent<RectTransform>();
            if (playerRect != null)
            {
                playerRect.sizeDelta = targetSize;
                Debug.Log($"延迟设置玩家组件尺寸完成: {targetSize}");
            }
        }
    }
    
    /// <summary>
    /// 清理玩家列表
    /// </summary>
    private void ClearPlayerList()
    {
        foreach (PlayerComponent player in currentPlayerList)
        {
            if (player != null)
            {
                Destroy(player.gameObject);
            }
        }
        currentPlayerList.Clear();
    }
    
    /// <summary>
    /// 获取缓存的头像Sprite
    /// 委托给AvatarManager处理
    /// </summary>
    public Sprite GetCachedAvatar(string avatarUrl)
    {
        return AvatarManager.Instance.GetCachedAvatar(avatarUrl);
    }
    
    /// <summary>
    /// 组件销毁时清理资源
    /// </summary>
    private void OnDestroy()
    {
        // 停止自动刷新协程
        StopAutoRefresh();
    }
    
    /// <summary>
    /// 创建房间按钮点击
    /// </summary>
    private void OnCreateRoomClicked()
    {
        Debug.Log("点击创建房间");

        if (TapSDKService.Instance != null)
        {
            TapSDKService.Instance.CreateRoom();
        }
    }
    
    /// <summary>
    /// 匹配房间按钮点击
    /// </summary>
    private void OnMatchRoomClicked()
    {
        Debug.Log("点击匹配房间");
        
        if (TapSDKService.Instance != null)
        {
            TapSDKService.Instance.StartMatchRoom();
        }
    }
    
    /// <summary>
    /// 开始游戏按钮点击
    ///
    /// 流程：
    /// 1. 先调用 UpdateRoomProperties 更新房间状态为 battleStatus = "fighting"
    /// 2. UpdateRoomProperties success 回调后：
    ///    - 帧同步：调用 StartBattle，等待 OnBattleStart 事件
    ///    - 状态同步：发送 START_GAME 消息，所有玩家收到后切换UI
    /// </summary>
    private void OnStartGameClicked()
    {
        Debug.Log($"点击开始游戏（当前模式：{GameConfig.GetModeName()}）");

        // 双重检查：确保只有房主才能开始游戏
        if (startGameButton != null && !startGameButton.interactable)
        {
            Debug.LogWarning("非房主无法开始游戏");
            return;
        }

        // 根据同步模式调用对应的Manager
        if (GameConfig.CurrentSyncMode == SyncMode.FrameSync)
        {
            // 帧同步：UpdateRoomProperties → StartBattle
            Debug.Log("[帧同步] 调用FrameSyncManager开始游戏");
            TapSDKService.Instance.UpdateRoomPropertiesToBattle(() =>
            {
                TapSDKService.Instance.StartBattle();
            });
        }
        else
        {
            // 状态同步：UpdateRoomProperties → SendStartGameMessage
            Debug.Log("[状态同步] 调用StateSyncManager开始游戏");
            StateSyncManager.Instance.StartGame();
        }
    }
    
    /// <summary>
    /// 当游戏开始时调用（由TapSDKService调用）
    /// </summary>
    public void OnBattleStart()
    {
        Debug.Log("游戏开始事件触发");

        // 切换到游戏中UI
        ShowInBattleUI();

        // 在BattleInteraction中初始化游戏玩家
        if (battleInteraction != null)
        {
            string myPlayerId = TapSDKService.Instance.GetCurrentPlayerId();
            battleInteraction.InitializeBattle(currentRoomPlayers, myPlayerId);
        }
    }
    
    /// <summary>
    /// 离开房间按钮点击 - 离开当前房间并返回大厅
    /// </summary>
    private void OnLeaveRoomClicked()
    {
        Debug.Log("点击离开房间按钮");

        // 调用TapSDKService离开房间
        if (TapSDKService.Instance != null)
        {
            TapSDKService.Instance.LeaveRoom();
        }
    }

    /// <summary>
    /// 分享房间按钮点击
    /// </summary>
    private void OnShareRoomClicked()
    {
        Debug.Log("点击分享房间");
        
        if (TapSDKService.Instance != null)
        {
            TapSDKService.Instance.ShareCurrentRoom();
        }
    }

    /// <summary>
    /// 关闭按钮点击 - 根据当前UI层决定行为
    /// roomUILayer: 离开房间回到游戏大厅
    /// gameLobbyLayer: 断开连接并退出游戏
    /// </summary>
    private void OnCloseButtonClicked()
    {
        // 判断当前在哪个UI层
        if (roomUILayer != null && roomUILayer.activeSelf)
        {
            // 在房间UI中，离开房间回到大厅
            Debug.Log("在房间UI中，点击关闭按钮 → 离开房间");
            if (TapSDKService.Instance != null)
            {
                TapSDKService.Instance.LeaveRoom();
            }
        }
        else if (gameLobbyLayer != null && gameLobbyLayer.activeSelf)
        {
            // 在游戏大厅中，退出游戏
            Debug.Log("在游戏大厅中，点击关闭按钮 → 退出游戏");
            if (TapSDKService.Instance != null)
            {
                TapSDKService.Instance.DisconnectAndQuit();
            }
            else
            {
                QuitGame();
            }
        }
        else
        {
            // 其他情况，默认退出游戏
            Debug.Log("点击关闭按钮 → 退出游戏");
            if (TapSDKService.Instance != null)
            {
                TapSDKService.Instance.DisconnectAndQuit();
            }
            else
            {
                QuitGame();
            }
        }
    }

    /// <summary>
    /// 断开连接按钮点击 - 断开多人联机服务连接
    /// </summary>
    private void OnDisconnectButtonClicked()
    {
        Debug.Log("点击断开连接按钮");

        if (TapSDKService.Instance != null)
        {
            TapSDKService.Instance.DisconnectBattle();
        }
    }

    /// <summary>
    /// 退出游戏
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("退出游戏");

        // 清理头像缓存（委托给AvatarManager）
        AvatarManager.Instance.ClearAllCache();
        
#if UNITY_EDITOR
        // 在Unity编辑器中停止播放
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 在构建版本中退出应用程序
        Tap.RestartMiniProgram(new RestartMiniProgramOption());
#endif
    }

    /// <summary>
    /// 显示加入房间确认弹窗
    /// </summary>
    public void ShowJoinRoomConfirmDialog(string roomId)
    {
        Debug.Log($"显示加入房间确认弹窗，房间ID: {roomId}");
        
        ShowModalOption option = new ShowModalOption();
        option.title = "加入房间";
        option.content = $"是否加入分享的房间？\n房间ID: {roomId}";
        option.showCancel = true;
        option.confirmText = "加入";
        option.cancelText = "取消";
        option.success = (result) =>
        {
            if (result.confirm)
            {
                Debug.Log("用户确认加入房间");
                if (TapSDKService.Instance != null)
                {
                    TapSDKService.Instance.JoinSharedRoom(roomId);
                }
            }
            else
            {
                Debug.Log("用户取消加入房间");
            }
        };
        option.fail = (result) =>
        {
            Debug.LogError($"显示加入房间弹窗失败: {result.errMsg}");
        };
        
        Tap.ShowModal(option);
    }

    #region 同步模式切换

    /// <summary>
    /// 同步模式Toggle回调
    /// </summary>
    /// <param name="isStateSync">true=状态同步, false=帧同步</param>
    private void OnSyncModeToggled(bool isStateSync)
    {
        SyncMode newMode = isStateSync ? SyncMode.StateSync : SyncMode.FrameSync;
        GameConfig.SetSyncMode(newMode);
        UpdateSyncModeUI();

        Debug.Log($"✅ 用户切换同步模式：{GameConfig.GetModeName()}");
    }

    /// <summary>
    /// 更新同步模式UI显示
    /// </summary>
    private void UpdateSyncModeUI()
    {
        if (syncModeText != null)
        {
            syncModeText.text = $"模式: {GameConfig.GetModeName()}";
        }

        if (syncModeToggle != null)
        {
            // 静默更新Toggle状态（不触发回调）
            syncModeToggle.SetIsOnWithoutNotify(GameConfig.CurrentSyncMode == SyncMode.StateSync);
        }
    }

    #endregion
}