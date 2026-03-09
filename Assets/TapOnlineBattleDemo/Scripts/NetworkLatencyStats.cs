using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 网络延迟统计管理类
/// 功能：
/// 1. 定时发送ping测试消息（每1秒一次）
/// 2. 记录待确认的ping消息
/// 3. 收到ping返回时计算往返延迟(RTT)
/// 4. 统计当前延迟、平均延迟、最低延迟、最高延迟
/// 5. 提供格式化的统计文本输出
/// 
/// 使用方式：
/// NetworkLatencyStats.Instance.StartPingTest(); // 开始测试
/// NetworkLatencyStats.Instance.OnPingReceived(pingId, timestamp); // 收到ping返回
/// string stats = NetworkLatencyStats.Instance.GetLatencyText(); // 获取统计文本
/// </summary>
public class NetworkLatencyStats : MonoBehaviour
{
    #region 单例模式
    
    private static NetworkLatencyStats _instance;
    public static NetworkLatencyStats Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("NetworkLatencyStats");
                _instance = go.AddComponent<NetworkLatencyStats>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    
    #endregion
    
    #region 统计变量
    
    private int nextPingId = 1;                                     // 下一个ping消息的ID（自增）
    private Dictionary<int, long> pendingPings = new Dictionary<int, long>(); // 待确认的ping消息 <pingId, 发送时间戳>
    
    private long currentLatency = 0;                                // 当前延迟（ms）
    private float averageLatency = 0f;                              // 平均延迟（ms）
    private long minLatency = long.MaxValue;                        // 最低延迟（ms）
    private long maxLatency = 0;                                    // 最高延迟（ms）
    private List<long> latencyHistory = new List<long>();           // 历史延迟记录
    
    private bool isTesting = false;                                 // 是否正在测试
    private Coroutine pingCoroutine = null;                         // ping发送协程引用
    
    #endregion
    
    #region 事件
    
    /// <summary>
    /// 统计数据更新事件（每收到一个ping返回触发一次）
    /// </summary>
    public event Action OnStatsUpdated;
    
    #endregion
    
    #region 生命周期
    
