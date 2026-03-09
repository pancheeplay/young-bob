using System;
using System.Collections;
using System.Collections.Generic;
using TapTapMiniGame;
using UnityEngine;
using LitJson;

/// <summary>
/// TapSDK服务层 - 多人联机Demo核心服务
///
/// 职责：
/// 1. 管理TapSDK的完整初始化流程（SDK初始化、登录、用户信息获取）
/// 2. 管理多人联机连接和房间匹配
/// 3. 处理多人联机业务逻辑（玩家进出房间、对战开始、帧同步）
/// 4. 维护游戏状态和房间玩家列表
///
/// 架构特点：
/// - 单例模式，全局唯一实例
/// - 逻辑层与管理层分离（GameManager控制流程，TapSDKService处理逻辑）
/// - 事件驱动设计，使用独立的TapBattleEventHandler处理事件通知
///
/// 使用示例：
/// TapSDKService.Instance.InitializeTapSDK();
/// TapSDKService.Instance.StartMatchRoom();
/// </summary>
public class TapSDKService : MonoBehaviour
{
    #region 单例模式
    
    private static TapSDKService _instance;
    public static TapSDKService Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<TapSDKService>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("TapSDKService");
                    _instance = go.AddComponent<TapSDKService>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    
    #endregion
    
        
    #region 游戏状态
    
    public enum GameState
    {
        Initializing,      // 初始化中
        LoggingIn,         // 登录中
        LoadingUserInfo,   // 获取用户信息中
        ConnectingBattle,  // 连接多人联机服务中
        InRoom,            // 在房间中
        InBattle,          // 对战中
        Error              // 错误状态
    }
    
    [SerializeField] private GameState currentState = GameState.Initializing;
    public GameState CurrentState => currentState;
    
    #endregion
    
    #region 用户数据

    [Header("用户信息")]
    public string playerName = "";          // 当前用户昵称（来自TapSDK授权）
    public string playerAvatarUrl = "";     // 当前用户头像URL（来自TapSDK授权）
    public string playerId = "";            // 当前用户在房间中的玩家ID（匹配成功后从roomInfo.players中获取）
    public bool isLoggedIn = false;         // 登录状态标记

    [Header("房间信息")]
    private string currentRoomId = "";  // 当前房间ID
    private List<PlayerDisplayInfo> roomPlayerList = new List<PlayerDisplayInfo>();  // 当前房间内所有玩家列表
    private TapOnlineBattleDemo.RoomCustomProperties currentRoomProperties = null;  // 当前房间的完整自定义属性

    // 缓存UIManager引用，避免频繁使用FindObjectOfType
    private UIManager cachedUIManager;
    #endregion

    
    
    
    #region TapSDK初始化流程
    
    /// <summary>
    /// 初始化TapSDK
    /// </summary>
    public IEnumerator InitializeTapSDK()
    {
        Debug.Log("正在初始化TapSDK...");
        SetState(GameState.Initializing);
        
        bool initCompleted = false;
        bool initSuccess = false;
        
        Tap.InitSDK((code) =>
        {
            Debug.Log($"TapSDK初始化完成，code: {code}");
            initSuccess = true;
            initCompleted = true;
        });
        
        // 等待初始化完成
        yield return new WaitUntil(() => initCompleted);
        
        if (initSuccess)
        {
            Debug.Log("✅ TapSDK初始化成功");
        }
        else
        {
            Debug.LogError("❌ TapSDK初始化失败");
            SetState(GameState.Error);
        }
    }
    
    /// <summary>
    /// 用户登录
    /// </summary>
    public IEnumerator LoginUser()
    {
        Debug.Log("正在进行用户登录...");
        SetState(GameState.LoggingIn);
        
        bool loginCompleted = false;
        bool loginSuccess = false;
        
        LoginOption option = new LoginOption();
        option.complete = (result) =>
        {
            Debug.Log($"登录完成: {result.errMsg}");
            loginCompleted = true;
        };
        option.success = (result) =>
        {
            Debug.Log($"登录成功: code={result.code}");
            loginCompleted = true;
            loginSuccess = true;
            isLoggedIn = true;
        };
        option.fail = (result) =>
        {
            Debug.LogError($"登录失败: {result.errMsg}");
            loginCompleted = true;
            loginSuccess = false;
        };
        
        Tap.Login(option);
        
        // 等待登录完成
        yield return new WaitUntil(() => loginCompleted);
        
        if (loginSuccess)
        {
            Debug.Log("✅ 用户登录成功");
        }
        else
        {
            Debug.LogError("❌ 用户登录失败");
            SetState(GameState.Error);
        }
    }
    
    /// <summary>
    /// 获取用户信息 - 包含完整的授权检查流程
    /// </summary>
    public IEnumerator LoadUserInfo()
    {
        Debug.Log("正在检查用户授权状态...");
        SetState(GameState.LoadingUserInfo);
        
#if UNITY_EDITOR && TAP_DEBUG_ENABLE
        // 调试模式：使用随机模拟数据
        Debug.Log("⚠️ 调试模式下，使用模拟用户信息");
        
        // 生成随机用户名
        string[] randomNames = { "测试玩家", "调试用户", "开发者", "Demo玩家", "测试账号", "Alpha测试", "Beta玩家", "编辑器用户" };
        int randomIndex = UnityEngine.Random.Range(0, randomNames.Length);
        int randomNumber = UnityEngine.Random.Range(1000, 9999);
        playerName = $"{randomNames[randomIndex]}{randomNumber}";
        
        // 头像使用空字符串，前端会用颜色替代
        // 测试可用的URL：
        playerAvatarUrl = "";
        
        Debug.Log($"✅ 调试模式用户信息生成完成");
        Debug.Log($"   用户名称: {playerName}");
        Debug.Log($"   头像: 使用颜色替代");
        
        yield break; // 直接返回，跳过后续流程
#endif
        
        bool settingCompleted = false;
        bool isAuthorized = false;
        
        // 1. 先检查用户授权状态
        Tap.GetSetting(new GetSettingOption
        {
            success = (res) =>
            {
                Debug.Log("GetSetting 调用成功");
                
                // 检查用户信息授权状态
                bool granted = res.authSetting != null &&
                        res.authSetting.ContainsKey("scope.userInfo") &&
                        res.authSetting["scope.userInfo"];
                        
                if (granted)
                {
                    Debug.Log("✅ 用户已授权用户信息权限");
                    isAuthorized = true;
                }
                else
                {
                    Debug.Log("❌ 用户未授权用户信息权限，需要申请授权");
                    isAuthorized = false;
                }
                
                settingCompleted = true;
            },
            fail = (error) =>
            {
                Debug.LogError($"GetSetting 调用失败: {error.errMsg}");
                isAuthorized = false;
                settingCompleted = true;
            },
            complete = (res) =>
            {
                Debug.Log("GetSetting 调用完成");
            }
        });
        
        // 等待授权状态检查完成
        yield return new WaitUntil(() => settingCompleted);
        
        if (isAuthorized)
        {
            // 2. 如果已授权，直接获取用户信息
            yield return StartCoroutine(GetUserInfoDirectly());
        }
        else
        {
            // 3. 如果未授权，创建全屏授权按钮
            yield return StartCoroutine(RequestUserInfoAuthorization());
        }
    }
    
