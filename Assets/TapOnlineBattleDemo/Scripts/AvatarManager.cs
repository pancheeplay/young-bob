using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 头像管理器 - 统一管理头像下载和缓存
///
/// 设计目的：
/// 消除重复代码，原本有3处头像加载逻辑（UIManager、BattleInteraction、RoomItemUI），
/// 现在统一为1个管理器，所有组件调用同一接口。
///
/// 核心功能：
/// 1. 异步下载头像（UnityWebRequest）
/// 2. 全局缓存机制（Dictionary<url, Sprite>）
/// 3. 自动检查缓存，避免重复下载
/// 4. 回调模式，使用简单
///
/// 使用示例：
/// <code>
/// // 基本用法
/// AvatarManager.Instance.LoadAvatar(url,
///     (sprite) => {
///         // 成功回调
///         playerComponent.SetAvatarSprite(sprite);
///     },
///     () => {
///         // 失败回调（可选）
///         UseDefaultAvatar();
///     }
/// );
///
/// // 检查缓存
/// if (AvatarManager.Instance.HasCachedAvatar(url)) {
///     Sprite sprite = AvatarManager.Instance.GetCachedAvatar(url);
/// }
/// </code>
///
/// 优势：
/// - ✅ 统一管理，避免代码重复
/// - ✅ 全局缓存，节省带宽（同一URL只下载一次）
/// - ✅ 使用简单，回调模式易于理解
/// - ✅ 易于复用到其他项目
///
/// 技术细节：
/// - 使用UnityWebRequestTexture下载图片
/// - 超时时间：10秒
/// - 单例模式，全局唯一实例
/// - 自动创建GameObject，DontDestroyOnLoad
/// </summary>
public class AvatarManager : MonoBehaviour
{
    #region 单例模式

    private static AvatarManager _instance;
    public static AvatarManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<AvatarManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("AvatarManager");
                    _instance = go.AddComponent<AvatarManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    #endregion

    // 全局头像缓存
    private Dictionary<string, Sprite> avatarCache = new Dictionary<string, Sprite>();

    /// <summary>
    /// 加载头像（带自动缓存）
    ///
    /// 这是唯一的头像加载入口，所有需要显示头像的地方都应该调用此方法
    ///
    /// 执行流程：
    /// 1. 检查URL是否为空 → 为空则调用失败回调
    /// 2. 检查缓存（Dictionary） → 有缓存则立即返回，无需下载
    /// 3. 启动异步下载（UnityWebRequest）
    /// 4. 下载成功：
    ///    - 创建Sprite对象
    ///    - 保存到缓存字典
    ///    - 调用成功回调
    /// 5. 下载失败：
    ///    - 调用失败回调
    ///
    /// 性能优化：
    /// - ✅ 全局缓存，同一URL只下载一次
    /// - ✅ 第二次加载同一URL，立即返回（无网络请求）
    /// - ✅ 超时保护，10秒未完成自动失败
    ///
    /// 使用场景：
    /// - 房间UI中的玩家头像
    /// - 战斗UI中的玩家头像
    /// - 房间列表中的房主头像
    /// - 任何需要显示用户头像的地方
    /// </summary>
    /// <param name="avatarUrl">头像URL（必须是有效的http/https地址）</param>
    /// <param name="onSuccess">成功回调，参数为下载完成的Sprite</param>
    /// <param name="onFail">失败回调（可选），下载失败或超时时调用</param>
    public void LoadAvatar(string avatarUrl, Action<Sprite> onSuccess, Action onFail = null)
    {
        // 1. URL验证
        if (string.IsNullOrEmpty(avatarUrl))
        {
            Debug.LogWarning("头像URL为空");
            onFail?.Invoke();
            return;
        }

        // 2. 检查缓存
        if (avatarCache.ContainsKey(avatarUrl))
        {
            Debug.Log($"[AvatarManager] 从缓存获取头像: {avatarUrl}");
            onSuccess?.Invoke(avatarCache[avatarUrl]);
            return;
        }

        // 3. 异步下载
        Debug.Log($"[AvatarManager] 开始下载头像: {avatarUrl}");
        StartCoroutine(DownloadAvatar(avatarUrl, onSuccess, onFail));
    }

    /// <summary>
    /// 异步下载头像
    /// </summary>
    private IEnumerator DownloadAvatar(string url, Action<Sprite> onSuccess, Action onFail)
    {
        using (UnityWebRequest webRequest = UnityWebRequestTexture.GetTexture(url))
        {
            // 设置超时时间（10秒）
            webRequest.timeout = 10;

            // 发送请求
            yield return webRequest.SendWebRequest();

            // 检查下载结果
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    // 获取下载的纹理
                    Texture2D texture = DownloadHandlerTexture.GetContent(webRequest);

                    if (texture != null)
                    {
                        // 将纹理转换为Sprite
                        Sprite avatarSprite = Sprite.Create(
                            texture,
                            new Rect(0, 0, texture.width, texture.height),
                            new Vector2(0.5f, 0.5f)
                        );

                        // 缓存头像
                        avatarCache[url] = avatarSprite;

                        Debug.Log($"[AvatarManager] ✅ 头像下载成功并已缓存: {url}");

                        // 成功回调
                        onSuccess?.Invoke(avatarSprite);
                    }
                    else
                    {
                        Debug.LogError($"[AvatarManager] ❌ 头像纹理为null: {url}");
                        onFail?.Invoke();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[AvatarManager] ❌ 处理头像失败: {e.Message}, URL: {url}");
                    onFail?.Invoke();
                }
            }
            else
            {
                Debug.LogError($"[AvatarManager] ❌ 头像下载失败: {webRequest.error}, URL: {url}");
                onFail?.Invoke();
            }
        }
    }

    /// <summary>
    /// 检查缓存中是否有指定URL的头像
    /// </summary>
    public bool HasCachedAvatar(string avatarUrl)
    {
        return !string.IsNullOrEmpty(avatarUrl) && avatarCache.ContainsKey(avatarUrl);
    }

    /// <summary>
    /// 获取缓存的头像（同步）
    /// </summary>
    public Sprite GetCachedAvatar(string avatarUrl)
    {
        if (HasCachedAvatar(avatarUrl))
        {
            return avatarCache[avatarUrl];
        }
        return null;
    }

    /// <summary>
    /// 清理所有缓存
    /// </summary>
    public void ClearAllCache()
    {
        foreach (var cachedSprite in avatarCache.Values)
        {
            if (cachedSprite != null && cachedSprite.texture != null)
            {
                Destroy(cachedSprite.texture);
                Destroy(cachedSprite);
            }
        }
        avatarCache.Clear();
        Debug.Log("[AvatarManager] 头像缓存已清理");
    }

    void OnDestroy()
    {
        ClearAllCache();
    }
}