    void Awake()
    {
        // 确保单例唯一性
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
    
    #endregion
    
    #region 公开方法
    
    /// <summary>
    /// 开始ping测试
    /// </summary>
    public void StartPingTest()
    {
        if (isTesting)
        {
            Debug.LogWarning("⚠️ 延迟测试已在进行中");
            return;
        }
        
        Debug.Log("🎯 开始网络延迟测试");
        isTesting = true;
        
        // 重置统计数据
        ResetStats();
        
        // 启动ping发送协程
        if (pingCoroutine != null)
        {
            StopCoroutine(pingCoroutine);
        }
        pingCoroutine = StartCoroutine(PingCoroutine());
    }
    
    /// <summary>
    /// 停止ping测试
    /// </summary>
    public void StopPingTest()
    {
        if (!isTesting)
        {
            return;
        }
        
        Debug.Log("🛑 停止网络延迟测试");
        isTesting = false;
        
        if (pingCoroutine != null)
        {
            StopCoroutine(pingCoroutine);
            pingCoroutine = null;
        }
        
        // 清空待确认ping
        pendingPings.Clear();
    }
    
    /// <summary>
    /// 收到ping返回（由BattleInteraction调用）
    /// </summary>
    /// <param name="pingId">ping消息ID</param>
    /// <param name="sendTimestamp">发送时的时间戳</param>
    public void OnPingReceived(int pingId, long sendTimestamp)
    {
        // 检查是否是待确认的ping
        if (!pendingPings.ContainsKey(pingId))
        {
            Debug.LogWarning($"⚠️ 收到未知的ping返回: pingId={pingId}");
            return;
        }
        
        // 计算往返延迟 RTT = 当前时间 - 发送时间
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        long rtt = now - sendTimestamp;
        
        // 记录延迟
        currentLatency = rtt;
        latencyHistory.Add(rtt);
        
        // 更新统计数据
        UpdateStats();
        
        // 从待确认列表中移除
        pendingPings.Remove(pingId);
        
        // 输出日志
        Debug.Log($"📊 网络延迟 [PingID:{pingId}] - RTT: {rtt}ms | 平均: {averageLatency:F1}ms | 最低: {minLatency}ms | 最高: {maxLatency}ms");
        
        // 触发更新事件
        OnStatsUpdated?.Invoke();
    }
    
    /// <summary>
    /// 重置所有统计数据
    /// </summary>
    public void ResetStats()
    {
        nextPingId = 1;
        pendingPings.Clear();
        currentLatency = 0;
        averageLatency = 0f;
        minLatency = long.MaxValue;
        maxLatency = 0;
        latencyHistory.Clear();
        
        Debug.Log("📊 延迟统计数据已重置");
        
        // 触发更新事件
        OnStatsUpdated?.Invoke();
    }
    
    /// <summary>
    /// 获取延迟统计的格式化文本
    /// 格式：网络延迟 | 当前: XXms | 平均: XXms | 最低: XXms | 最高: XXms
    /// </summary>
    public string GetLatencyText()
    {
        if (latencyHistory.Count == 0)
        {
            return "网络延迟 | 等待数据...";
        }
        
        return $"网络延迟 | 当前: {currentLatency}ms | 平均: {averageLatency:F0}ms | 最低: {minLatency}ms | 最高: {maxLatency}ms";
    }
    
    /// <summary>
    /// 获取当前延迟
    /// </summary>
    public long GetCurrentLatency()
    {
        return currentLatency;
    }
    
    /// <summary>
    /// 获取平均延迟
    /// </summary>
    public float GetAverageLatency()
    {
        return averageLatency;
    }
    
    /// <summary>
    /// 获取最低延迟
    /// </summary>
    public long GetMinLatency()
    {
        return minLatency == long.MaxValue ? 0 : minLatency;
    }
    
    /// <summary>
    /// 获取最高延迟
    /// </summary>
    public long GetMaxLatency()
    {
        return maxLatency;
    }
    
    /// <summary>
    /// 获取下一个ping消息并记录（由TapSDKService调用）
    /// </summary>
    /// <returns>PingData对象，包含pingId、timestamp、senderId</returns>
    public PingData GetNextPing(string senderId)
    {
        int pingId = nextPingId++;
        long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        
        // 记录待确认的ping
        pendingPings[pingId] = timestamp;
        
        // 限制待确认ping数量（防止内存泄漏）
        if (pendingPings.Count > 100)
        {
            Debug.LogWarning("⚠️ 待确认ping数量过多，清理旧数据");
            CleanOldPendingPings();
        }
        
        return new PingData(pingId, timestamp, senderId);
    }
    
    #endregion
    
    #region 私有方法
    
    /// <summary>
    /// Ping发送协程：每1秒发送一次ping
    /// </summary>
    private IEnumerator PingCoroutine()
    {
        while (isTesting)
        {
            // 调用TapSDKService发送ping
            TapSDKService.Instance?.SendPingTest();
            
            // 等待1秒
            yield return new WaitForSeconds(1f);
        }
    }
    
    /// <summary>
    /// 更新统计数据（平均值、最低值、最高值）
    /// </summary>
    private void UpdateStats()
    {
        if (latencyHistory.Count == 0)
        {
            return;
        }
        
        // 计算平均延迟
        long sum = 0;
        long min = long.MaxValue;
        long max = 0;
        
        foreach (long latency in latencyHistory)
        {
            sum += latency;
            if (latency < min)
            {
                min = latency;
            }
            if (latency > max)
            {
                max = latency;
            }
        }
        
        averageLatency = (float)sum / latencyHistory.Count;
        minLatency = min;
        maxLatency = max;
    }
    
    /// <summary>
    /// 清理超时的待确认ping（超过10秒未收到返回）
    /// </summary>
    private void CleanOldPendingPings()
    {
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        List<int> toRemove = new List<int>();
        
        foreach (var kvp in pendingPings)
        {
            if (now - kvp.Value > 10000) // 超过10秒
            {
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (int pingId in toRemove)
        {
            pendingPings.Remove(pingId);
            Debug.LogWarning($"⚠️ 清理超时ping: pingId={pingId}");
        }
    }
    
    #endregion
}