    /// <summary>
    /// 直接获取用户信息（已授权状态下）
    /// </summary>
    private IEnumerator GetUserInfoDirectly()
    {
        Debug.Log("用户已授权，正在获取用户信息...");
        
        bool getUserInfoCompleted = false;
        bool getUserInfoSuccess = false;
        
        GetUserInfoOption option = new GetUserInfoOption();
        option.complete = (result) =>
        {
            Debug.Log($"获取用户信息完成: {result.errMsg}");
            getUserInfoCompleted = true;
        };
        option.success = (result) =>
        {
            Debug.Log($"获取用户信息成功");
            playerName = result.userInfo.nickName;
            playerAvatarUrl = result.userInfo.avatarUrl;
            getUserInfoSuccess = true;
            
            Debug.Log($"用户名称: {playerName}");
            Debug.Log($"用户头像: {playerAvatarUrl}");
        };
        option.fail = (result) =>
        {
            Debug.LogError($"获取用户信息失败: {result.errMsg}");
            getUserInfoSuccess = false;
        };
        
        Tap.GetUserInfo(option);
        
        // 等待获取用户信息完成
        yield return new WaitUntil(() => getUserInfoCompleted);
        
        if (getUserInfoSuccess)
        {
            Debug.Log("✅ 获取用户信息成功");
        }
        else
        {
            Debug.LogError("❌ 获取用户信息失败");
            SetState(GameState.Error);
        }
    }
    
