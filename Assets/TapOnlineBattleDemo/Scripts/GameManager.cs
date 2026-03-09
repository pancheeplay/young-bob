using System;
using System.Collections;
using System.Collections.Generic;
using TapTapMiniGame;
using UnityEngine;
using LitJson;

/// <summary>
/// 多人联机游戏主管理器
/// 功能：游戏入口，单例模式，控制整个游戏流程
/// 流程：TapSDK初始化 -> 登录 -> 获取用户信息 -> 多人联机连接 -> 开始游戏
/// </summary>
public class GameManager : MonoBehaviour
{
    #region 单例模式
    
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("GameManager");
                    _instance = go.AddComponent<GameManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }
    
    #endregion
    
    #region 启动参数

    private string sharedRoomId = "";  // 从分享链接获取的房间ID

    #endregion
    
    #region Unity生命周期
    
    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("GameManager初始化完成");
            
            // 注册OnShow回调，接收启动参数
            RegisterOnShowCallback();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        // 冷启动：获取启动参数
        CheckLaunchOptions();

        StartCoroutine(InitializeGameFlow());
    }
    
    #region 游戏流程控制
    
    /// <summary>
    /// 初始化游戏流程
    /// </summary>
    public IEnumerator InitializeGameFlow()
    {
        Debug.Log("InitializeGameFlow start");
        
#if (UNITY_WEBGL || UNITY_MINIGAME) && UNITY_EDITOR && TAP_DEBUG_ENABLE
        // 在 Unity Editor 调试模式下，等待调试服务器客户端连接
        Debug.Log("[GameManager] 等待调试服务器客户端连接...");
        var serverModule = TapServer.NetworkServerModule.Instance;
        if (serverModule != null)
        {
            yield return serverModule.WaitForClientConnected(timeout: 300f);
        }
        else
        {
            Debug.LogWarning("[GameManager] 调试服务器模块未找到，跳过等待");
        }
        Debug.Log("[GameManager] 等待调试服务器客户端连接...结束");
        
        // 调试模式下额外等待5秒，确保调试连接稳定
        yield return new WaitForSeconds(5);
#endif

        Debug.Log("[GameManager] 多人联机流程开始");
        
        // 1. 初始化TapSDK
        // yield return StartCoroutine(TapSDKService.Instance.InitializeTapSDK());
        // if (TapSDKService.Instance.CurrentState == TapSDKService.GameState.Error) yield break;
        
        // 2. 用户登录
        yield return StartCoroutine(TapSDKService.Instance.LoginUser());
        if (TapSDKService.Instance.CurrentState == TapSDKService.GameState.Error) yield break;
        
        // 3. 获取用户信息
        yield return StartCoroutine(TapSDKService.Instance.LoadUserInfo());
        if (TapSDKService.Instance.CurrentState == TapSDKService.GameState.Error) yield break;
        
        // 4. 初始化多人联机
        yield return StartCoroutine(TapSDKService.Instance.InitializeBattle());
        if (TapSDKService.Instance.CurrentState == TapSDKService.GameState.Error) yield break;
        
        // 5. 连接多人联机服务
        yield return StartCoroutine(TapSDKService.Instance.ConnectToBattleService());
        if (TapSDKService.Instance.CurrentState == TapSDKService.GameState.Error) yield break;

        Debug.Log("InitializeGameFlow end");

        // 等待1秒后执行OnGameReady
        yield return new WaitForSeconds(1f);
        yield return StartCoroutine(OnGameReady());
    }

    /// <summary>
    /// 游戏准备完成（仅冷启动时调用）
    /// </summary>
    private IEnumerator OnGameReady()
    {
        Debug.Log("[OnGameReady] Game ready, can start matching room");

        // 通知UIManager显示游戏大厅UI
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.OnSDKInitialized();
        }

        // 等���1秒后检查分享房间ID
        yield return new WaitForSeconds(1f);

        // 检查是否有从分享链接获取的房间ID
        if (!string.IsNullOrEmpty(sharedRoomId))
        {
            Debug.Log($"[OnGameReady] Detected shared room ID from cold start: {sharedRoomId}");
            ShowJoinRoomDialog();
        }
    }
    
    #endregion
    
    #region 分享功能 - 启动参数处理

    /// <summary>
    /// 检查启动参数（冷启动）
    /// </summary>
    private void CheckLaunchOptions()
    {
#if !UNITY_EDITOR
        Debug.Log("[LaunchOptions] Checking launch options (Cold Start)");

        try
        {
            var options = Tap.GetLaunchOptionsSync();
            Debug.Log($"[LaunchOptions] GetLaunchOptionsSync result is null: {options == null}");

            if (options != null)
            {
                Debug.Log($"[LaunchOptions] options.query is null: {options.query == null}");

                if (options.query != null)
                {
                    Debug.Log($"[LaunchOptions] options.query.Count: {options.query.Count}");

                    // Log all query parameters
                    foreach (var kvp in options.query)
                    {
                        Debug.Log($"[LaunchOptions] Query Parameter - Key: {kvp.Key}, Value: {kvp.Value}");
                    }

                    // Try to get sceneParam
                    string sceneParam;
                    if (options.query.TryGetValue("sceneParam", out sceneParam))
                    {
                        Debug.Log($"[LaunchOptions] sceneParam raw value: '{sceneParam}'");
                        Debug.Log($"[LaunchOptions] sceneParam is empty: {string.IsNullOrEmpty(sceneParam)}");
                        Debug.Log($"[LaunchOptions] sceneParam length: {sceneParam?.Length ?? 0}");

                        if (!string.IsNullOrEmpty(sceneParam))
                        {
                            sharedRoomId = sceneParam;
                            Debug.Log($"[LaunchOptions] Successfully got shared room ID: {sharedRoomId}");
                        }
                        else
                        {
                            Debug.LogWarning("[LaunchOptions] sceneParam exists but is empty string");
                        }
                    }
                    else
                    {
                        Debug.Log("[LaunchOptions] options.query does not contain sceneParam");
                    }
                }
                else
                {
                    Debug.LogWarning("[LaunchOptions] options.query is null");
                }
            }
            else
            {
                Debug.Log("[LaunchOptions] No launch options available");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[LaunchOptions] Exception when getting launch options: {e.Message}");
            Debug.LogError($"[LaunchOptions] Exception stack trace: {e.StackTrace}");
        }
#else
        Debug.Log("[LaunchOptions] Unity Editor environment, skip launch options check");
#endif
    }

    /// <summary>
    /// 注册OnShow回调，接收启动参数（包括分享传递的房间ID）- 热启动
    /// </summary>
    private void RegisterOnShowCallback()
    {
#if !UNITY_EDITOR
        Debug.Log("[OnShow] Starting to register OnShow callback (Runtime Environment)");

        Tap.OnShow((OnShowListenerResult res) =>
        {
            Debug.Log("[OnShow] ========== OnShow Event Triggered (Hot Start) ==========");

            try
            {
                // 1. Log complete startup parameters
                Debug.Log($"[OnShow] res is null: {res == null}");

                if (res != null)
                {
                    Debug.Log($"[OnShow] res.query is null: {res.query == null}");

                    if (res.query != null)
                    {
                        Debug.Log($"[OnShow] res.query.Count: {res.query.Count}");

                        // Log all query parameters
                        foreach (var kvp in res.query)
                        {
                            Debug.Log($"[OnShow] Query Parameter - Key: {kvp.Key}, Value: {kvp.Value}");
                        }

                        // 2. Check sceneParam (shared room ID)
                        if (res.query.ContainsKey("sceneParam"))
                        {
                            string sceneParam = res.query["sceneParam"];
                            Debug.Log($"[OnShow] sceneParam raw value: '{sceneParam}'");
                            Debug.Log($"[OnShow] sceneParam is empty: {string.IsNullOrEmpty(sceneParam)}");
                            Debug.Log($"[OnShow] sceneParam length: {sceneParam?.Length ?? 0}");

                            if (!string.IsNullOrEmpty(sceneParam))
                            {
                                sharedRoomId = sceneParam;
                                Debug.Log($"[OnShow] Successfully got shared room ID: {sharedRoomId}");

                                // 热启动时直接显示加入房间弹窗（此时游戏已经初始化完成）
                                ShowJoinRoomDialog();
                            }
                            else
                            {
                                Debug.LogWarning("[OnShow] sceneParam exists but is empty string");
                            }
                        }
                        else
                        {
                            Debug.Log("[OnShow] res.query does not contain sceneParam");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[OnShow] res.query is null");
                    }
                }
                else
                {
                    Debug.LogError("[OnShow] res object is null");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[OnShow] Exception when handling OnShow callback: {e.Message}");
                Debug.LogError($"[OnShow] Exception stack trace: {e.StackTrace}");
            }

            Debug.Log("[OnShow] ========== OnShow Processing Complete ==========");
        });

        Debug.Log("[OnShow] OnShow callback registered successfully");
#else
        Debug.Log("[OnShow] Unity Editor environment, skip OnShow callback registration");
#endif
    }

    /// <summary>
    /// 显示加入房间对话框
    /// </summary>
    private void ShowJoinRoomDialog()
    {
        if (string.IsNullOrEmpty(sharedRoomId))
        {
            Debug.LogWarning("[ShowJoinRoomDialog] sharedRoomId is empty, skip");
            return;
        }

        Debug.Log($"[ShowJoinRoomDialog] Showing join room dialog for room: {sharedRoomId}");

        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ShowJoinRoomConfirmDialog(sharedRoomId);

            // 清空sharedRoomId，避免重复处理
            string roomIdCopy = sharedRoomId;
            sharedRoomId = "";

            Debug.Log($"[ShowJoinRoomDialog] Dialog shown for room: {roomIdCopy}, sharedRoomId cleared");
        }
        else
        {
            Debug.LogError("[ShowJoinRoomDialog] UIManager not found!");
        }
    }

    #endregion
    
    #endregion 
}