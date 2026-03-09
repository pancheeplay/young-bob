using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TapTapMiniGame;

/// <summary>
/// 游戏对战交互脚本
/// 功能：
/// 1. 接收全屏点击事件
/// 2. 创建和管理玩家对象（显示头像）
/// 3. 处理玩家移动动画
/// 4. 处理帧同步数据
///
/// 挂载位置：InBattleUI（全屏Image）
/// </summary>
public class BattleInteraction : MonoBehaviour, IPointerClickHandler
{
    [Header("玩家对象管理")]
    [SerializeField] private GameObject playerPrefab; // 玩家预制体
    [SerializeField] private float moveSpeed = 500f; // 移动速度

    [Header("UI按钮")]
    [SerializeField] private UnityEngine.UI.Button endGameButton; // 结束游戏按钮

    [Header("帧同步统计显示")]
    [SerializeField] private UnityEngine.UI.Text frameRateText; // 帧同步统计文本组件

    // 玩家对象字典 <playerId, playerGameObject>
    private Dictionary<string, GameObject> battlePlayers = new Dictionary<string, GameObject>();

    // 玩家目标位置 <playerId, targetPosition>
    private Dictionary<string, Vector2> playerTargets = new Dictionary<string, Vector2>();

    private RectTransform rectTransform;
    private string myPlayerId;
    