    /// <summary>
    /// 请求用户信息授权（未授权状态下）
    /// </summary>
    private IEnumerator RequestUserInfoAuthorization()
    {
        Debug.Log("用户未授权，创建全屏授权按钮...");
        
        bool authorizationCompleted = false;
        bool authorizationSuccess = false;
        
        try
        {
            // 创建全屏淡绿色透明授权按钮
            var option = new CreateUserInfoButtonOption
            {
                type = "text",
                text = "点击屏幕授权获取用户信息",
                style = new UserInfoButtonStyle
                {
                    left = 0,
                    top = 0,
                    width = Screen.width,
                    height = Screen.height,
                    backgroundColor = "#8800FF00", // 淡绿色透明背景
                    borderColor = "transparent",
                    borderWidth = 0,
                    borderRadius = 0,
                    color = "#FFFFFF",
                    textAlign = "center",
                    fontSize = 24,
                    lineHeight = 32
                },
                withCredentials = true,
                lang = "zh_CN"
            };

#if UNITY_EDITOR
            // Unity编辑器中的调试信息
            Debug.Log("Unity编辑器环境下，模拟用户授权成功");
            playerName = "测试用户";
            playerAvatarUrl = "https://example.com/avatar.jpg";
            authorizationSuccess = true;
            authorizationCompleted = true;
#else
            var authButton = Tap.CreateUserInfoButton(option);
            
            // 设置点击事件
            authButton.OnTap((response) =>
            {
                try
                {
                    Debug.Log($"授权按钮回调: {JsonMapper.ToJson(response)}");
                    
                    // 判断授权是否成功
                    bool isSuccess = false;
                    
                    if (response?.errMsg == "getUserInfo:ok")
                    {
                        isSuccess = true;
                    }
                    else if (!string.IsNullOrEmpty(response?.errMsg) && response.errMsg.Contains("ok") 
                             && response?.userInfo != null 
                             && !string.IsNullOrEmpty(response.userInfo.avatarUrl) 
                             && response.userInfo.avatarUrl.StartsWith("http"))
                    {
                        isSuccess = true;
                    }
                    else if (string.IsNullOrEmpty(response?.errMsg?.Trim()) && response?.userInfo != null)
                    {
                        isSuccess = true;
                    }
                    
                    if (isSuccess)
                    {
                        Debug.Log($"✅ 用户授权成功，获取到用户信息: {response.userInfo.nickName}");
                        
                        // 保存用户信息
                        playerName = response.userInfo.nickName ?? "未知用户";
                        playerAvatarUrl = response.userInfo.avatarUrl ?? "";
                        
                        authorizationSuccess = true;
                        
                        Debug.Log($"用户名称: {playerName}");
                        Debug.Log($"用户头像: {playerAvatarUrl}");
                    }
                    else
                    {
                        string errorMsg = response?.errMsg ?? "未知错误";
                        Debug.LogError($"❌ 用户授权失败: {errorMsg}");
                        authorizationSuccess = false;
                    }
                    
                    // 隐藏授权按钮
                    authButton.Hide();
                    authorizationCompleted = true;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"处理授权回调时发生异常: {ex.Message}");
                    authorizationSuccess = false;
                    authButton.Hide();
                    authorizationCompleted = true;
                }
            });
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"创建授权按钮失败: {e.Message}");
            authorizationSuccess = false;
            authorizationCompleted = true;
        }
        
        // 等待授权完成
        yield return new WaitUntil(() => authorizationCompleted);
        
        if (authorizationSuccess)
        {
            Debug.Log("✅ 用户授权并获取用户信息成功");
        }
        else
        {
            Debug.LogError("❌ 用户授权失败，将使用默认用户信息");
            playerName = "未知用户";
            playerAvatarUrl = "";
        }
    }
    
    #endregion
    
    #region 多人联机流程
    
    /// <summary>
    /// 初始化多人联机系统
    /// </summary>
    public IEnumerator InitializeBattle()
    {
        Debug.Log("正在初始化多人联机系统...");

        try
        {
            // 创建独立的事件处理器
            TapBattleEventHandler battleEventHandler = new TapBattleEventHandler();

            // 使用事件处理器初始化多人联机SDK
            TapBattleClient.Initialize(battleEventHandler);
            Debug.Log("✅ 多人联机系统初始化成功");

        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 多人联机系统初始化失败: {e.Message}");
            SetState(GameState.Error);
        }

        yield return null;
    }
    
    /// <summary>
    /// 连接多人联机服务
    ///
    /// 重要：连接成功后会返回playerId，这是服务器分配的全局唯一玩家ID
    /// 后续所有多人联机操作都需要使用这个playerId
    /// </summary>
    public IEnumerator ConnectToBattleService()
    {
        Debug.Log("正在连接多人联机服务...");
        SetState(GameState.ConnectingBattle);

        bool connectCompleted = false;
        bool connectSuccess = false;
        bool needReconnect = false;

        var option = new BattleConnectOption
        {
            success = (result) =>
            {
                Debug.Log("✅ 多人联机服务连接成功");

                // ⚠️ 关键：保存服务器返回的playerId
                // 这是全局唯一的玩家ID，后续SendInput等操作需要用到
                playerId = result.playerId;
                Debug.Log($"✅ 获取到playerId: {playerId}");

                connectSuccess = true;
            },
            fail = (result) =>
            {
                // errno=14 表示已经登录过了，需要先清理再重连
                if (result.errMsg.Equals("already connected"))
                {
                    Debug.Log($"⚠️ 多人联机服务重复登录: {result.errMsg}，需要清理重连");
                    needReconnect = true; // 标记需要重连
                    connectSuccess = false;
                }
                else
                {
                    Debug.LogError($"❌ 多人联机服务连接失败: {result.errMsg} (错误码: {result.errNo})");
                    connectSuccess = false;
                }
            },
            complete = (result) =>
            {
                Debug.Log("多人联机连接操作完成");
                connectCompleted = true;
            }
        };

        TapBattleClient.Connect(option);

        // 等待连接完成
        yield return new WaitUntil(() => connectCompleted);

        // 判断是否需要重连（处理errNo=14重复登录情况）
        if (needReconnect)
        {
            Debug.Log("�� 检测到需要重连，开始清理重连流程...");

            // 1. 盲调用离开房间（不关心返回结果）
            Debug.Log("🔧 调用LeaveRoom清理房间状态...");
            try
            {
                TapBattleClient.LeaveRoom(new LeaveRoomOption());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"⚠️ LeaveRoom调用异常（忽略）: {e.Message}");
            }
            yield return new WaitForSeconds(0.1f);

            // 2. 盲调用断开连接（不关心返回结果）
            Debug.Log("🔧 调用Disconnect清理连接状态...");
            try
            {
                var disconnectOption = new BattleOption();
                TapBattleClient.Disconnect(disconnectOption);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"⚠️ Disconnect调用异常（忽略）: {e.Message}");
            }
            yield return new WaitForSeconds(0.5f);

            // 3. 重新连接（调用简单重连函数，避免递归风险）
            needReconnect = false; // 重置标志，避免无限重连
            Debug.Log("🔧 开始重新连接多人联机服务...");
            yield return SimpleReconnectBattleService();
        }
        else if (connectSuccess)
        {
            Debug.Log($"✅ 多人联机服务连接成功，playerId: {playerId}");
        }
        else
        {
            Debug.LogError("❌ 多人联机服务连接失败");
            SetState(GameState.Error);
        }
    }

    /// <summary>
    /// 简单重连多人联机服务
    ///
    /// 使用场景：
    /// 处理errNo=14重复登录后的重连操作
    /// 只做最基础的连接操作，避免复杂逻辑和递归风险
    /// </summary>
    private IEnumerator SimpleReconnectBattleService()
    {
        Debug.Log("🔄 执行简单重连多人联机服务...");

        bool reconnectCompleted = false;
        bool reconnectSuccess = false;

        var option = new BattleConnectOption
        {
            success = (result) =>
            {
                Debug.Log("✅ 重连成功");

                // 保存服务器返回的playerId
                playerId = result.playerId;
                Debug.Log($"✅ 重连获取到playerId: {playerId}");

                reconnectSuccess = true;
            },
            fail = (result) =>
            {
                Debug.LogError($"❌ 重连失败: {result.errMsg} (错误码: {result.errNo})");
                reconnectSuccess = false;
            },
            complete = (result) =>
            {
                Debug.Log("重连操作完成");
                reconnectCompleted = true;
            }
        };

        TapBattleClient.Connect(option);

        // 等待重连完成
        yield return new WaitUntil(() => reconnectCompleted);

        if (reconnectSuccess)
        {
            Debug.Log($"✅ 重连成功，playerId: {playerId}");
        }
        else
        {
            Debug.LogError("❌ 重连失败，设置错误状态");
            SetState(GameState.Error);
        }
    }

    #endregion

    #region 公共接口
    
    /// <summary>
    /// 开始匹配房间
    /// </summary>
    public void StartMatchRoom()
    {
        if (currentState != GameState.ConnectingBattle && currentState != GameState.InRoom)
        {
            Debug.LogWarning("当前状态不允许匹配房间");
            return;
        }

        Debug.Log("开始匹配房间...");

        // 创建房间自定义属性（初始状态为idle）
        var roomCustomProps = new TapOnlineBattleDemo.RoomCustomProperties(
            gameMode: "move",
            ownerName: playerName,
            roomName: "测试房间",
            ownerAvatarUrl: playerAvatarUrl,
            roomDescription: "头像大作战测试房间",
            battleStatus: "idle"  // ⚠️ 初始状态为空闲
        );

        // 保存初始房间属性
        currentRoomProperties = roomCustomProps;

        var option = new MatchRoomOption
        {
            data = new MatchRoomRequest
            {
                roomCfg = new RoomConfig
                {
                    maxPlayerCount = 4,
                    type = "多人移动demo",
                    customProperties = JsonMapper.ToJson(roomCustomProps),
                    matchParams = new Dictionary<string, string>
                    {
                        { "level", "1" },
                        { "score", "0" }
                    }
                },
                playerCfg = new PlayerConfig
                {
                    customStatus = 0,
                    customProperties = JsonMapper.ToJson(new PlayerCustomProperties(playerName, playerAvatarUrl))
                }
            },
            success = (result) =>
            {
                Debug.Log($"✅ 匹配房间成功！房间ID: {result.roomInfo.id}");
                SetState(GameState.InRoom);

                // 处理房间玩家信息
                HandleRoomPlayersInfo(result.roomInfo);
            },
            fail = (result) =>
            {
                Debug.LogError($"❌ 匹配房间失败: {result.errMsg}");
            }
        };

        TapBattleClient.MatchRoom(option);
    }

    /// <summary>
    /// 创建房间
    /// </summary>
    public void CreateRoom()
    {
        if (currentState != GameState.ConnectingBattle && currentState != GameState.InRoom)
        {
            Debug.LogWarning("当前状态不允许创建房间");
            return;
        }

        Debug.Log("开始创建房间...");

        // 创建房间自定义属性（初始状态为idle）
        var roomCustomProps = new TapOnlineBattleDemo.RoomCustomProperties(
            gameMode: "move",
            ownerName: playerName,
            roomName: "测试房间",
            ownerAvatarUrl: playerAvatarUrl,
            roomDescription: "头像大作战测试房间",
            battleStatus: "idle"  // ⚠️ 初始状态为空闲
        );

        // 保存初始房间属性
        currentRoomProperties = roomCustomProps;

        Debug.Log($"创建房间customProperties: {JsonMapper.ToJson(roomCustomProps)}");

        var option = new CreateRoomOption
        {
            data = new CreateRoomRequest
            {
                roomCfg = new RoomConfig
                {
                    maxPlayerCount = 4,
                    type = "多人移动demo",
                    name = "测试房间",
                    customProperties = JsonMapper.ToJson(roomCustomProps),
                    matchParams = new Dictionary<string, string>
                    {
                        { "level", "1" },
                        { "score", "0" }
                    }
                },
                playerCfg = new PlayerConfig
                {
                    customStatus = 0,
                    customProperties = JsonMapper.ToJson(new PlayerCustomProperties(playerName, playerAvatarUrl))
                }
            },
            success = (result) =>
            {
                Debug.Log($"✅ 创建房间成功！房间ID: {result.roomInfo.id}");
                SetState(GameState.InRoom);

                // 处理房间玩家信息
                HandleRoomPlayersInfo(result.roomInfo);
            },
            fail = (result) =>
            {
                Debug.LogError($"❌ 创建房间失败: errNo={result.errNo}, errMsg={result.errMsg}");
            }
        };

        // 调试日志：打印完整的CreateRoom请求数据
        Debug.Log($"CreateRoom请求数据: {JsonMapper.ToJson(option.data)}");

        TapBattleClient.CreateRoom(option);
    }

    /// <summary>
    /// 离开房间
    /// </summary>
    public void LeaveRoom()
    {
        Debug.Log("调用TapBattleClient.LeaveRoom()...");

        var option = new LeaveRoomOption
        {
            success = (result) =>
            {
                Debug.Log("✅ LeaveRoom成功");
                // 离开成功后返回游戏大厅
                HandleLeaveRoomSuccess();
            },
            fail = (result) =>
            {
                Debug.LogError($"❌ LeaveRoom失败: {result.errMsg}");
            },
            complete = (result) =>
            {
                Debug.Log("LeaveRoom操作完成");
            }
        };

        TapBattleClient.LeaveRoom(option);
    }

    /// <summary>
    /// 更新房间属性为战斗状态
    ///
    /// 流程：
    /// 1. 先调用 UpdateRoomProperties 更新 battleStatus = "fighting"
    /// 2. success 回调中再调用 StartBattle（帧同步）或 SendStartGameMessage（状态同步）
    ///
    /// ⚠️ 重要：必须保留原有的其他字段，只更新battleStatus
    /// </summary>
    public void UpdateRoomPropertiesToBattle(System.Action onSuccess)
    {
        Debug.Log("更新房间属性为战斗状态...");

        // 使用保存的房间属性（如果有），否则创建新的
        TapOnlineBattleDemo.RoomCustomProperties roomProps;

        if (currentRoomProperties != null)
        {
            // 使用保存的属性，只修改battleStatus
            Debug.Log("✅ 使用保存的房间属性，只修改battleStatus");
            roomProps = new TapOnlineBattleDemo.RoomCustomProperties(
                gameMode: currentRoomProperties.gameMode,
                ownerName: currentRoomProperties.ownerName,
                roomName: currentRoomProperties.roomName,
                ownerAvatarUrl: currentRoomProperties.ownerAvatarUrl,
                roomDescription: currentRoomProperties.roomDescription,
                battleStatus: "fighting"  // ⚠️ 修改为战斗中
            );
        }
        else
        {
            // Fallback：使用当前玩家信息创建
            Debug.LogWarning("⚠️ currentRoomProperties为null，使用当前玩家信息创建");
            roomProps = new TapOnlineBattleDemo.RoomCustomProperties(
                gameMode: "move",
                ownerName: playerName,
                roomName: "测试房间",
                ownerAvatarUrl: playerAvatarUrl,
                roomDescription: "头像大作战测试房间",
                battleStatus: "fighting"
            );
        }

        Debug.Log($"UpdateRoomProperties数据: {JsonMapper.ToJson(roomProps)}");

        var option = new UpdateRoomPropertiesOption
        {
            data = new UpdateRoomPropertiesData
            {
                customProperties = JsonMapper.ToJson(roomProps)
            },
            success = (result) =>
            {
                Debug.Log("✅ 房间属性更新成功：battleStatus = fighting");

                // 保存更新后的房间属性
                currentRoomProperties = roomProps;

                // 回调通知可以开始游戏了
                onSuccess?.Invoke();
            },
            fail = (result) =>
            {
                Debug.LogError($"❌ 房间属性更新失败: {result.errMsg} (错误码: {result.errNo})");
            },
            complete = (result) =>
            {
                Debug.Log("UpdateRoomProperties操作完成");
            }
        };

        TapBattleClient.UpdateRoomProperties(option);
    }

    /// <summary>
    /// 开始对战（只有房主可以调用）
    /// 注意：调用前需要先调用 UpdateRoomPropertiesToBattle
    /// </summary>
    public void StartBattle()
    {
        Debug.Log("调用TapBattleClient.StartBattle()...");

        var option = new StartFrameSyncOption
        {
            success = (result) =>
            {
                Debug.Log("✅ StartBattle成功");
            },
            fail = (result) =>
            {
                Debug.LogError($"❌ StartBattle失败: {result.errMsg} (错误码: {result.errNo})");
            },
            complete = (result) =>
            {
                Debug.Log("StartBattle操作完成");
            }
        };

        TapBattleClient.StartFrameSync(option);
    }

    /// <summary>
    /// 发送玩家移动输入
    ///
    /// 根据当前同步模式自动选择发送方式：
    /// - 帧同步：使用SendInput
    /// - 状态同步：使用SendCustomMessage
    /// </summary>
    public void SendPlayerMove(Vector2 targetPosition)
    {
        // 使用MoveData实体类
        var moveData = new MoveData(
            targetPosition.x,
            targetPosition.y,
            0f,
            playerId, // 使用playerId
            DateTimeOffset.Now.ToUnixTimeMilliseconds()
        );

        // 根据同步模式选择发送方式
        if (GameConfig.CurrentSyncMode == SyncMode.FrameSync)
        {
            // 帧同步：检查对战状态
            if (currentState != GameState.InBattle)
            {
                Debug.LogWarning("[帧同步] 当前不在对战状态，无法发送移动指令");
                return;
            }

            // 使用SendInput
            var option = new SendFrameInputOption
            {
                data = JsonMapper.ToJson(moveData),
                success = (result) =>
                {
                    Debug.Log($"[帧同步] 移动指令发送成功: ({targetPosition.x:F2}, {targetPosition.y:F2})");
                },
                fail = (result) =>
                {
                    Debug.LogError($"[帧同步] 移动指令发送失败: {result.errMsg}");
                }
            };

            TapBattleClient.SendFrameInput(option);
        }
        else
        {
            // 状态同步：不需要检查InBattle状态，只要在房间中就可以发送
            if (currentState != GameState.InRoom && currentState != GameState.InBattle)
            {
                Debug.LogWarning("[状态同步] 当前不在房间或对战状态，无法发送移动指令");
                return;
            }

            // 调用StateSyncManager发送移动
            StateSyncManager.Instance.SendMove(moveData);
        }
    }
    
    /// <summary>
    /// 发送Ping测试消息（用于网络延迟测试）
    /// </summary>
    public void SendPingTest()
    {
        if (currentState != GameState.InBattle)
        {
            Debug.LogWarning("当前不在对战状态，无法发送ping测试");
            return;
        }

        // 从NetworkLatencyStats获取下一个ping数据
        PingData pingData = NetworkLatencyStats.Instance.GetNextPing(playerId);

        var option = new SendFrameInputOption
        {
            data = JsonMapper.ToJson(pingData),
            success = (result) =>
            {
                Debug.Log($"✅ Ping测试发送成功: pingId={pingData.pingId}");
            },
            fail = (result) =>
            {
                Debug.LogError($"❌ Ping测试发送失败: {result.errMsg}");
            }
        };

        TapBattleClient.SendFrameInput(option);
    }
    
    /// <summary>
    /// 结束对战
    /// 只调用API，后续逻辑在OnBattleStop事件中处理
    /// </summary>
    public void EndBattle()
    {
        Debug.Log("调用结束对战API...");

        var stopOption = new StopFrameSyncOption
        {
            success = (result) =>
            {
                Debug.Log("✅ 停止对战API调用成功");
            },
            fail = (result) =>
            {
                Debug.LogError($"❌ 停止对战API调用失败: {result.errMsg} (错误码: {result.errNo})");
            },
            complete = (result) =>
            {
                Debug.Log("停止对战API调用完成");
            }
        };

        TapBattleClient.StopFrameSync(stopOption);
    }


    /// <summary>
    /// 踢出玩家（仅房主可用）
    /// </summary>
    /// <param name="targetPlayerId">要踢出的玩家ID</param>
    public void KickPlayer(string targetPlayerId)
    {
        if (string.IsNullOrEmpty(targetPlayerId))
        {
            Debug.LogError("踢人失败：玩家ID为空");
            return;
        }

        Debug.Log($"正在踢出玩家: {targetPlayerId}");

        var option = new KickRoomPlayerOption
        {
            playerId = targetPlayerId,
            success = (result) =>
            {
                Debug.Log($"✅ 成功踢出玩家: {targetPlayerId}");
            },
            fail = (result) =>
            {
                Debug.LogError($"❌ 踢出玩家失败: {result.errMsg} (错误码: {result.errNo})");
            },
            complete = (result) =>
            {
                Debug.Log("踢人操作完成");
            }
        };

        TapBattleClient.KickRoomPlayer(option);
    }

    /// <summary>
    /// 断开多人联机连接（不退出游戏，留在大厅）
    /// </summary>
    public void DisconnectBattle()
    {
        Debug.Log("断开多人联机连接...");

        try
        {
            // 清理房间玩家列表
            roomPlayerList.Clear();

            // 断开多人联机连接
            if (currentState == GameState.InRoom || currentState == GameState.InBattle || currentState == GameState.ConnectingBattle)
            {
                var disconnectOption = new BattleOption
                {
                    success = (result) =>
                    {
                        Debug.Log("✅ 多人联机断开连接成功");
                        SetState(GameState.Initializing);
                    },
                    fail = (result) =>
                    {
                        Debug.LogError($"❌ 多人联机断开连接失败: {result.errMsg}");
                        SetState(GameState.Error);
                    },
                    complete = (result) =>
                    {
                        Debug.Log("多人联机断开连接操作完成");
                    }
                };

                TapBattleClient.Disconnect(disconnectOption);
            }
            else
            {
                Debug.LogWarning($"当前状态不需要断开连接: {currentState}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"断开多人联机连接时发生异常: {e.Message}");
        }
    }

    /// <summary>
    /// 断开连接并退出游戏
    /// </summary>
    public void DisconnectAndQuit()
    {
        Debug.Log("开始断开多人联机连接...");

        try
        {
            // 清理房间玩家列表
            roomPlayerList.Clear();

            // 断开多人联机连接
            if (currentState == GameState.InRoom || currentState == GameState.InBattle || currentState == GameState.ConnectingBattle)
            {
                var disconnectOption = new BattleOption
                {
                    success = (result) =>
                    {
                        Debug.Log("✅ 多人联机断开连接成功");
                        NotifyUIToQuit();
                    },
                    fail = (result) =>
                    {
                        Debug.LogError($"❌ 多人联机断开连接失败: {result.errMsg}");
                        // 即使断开失败也要退出游戏
                        NotifyUIToQuit();
                    },
                    complete = (result) =>
                    {
                        Debug.Log("��人对战断开连接操作完成");
                    }
                };
                
                TapBattleClient.Disconnect(disconnectOption);
            }
            else
            {
                // 如果不在多人联机状态，直接退出
                NotifyUIToQuit();
            }
            
            // 重置状态
            SetState(GameState.Error);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"断开连接时发生错误: {e.Message}");
            // 即使出错也要退出游戏
            NotifyUIToQuit();
        }
    }
    
    /// <summary>
    /// 通知UI执行退出游戏
    /// </summary>
    private void NotifyUIToQuit()
    {
        // 查找UIManager并调用退出方法
        UIManager uiManager = GetUIManager();
        if (uiManager != null)
        {
            uiManager.QuitGame();
        }
        else
        {
            // 如果找不到UIManager，直接退出
            Debug.LogError("找不到UIManager，直接退出游戏");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Tap.RestartMiniProgram(new RestartMiniProgramOption());
#endif
        }
    }

    /// <summary>
    /// 获取UIManager引用（使用缓存优化性能）
    /// </summary>
    private UIManager GetUIManager()
    {
        if (cachedUIManager == null)
        {
            cachedUIManager = FindObjectOfType<UIManager>();
            if (cachedUIManager == null)
            {
                Debug.LogError("找不到UIManager组件");
            }
        }
        return cachedUIManager;
    }
    
    #endregion
    
    #region 状态管理
    
    /// <summary>
    /// 设置游戏状态
    /// </summary>
    public void SetState(GameState newState)
    {
        if (currentState != newState)
        {
            Debug.Log($"游戏状态变更: {currentState} -> {newState}");
            currentState = newState;
        }
    }
    
    /// <summary>
    /// 获取状态描述
    /// </summary>
    public string GetStateDescription()
    {
        switch (currentState)
        {
            case GameState.Initializing: return "初始化中...";
            case GameState.LoggingIn: return "登录中...";
            case GameState.LoadingUserInfo: return "获取用户信息中...";
            case GameState.ConnectingBattle: return "连接多人联机服务中...";
            case GameState.InRoom: return "在房间中";
            case GameState.InBattle: return "对战中";
            case GameState.Error: return "发生错误";
            default: return "未知状态";
        }
    }

    /// <summary>
    /// 获取当前玩家ID
    /// </summary>
    public string GetCurrentPlayerId()
    {
        return playerId;
    }

    /// <summary>
    /// 获取房间玩家列表
    /// 供Manager使用
    /// </summary>
    public List<PlayerDisplayInfo> GetRoomPlayerList()
    {
        return roomPlayerList;
    }
    
    /// <summary>
    /// 处理房间玩家信息
    ///
    /// 功能：
    /// 1. 解析roomInfo.players获取房间内所有玩家信息
    /// 2. 从每个玩家的customProperties中提取昵称和头像URL
    /// 3. 保存每个玩家的player.id到PlayerDisplayInfo（用于UI显示和判断房主）
    /// 4. 标记房主（通过player.id == roomInfo.ownerId判断）
    /// 5. ✨检测房间battleStatus，如果是"fighting"则直接进入战斗（状态同步特有）
    /// 6. 通知UIManager更新玩家列表显示
    ///
    /// ⚠️ 重要：
    /// - roomInfo.players[].id 是房间内的玩家ID（每个玩家在房间中的临时ID）
    /// - Connect返回的playerId 是当前用户的全局唯一ID
    /// - 通过比较 player.id == Connect返回的playerId 来判断"哪个是我"
    /// - 通过比较 player.id == roomInfo.ownerId 来判断"谁是房主"
    ///
    /// 使用场景：
    /// - MatchRoom success 回调
    /// - JoinRoom success 回调（从房间列表加入）
    /// - CreateRoom success 回调
    /// </summary>
    public void HandleRoomPlayersInfo(RoomInfo roomInfo)
    {
        try
        {
            // 保存当前房间ID（用于分享功能）
            currentRoomId = roomInfo.id;
            Debug.Log($"保存当前房间ID: {currentRoomId}");

            // 清空房间玩家列表
            roomPlayerList.Clear();

            Debug.Log($"房间匹配成功，房间ID: {roomInfo.id}, 房主ID: {roomInfo.ownerId}");
            Debug.Log($"当前用户playerId: {playerId}");  // Connect时获取的playerId
            Debug.Log($"roomInfo内容: {JsonMapper.ToJson(roomInfo)}"); // 打印roomInfo内容用于调试

            // ✨ 检测房间战斗状态（状态同步特有功能）
            bool isRoomFighting = false;
            if (!string.IsNullOrEmpty(roomInfo.customProperties))
            {
                try
                {
                    Debug.Log($"🔍 解析房间customProperties: {roomInfo.customProperties}");
                    var roomProps = JsonMapper.ToObject<TapOnlineBattleDemo.RoomCustomProperties>(roomInfo.customProperties);

                    // 保存当前房间属性
                    currentRoomProperties = roomProps;

                    Debug.Log($"🔍 房间属性解析成功 - battleStatus: {roomProps.battleStatus}");

                    if (!string.IsNullOrEmpty(roomProps.battleStatus) && roomProps.battleStatus == "fighting")
                    {
                        isRoomFighting = true;
                        Debug.Log("⚠️ 检测到房间正在战斗中（battleStatus = fighting）");
                    }
                    else
                    {
                        Debug.Log($"✅ 房间状态：{roomProps.battleStatus ?? "null/空"}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"❌ 解析房间customProperties失败: {e.Message}");
                    Debug.LogError($"   customProperties内容: {roomInfo.customProperties}");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ roomInfo.customProperties 为空");
            }

            // 解析房间内所有玩家信息
            if (roomInfo.players != null && roomInfo.players.Length > 0)
            {
                Debug.Log($"房间内玩家数量: {roomInfo.players.Length}");
                
                foreach (var player in roomInfo.players)
                {
                    if (player != null)
                    {
                        string playerName = "未知玩家";
                        string avatarUrl = "";
                        bool isOwner = player.id == roomInfo.ownerId;
                        
                        // 解析玩家的customProperties获取昵称和头像
                        if (!string.IsNullOrEmpty(player.customProperties))
                        {
                            try
                            {
                                var customProps = JsonMapper.ToObject<PlayerCustomProperties>(player.customProperties);
                                if (!string.IsNullOrEmpty(customProps.playerName))
                                {
                                    playerName = customProps.playerName;
                                }
                                if (!string.IsNullOrEmpty(customProps.avatarUrl))
                                {
                                    avatarUrl = customProps.avatarUrl;
                                }
                            }
                            catch (System.Exception parseEx)
                            {
                                Debug.LogError($"解析玩家 {player.id} 的customProperties失败: {parseEx.Message}");
                            }
                        }

                        // ✅ 判断这个玩家是否是当前用户
                        // 通过比较 player.id 和 Connect返回的playerId
                        bool isMe = (player.id == this.playerId);

                        // 添加到玩家列表
                        var playerDisplayInfo = new PlayerDisplayInfo(playerName, avatarUrl, player.id, isOwner);
                        roomPlayerList.Add(playerDisplayInfo);

                        string ownerText = isOwner ? "(房主)" : "";
                        string meText = isMe ? " ← 这是我" : "";
                        Debug.Log($"玩家: {playerName}{ownerText}, ID: {player.id}, 状态: {player.status}{meText}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("roomInfo.players 为空或null，fallback到当前玩家信息");
                // Fallback: 如果roomInfo中没有玩家信息，使用当前玩家信息
                roomPlayerList.Add(new PlayerDisplayInfo(playerName, playerAvatarUrl, "", false));
            }
            
            Debug.Log($"✅ 房间玩家信息处理完成，当前玩家数: {roomPlayerList.Count}");

            // 获取UIManager
            UIManager uiManager = GetUIManager();

            // ✨ 状态同步特有：如果房间已经在战斗中，先显示队伍UI加载头像，再自动进入战斗
            Debug.Log($"🔍 检查是否需要自动进入战斗 - isRoomFighting:{isRoomFighting}, CurrentSyncMode:{GameConfig.CurrentSyncMode}, currentState:{currentState}");

            // 先显示房间UI并更新玩家列表（加载头像）
            if (uiManager != null)
            {
                uiManager.ShowRoomUI();  // 切换到房间UI
                uiManager.OnMatchRoomSuccess();
                uiManager.UpdatePlayerList(roomPlayerList);
            }

            // ✨ 如果房间在战斗中，延迟自动进入战斗
            if (isRoomFighting)
            {
                Debug.Log("🎮 检测到房间正在战斗中，调用StateSyncManager处理");

                // 调用StateSyncManager处理延迟进入战斗
                StartCoroutine(StateSyncManager.Instance.AutoEnterBattleAfterDelay(2f));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 处理房间玩家信息失败: {e.Message}");
        }
    }
    

    /// <summary>
    /// 处理玩家进入房间
    ///
    /// 两种情况：
    /// 1. 房间状态（InRoom）：更新房间玩家列表UI
    /// 2. 战斗状态（InBattle）：在战斗界面动态创建新玩家对象（状态同步特有）
    /// </summary>
    public void HandlePlayerEnterRoom(EnterRoomNotification playerEnterInfo)
    {
        try
        {
            // 从EnterRoomNotification中解析玩家信息
            string newPlayerName = "未知玩家";
            string newPlayerAvatar = "";

            if (playerEnterInfo.playerInfo != null && !string.IsNullOrEmpty(playerEnterInfo.playerInfo.customProperties))
            {
                try
                {
                    // 使用PlayerCustomProperties类反序列化customProperties
                    var playerCustomProps = JsonMapper.ToObject<PlayerCustomProperties>(playerEnterInfo.playerInfo.customProperties);

                    if (!string.IsNullOrEmpty(playerCustomProps.playerName))
                    {
                        newPlayerName = playerCustomProps.playerName;
                    }

                    if (!string.IsNullOrEmpty(playerCustomProps.avatarUrl))
                    {
                        newPlayerAvatar = playerCustomProps.avatarUrl;
                    }
                }
                catch (System.Exception parseEx)
                {
                    Debug.LogError($"解析玩家customProperties失败: {parseEx.Message}");
                }
            }

            // 添加到房间玩家列表（新进入的玩家不是房主）
            PlayerDisplayInfo newPlayer = new PlayerDisplayInfo(newPlayerName, newPlayerAvatar, playerEnterInfo.playerInfo.id, false);
            if (!IsPlayerInList(newPlayerName))
            {
                roomPlayerList.Add(newPlayer);
                Debug.Log($"玩家 {newPlayerName} 进入房间，当前玩家数: {roomPlayerList.Count}");

                // 判断当前游戏状态
                if (currentState == GameState.InBattle)
                {
                    // ========== 战斗中：动态创建玩家对象 ==========
                    Debug.Log($"🎮 [战斗中] 检测到新玩家加入，在战斗界面创建玩家对象");

                    BattleInteraction battleInteraction = FindObjectOfType<BattleInteraction>();
                    if (battleInteraction != null)
                    {
                        // 尝试从UIManager缓存中获取头像
                        UIManager uiManager = GetUIManager();
                        Sprite avatarSprite = null;

                        if (uiManager != null && !string.IsNullOrEmpty(newPlayerAvatar))
                        {
                            avatarSprite = uiManager.GetCachedAvatar(newPlayerAvatar);
                            if (avatarSprite != null)
                            {
                                Debug.Log($"✅ 从UIManager缓存获取到头像");
                            }
                            else
                            {
                                Debug.Log($"⚠️ UIManager缓存中没有头像，将异步加载");
                            }
                        }

                        // 在战斗界面创建新玩家对象（传入avatarUrl用于异步加载）
                        battleInteraction.AddNewPlayer(playerEnterInfo.playerInfo.id, newPlayerName, avatarSprite, newPlayerAvatar);

                        Debug.Log($"✅ 战斗界面已添加新玩家: {newPlayerName}");
                    }
                    else
                    {
                        Debug.LogError("找不到BattleInteraction组件");
                    }
                }
                else
                {
                    // ========== 房间状态：更新房间UI ==========
                    UpdateUIPlayerList();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"处理玩家进入房间失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 处理玩家离开房间
    ///
    /// 注意：当前LeaveRoomNotification结构包含playerId字段
    /// 需要根据playerId从列表中移除对应玩家
    /// </summary>
    public void HandlePlayerLeaveRoom(LeaveRoomNotification playerLeaveInfo)
    {
        try
        {
            if (playerLeaveInfo == null || string.IsNullOrEmpty(playerLeaveInfo.playerId))
            {
                Debug.LogError("玩家离开房间事件数据无效");
                return;
            }

            string leftPlayerId = playerLeaveInfo.playerId;

            // 从房间玩家列表中移除（通过playerId匹配）
            bool removed = false;
            for (int i = roomPlayerList.Count - 1; i >= 0; i--)
            {
                if (roomPlayerList[i].playerId == leftPlayerId)
                {
                    string leftPlayerName = roomPlayerList[i].playerName;
                    roomPlayerList.RemoveAt(i);
                    removed = true;
                    Debug.Log($"玩家 {leftPlayerName} (ID:{leftPlayerId}) 离开房间");
                    break;
                }
            }

            if (!removed)
            {
                Debug.LogError($"未找到离开的玩家 ID:{leftPlayerId}");
            }

            // 更新UI
            UpdateUIPlayerList();
            Debug.Log($"当前房间玩家数: {roomPlayerList.Count}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"处理玩家离开房间失败: {e.Message}");
        }
    }

    /// <summary>
    /// 处理玩家被踢
    /// 所有玩家都会收到这个事件，从房间列表中移除被踢的玩家
    /// </summary>
    public void HandlePlayerKicked(PlayerKickedInfo kickedInfo)
    {
        try
        {
            if (kickedInfo == null || string.IsNullOrEmpty(kickedInfo.playerId))
            {
                Debug.LogError("玩家被踢事件数据无效");
                return;
            }

            string kickedPlayerId = kickedInfo.playerId;
            string myPlayerId = GetCurrentPlayerId();

            Debug.Log($"========== HandlePlayerKicked 开始 ==========");
            Debug.Log($"👢 被踢的玩家ID: {kickedPlayerId}");
            Debug.Log($"👤 我的玩家ID: {myPlayerId}");
            Debug.Log($"🔢 当前房间玩家数: {roomPlayerList.Count}");

            // 打印当前房间所有玩家
            for (int i = 0; i < roomPlayerList.Count; i++)
            {
                Debug.Log($"  玩家[{i}]: {roomPlayerList[i].playerName} (ID:{roomPlayerList[i].playerId})");
            }

            // 检查是否是自己被踢
            bool isMyselfKicked = kickedPlayerId == myPlayerId;
            Debug.Log($"🎯 是否是我被踢: {isMyselfKicked}");

            if (isMyselfKicked)
            {
                Debug.Log("⚠️ 确认：我被房主踢出房间，准备返回游戏大厅");

                // 清空房间玩家列表
                roomPlayerList.Clear();

                // 设置状态为ConnectingBattle（已连接多人联机服务，但不在房间中）
                SetState(GameState.ConnectingBattle);

                // 返回游戏大厅
                GetUIManager()?.ShowGameLobby();

                Debug.Log("========== HandlePlayerKicked 结束（我被踢） ==========");
                return;
            }

            // 其他玩家被踢，从房间玩家列表中移除
            Debug.Log($"📝 其他玩家被踢，从本地列表移除");
            bool removed = false;
            for (int i = roomPlayerList.Count - 1; i >= 0; i--)
            {
                if (roomPlayerList[i].playerId == kickedPlayerId)
                {
                    string kickedPlayerName = roomPlayerList[i].playerName;
                    roomPlayerList.RemoveAt(i);
                    removed = true;
                    Debug.Log($"✅ 已移除玩家: {kickedPlayerName} (ID:{kickedPlayerId})");
                    break;
                }
            }

            if (!removed)
            {
                Debug.LogWarning($"未在本地列表中��到被踢的玩家 ID:{kickedPlayerId}");
            }

            // 更新UI
            UpdateUIPlayerList();
            Debug.Log($"当前房间玩家数: {roomPlayerList.Count}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"处理玩家被踢失败: {e.Message}");
        }
    }
    
    /// <summary>
    /// 检查玩家是否已在列表中
    /// </summary>
    private bool IsPlayerInList(string playerName)
    {
        foreach (PlayerDisplayInfo player in roomPlayerList)
        {
            if (player.playerName == playerName)
            {
                return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// 更新UI玩家列表
    /// </summary>
    private void UpdateUIPlayerList()
    {
        GetUIManager()?.UpdatePlayerList(roomPlayerList);
    }
    

    /// <summary>
    /// 处理离开房间成功
    /// 清理房间数据并返回游戏大厅
    /// </summary>
    private void HandleLeaveRoomSuccess()
    {
        Debug.Log("处理离开房间成功事件");

        // 清理房间玩家列表
        roomPlayerList.Clear();
        
        // 清空房间ID
        currentRoomId = "";

        // 切换状态
        SetState(GameState.ConnectingBattle);

        // 返回游戏大厅UI
        UIManager uiManager = GetUIManager();
        if (uiManager != null)
        {
            uiManager.ShowGameLobby();
            Debug.Log("✅ 已返回游戏大厅");
        }
        else
        {
            Debug.LogError("找不到UIManager");
        }
    }

    #endregion

    #region 分享房间功能

    /// <summary>
    /// 分享当前房间
    /// </summary>
    public void ShareCurrentRoom()
    {
        Debug.Log("[Share] ========== Start Sharing Flow ==========");

        if (string.IsNullOrEmpty(currentRoomId))
        {
            Debug.LogError("[Share] Cannot share: not in room (currentRoomId is empty)");
            return;
        }

        Debug.Log($"[Share] Room ID: '{currentRoomId}'");
        Debug.Log($"[Share] Room ID length: {currentRoomId.Length}");
        Debug.Log($"[Share] Template ID: ST1760947538814BN3JL9Q07");

        // Register share message callback
        Tap.OnShareMessage(new OnShareOption
        {
            success = (res) =>
            {
                Debug.Log($"[OnShareMessage] Share message callback - Success");
                Debug.Log($"[OnShareMessage] errMsg: {res.errMsg}");
            },
            fail = (res) =>
            {
                Debug.LogError($"[OnShareMessage] Share message callback - Failed");
                Debug.LogError($"[OnShareMessage] errMsg: {res.errMsg}");
            }
        });

        Debug.Log("[Share] OnShareMessage callback registered");

        // Show share panel, pass room ID via sceneParam
        var shareOption = new ShowShareboardOption
        {
            templateId = "ST1760947538814BN3JL9Q07", // Actual share template ID
            sceneParam = currentRoomId, // Pass room ID as scene parameter
            success = (res) =>
            {
                Debug.Log($"[ShowShareboard] Share panel opened successfully");
                Debug.Log($"[ShowShareboard] errMsg: {res.errMsg}");
            },
            fail = (res) =>
            {
                Debug.LogError($"[ShowShareboard] Failed to open share panel");
                Debug.LogError($"[ShowShareboard] errNo: {res.errNo}");
                Debug.LogError($"[ShowShareboard] errMsg: {res.errMsg}");
            },
            complete = (res) =>
            {
                Debug.Log("[ShowShareboard] Share operation complete (complete callback)");
                Debug.Log($"[ShowShareboard] errMsg: {res.errMsg}");
            }
        };

        Debug.Log("[Share] Preparing to call Tap.ShowShareboard...");
        Debug.Log($"[Share] Parameters - templateId: {shareOption.templateId}");
        Debug.Log($"[Share] Parameters - sceneParam: '{shareOption.sceneParam}'");

        Tap.ShowShareboard(shareOption);

        Debug.Log("[Share] Tap.ShowShareboard called");
    }

    /// <summary>
    /// 加入分享的房间
    /// </summary>
    public void JoinSharedRoom(string roomId)
    {
        if (string.IsNullOrEmpty(roomId))
        {
            Debug.LogError("房间ID为空，无法加入");
            return;
        }

        Debug.Log($"正在加入分享的房间: {roomId}");

        var option = new JoinRoomOption
        {
            data = new JoinRoomRequest
            {
                roomId = roomId,
                playerCfg = new PlayerConfig
                {
                    customStatus = 0,
                    customProperties = JsonMapper.ToJson(new PlayerCustomProperties(playerName, playerAvatarUrl))
                }
            },
            success = (result) =>
            {
                Debug.Log($"✅ 加入房间成功！房间ID: {result.roomInfo.id}");
                SetState(GameState.InRoom);

                // 处理房间玩家信息
                HandleRoomPlayersInfo(result.roomInfo);
            },
            fail = (result) =>
            {
                Debug.LogError($"❌ 加入房间失败: {result.errMsg} (错误码: {result.errNo})");
                
                // 加入失败后显示提示
                ShowToastOption toastOption = new ShowToastOption
                {
                    title = $"加入房间失败: {result.errMsg}",
                    duration = 2
                };
                Tap.ShowToast(toastOption);
            },
            complete = (result) =>
            {
                Debug.Log("加入房间操作完成");
            }
        };

        TapBattleClient.JoinRoom(option);
    }

    /// <summary>
    /// 获取当前房间ID
    /// </summary>
    public string GetCurrentRoomId()
    {
        return currentRoomId;
    }

    #endregion

    // ========================================
    // 注意：状态同步相关方法已移至 StateSyncManager.cs
    // 帧同步相关方法已移至 FrameSyncManager.cs
    // ========================================

}