    // 帧率统计管理器引用
    private FrameRateStats frameRateStats;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // 绑定结束游戏按钮事件
        if (endGameButton != null)
        {
            endGameButton.onClick.AddListener(OnEndGameButtonClicked);
        }
    }

    void Start()
    {
        // 初始化帧同步统计
        frameRateStats = FrameRateStats.Instance;
        if (frameRateStats != null)
        {
            // 注册统计更新事件
            frameRateStats.OnStatsUpdated += UpdateFrameRateUI;
            Debug.Log("✅ 帧同步统计UI已初始化");
        }
        else
        {
            Debug.LogWarning("⚠️ 无法获取FrameRateStats实例");
        }
        
        // 订阅网络延迟统计更新事件
        NetworkLatencyStats.Instance.OnStatsUpdated += UpdateFrameRateUI;
        Debug.Log("✅ 网络延迟统计UI已初始化");
        
        // 初始化显示
        UpdateFrameRateUI();
    }

    /// <summary>
    /// 初始化对战场景
    /// </summary>
    public void InitializeBattle(List<PlayerDisplayInfo> players, string currentPlayerId)
    {
        myPlayerId = currentPlayerId;

        // 清理旧玩家
        ClearAllPlayers();

        // 创建所有玩家对象
        foreach (var player in players)
        {
            // 从AvatarManager全局缓存中获取头像
            Sprite avatarSprite = AvatarManager.Instance.GetCachedAvatar(player.avatarUrl);
            if (avatarSprite != null)
            {
                Debug.Log($"✅ [AvatarManager] 从缓存获取头像: {player.playerName}");
            }

            CreatePlayerObject(player.playerId, player.playerName, avatarSprite);
        }
    }

    /// <summary>
    /// 创建玩家对象
    /// </summary>
    private void CreatePlayerObject(string playerId, string playerName, Sprite avatarSprite)
    {
        if (battlePlayers.ContainsKey(playerId))
        {
            Debug.LogWarning($"玩家 {playerId} 已存在");
            return;
        }

        GameObject playerObj = Instantiate(playerPrefab, transform);
        PlayerComponent playerComp = playerObj.GetComponent<PlayerComponent>();

        if (playerComp != null)
        {
            // 直接使用传入的头像Sprite（来自UIManager的缓存）
            if (avatarSprite != null)
            {
                playerComp.SetPlayerInfo(playerName, avatarSprite);
            }
            else
            {
                // 使用随机颜色作为备用头像
                playerComp.SetPlayerInfo(playerName, null);
                SetRandomAvatarColor(playerComp);
            }

            // 启用战斗模式（开启头像mask裁剪）
            playerComp.EnableBattleMode();
        }

        // 随机初始位置
        RectTransform playerRect = playerObj.GetComponent<RectTransform>();
        playerRect.anchoredPosition = new Vector2(
            Random.Range(-200f, 200f),
            Random.Range(-200f, 200f)
        );

        battlePlayers[playerId] = playerObj;
        playerTargets[playerId] = playerRect.anchoredPosition;

        Debug.Log($"创建玩家对象: {playerName} ({playerId})");
    }

    /// <summary>
    /// 动态添加新玩家（战斗中途加入）
    ///
    /// 用途：
    /// 状态同步模式下，玩家可以在战斗进行时加入房间
    /// 需要在战斗界面中动态创建新玩家的显示对象
    /// </summary>
    /// <param name="playerId">玩家ID</param>
    /// <param name="playerName">玩家昵称</param>
    /// <param name="avatarSprite">头像Sprite（可为null）</param>
    public void AddNewPlayer(string playerId, string playerName, Sprite avatarSprite, string avatarUrl = "")
    {
        Debug.Log($"🎮 [战斗中] 动态添加新玩家: {playerName} ({playerId})");

        // 先用现有头像创建玩家对象（可能为null）
        CreatePlayerObject(playerId, playerName, avatarSprite);

        // 如果没有头像但有URL，使用AvatarManager异步加载
        if (avatarSprite == null && !string.IsNullOrEmpty(avatarUrl))
        {
            Debug.Log($"🎮 [战斗中] 新玩家没有缓存头像，使用AvatarManager加载: {avatarUrl}");

            AvatarManager.Instance.LoadAvatar(avatarUrl,
                (sprite) => {
                    // 下载成功，更新玩家头像
                    if (battlePlayers.ContainsKey(playerId))
                    {
                        GameObject playerObj = battlePlayers[playerId];
                        PlayerComponent playerComp = playerObj.GetComponent<PlayerComponent>();
                        if (playerComp != null)
                        {
                            playerComp.SetAvatarSprite(sprite);
                            Debug.Log($"✅ [AvatarManager] 战斗玩家头像加载成功: {playerId}");
                        }
                    }
                },
                () => {
                    // 下载失败，保持随机颜色
                    Debug.LogWarning($"⚠️ [AvatarManager] 战斗玩家头像加载失败，保持随机颜色");
                }
            );
        }
    }

    /// <summary>
    /// 处理点击事件（实现IPointerClickHandler接口）
    ///
    /// 功能：响应全屏点击，将点击位置转换为本地坐标后发送移动指令
    /// 注意：这里只发送指令，不直接移动玩家（帧同步架构）
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 获取Canvas的渲染相机
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera camera = null;

        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Overlay模式不需要相机
                camera = null;
            }
            else
            {
                // Camera或WorldSpace模式需要相机
                camera = canvas.worldCamera ?? Camera.main;
            }
        }

        Debug.Log($"🖱️ 屏幕点击位置: {eventData.position}, Canvas模式: {canvas?.renderMode}, 相机: {camera?.name}");

        // 转换为本地坐标
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            eventData.position,
            camera,
            out localPoint))
        {
            Debug.Log($"✅ 转换后本地坐标: ({localPoint.x:F2}, {localPoint.y:F2})");

            // ⚠️ 帧同步架构：只发送移动指令，不直接操作玩家移动
            // 所有玩家的移动统一由 onBattleFrame 事件处理
            TapSDKService.Instance.SendPlayerMove(localPoint);
        }
        else
        {
            Debug.LogError("❌ 坐标转换失败");
        }
    }

    /// <summary>
    /// 处理帧同步数据（由TapSDKService调用）
    /// </summary>
    public void ProcessFrameData(BattleFrameData frameData)
    {
        // ⚠️ 空帧检查：帧同步框架会持续广播空帧（只有id，没有inputs）
        if (frameData == null)
        {
            return;
        }

        // 检查inputs是否为空
        if (frameData.inputs == null || frameData.inputs.Length == 0)
        {
            return;
        }

        // 处理每个玩家的输入
        foreach (var input in frameData.inputs)
        {
            // 检查输入数据是否有效
            if (input == null || string.IsNullOrEmpty(input.data))
            {
                continue;
            }

            try
            {
                Debug.Log($"📨 解析输入数据: {input.data}");

                // 首先尝试解析action字段来判断消息类型
                // 使用简单的JSON解析获取action字段
                string action = GetActionFromJson(input.data);

                if (action == "move")
                {
                    // 解析移动数据
                    MoveData moveData = JsonUtility.FromJson<MoveData>(input.data);

                    if (moveData != null)
                    {
                        string playerId = input.playerId;
                        Vector2 targetPos = new Vector2(moveData.x, moveData.y);

                        // 检查玩家是否存在
                        if (battlePlayers.ContainsKey(playerId))
                        {
                            playerTargets[playerId] = targetPos;
                            Debug.Log($"✅ 玩家 {playerId} 移动到 ({targetPos.x:F2}, {targetPos.y:F2})");
                        }
                        else
                        {
                            Debug.LogWarning($"❌ 收到未知玩家 {playerId} 的移动数据");
                        }
                    }
                }
                else if (action == "ping")
                {
                    // 解析ping数据
                    PingData pingData = JsonUtility.FromJson<PingData>(input.data);

                    if (pingData != null)
                    {
                        // 只处理自己发送的ping消息（计算往返延迟）
                        if (pingData.senderId == myPlayerId)
                        {
                            NetworkLatencyStats.Instance.OnPingReceived(pingData.pingId, pingData.timestamp);
                            Debug.Log($"✅ 收到自己的ping返回: pingId={pingData.pingId}");
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ 解析帧数据失败: {e.Message}, data: {input.data}");
            }
        }
    }

    /// <summary>
    /// 更新玩家移动动画
    /// </summary>
    void Update()
    {
        foreach (var kvp in battlePlayers)
        {
            string playerId = kvp.Key;
            GameObject playerObj = kvp.Value;

            if (playerObj == null || !playerTargets.ContainsKey(playerId)) continue;

            RectTransform playerRect = playerObj.GetComponent<RectTransform>();
            Vector2 currentPos = playerRect.anchoredPosition;
            Vector2 targetPos = playerTargets[playerId];

            float distance = Vector2.Distance(currentPos, targetPos);

            // 平滑移动
            if (distance > 1f)
            {
                Vector2 newPos = Vector2.MoveTowards(
                    currentPos,
                    targetPos,
                    moveSpeed * Time.deltaTime
                );

                playerRect.anchoredPosition = newPos;

                // 只在开始移动时输出一次日志
                if (distance > moveSpeed * Time.deltaTime)
                {
                    Debug.Log($"🚶 玩家 {playerId} 正在移动: 当前({currentPos.x:F1}, {currentPos.y:F1}) → 目标({targetPos.x:F1}, {targetPos.y:F1}), 距离={distance:F1}");
                }
            }
        }
    }

    /// <summary>
    /// 清理所有玩家对象
    /// </summary>
    private void ClearAllPlayers()
    {
        foreach (var playerObj in battlePlayers.Values)
        {
            if (playerObj != null)
            {
                Destroy(playerObj);
            }
        }

        battlePlayers.Clear();
        playerTargets.Clear();
    }

    void OnDestroy()
    {
        // 清理玩家对象
        ClearAllPlayers();
        
        // 清理事件订阅
        if (frameRateStats != null)
        {
            frameRateStats.OnStatsUpdated -= UpdateFrameRateUI;
        }
        
        // 清理网络延迟统计事件订阅
        if (NetworkLatencyStats.Instance != null)
        {
            NetworkLatencyStats.Instance.OnStatsUpdated -= UpdateFrameRateUI;
        }
    }

    /// <summary>
    /// 设置随机头像颜色（备用方案）
    /// </summary>
    private void SetRandomAvatarColor(PlayerComponent playerComp)
    {
        if (playerComp != null)
        {
            Color randomColor = new Color(
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f),
                Random.Range(0.3f, 1f)
            );
            playerComp.SetAvatarColor(randomColor);
        }
    }

    /// <summary>
    /// 结束游戏按钮点击事件
    ///
    /// 根据同步模式选择结束方式：
    /// - 帧同步：调用StopBattle，触发OnBattleStop事件返回房间UI
    /// - 状态同步：发送STOP_GAME消息，所有玩家收到后返回房间UI
    /// </summary>
    private void OnEndGameButtonClicked()
    {
        Debug.Log($"点击结束游戏按钮（当前模式：{GameConfig.GetModeName()}）");

        ShowModalOption option = new ShowModalOption();
        option.title = "结束对战";
        option.content = "确定要结束当前对战吗？";
        option.showCancel = true;
        option.cancelText = "取消";
        option.cancelColor = "#999999";
        option.confirmText = "确定";
        option.confirmColor = "#FF3B30";

        option.success = (ShowModalSuccessCallbackResult result) =>
        {
            if (result.confirm)
            {
                Debug.Log("用户确认结束对战");

                if (GameConfig.CurrentSyncMode == SyncMode.FrameSync)
                {
                    // 帧同步：调用EndBattle（会触发OnBattleStop事件）
                    Debug.Log("[帧同步] 调用EndBattle");
                    TapSDKService.Instance.EndBattle();
                }
                else
                {
                    // 状态同步：调用StateSyncManager结束游戏
                    Debug.Log("[状态同步] 调用StateSyncManager结束游戏");
                    StateSyncManager.Instance.StopGame();
                }
            }
            else if (result.cancel)
            {
                Debug.Log("用户取消结束对战");
            }
        };

        option.fail = (GeneralCallbackResult result) =>
        {
            Debug.LogError($"显示确认弹窗失败: {result.errMsg}");
        };

        Tap.ShowModal(option);
    }

    /// <summary>
    /// 更新帧同步统计UI显示
    /// </summary>
    private void UpdateFrameRateUI()
    {
        if (frameRateText != null)
        {
            // 第一行：帧同步统计
            string frameStatsText = frameRateStats != null ? frameRateStats.GetStatsText() : "等待数据...";
            
            // 第二行：网络延迟统计
            string latencyText = NetworkLatencyStats.Instance != null ? NetworkLatencyStats.Instance.GetLatencyText() : "等待数据...";
            
            // 拼接两行文本
            frameRateText.text = $"{frameStatsText}\n{latencyText}";
        }
    }
    
    /// <summary>
    /// 从JSON字符串中提取action字段
    /// </summary>
    private string GetActionFromJson(string json)
    {
        try
        {
            // 简单的JSON解析，查找 "action":"xxx" 模式
            int actionIndex = json.IndexOf("\"action\"");
            if (actionIndex >= 0)
            {
                int colonIndex = json.IndexOf(":", actionIndex);
                if (colonIndex >= 0)
                {
                    int startQuote = json.IndexOf("\"", colonIndex);
                    if (startQuote >= 0)
                    {
                        int endQuote = json.IndexOf("\"", startQuote + 1);
                        if (endQuote >= 0)
                        {
                            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 提取action字段失败: {e.Message}");
        }
        
        return "";
    }
}