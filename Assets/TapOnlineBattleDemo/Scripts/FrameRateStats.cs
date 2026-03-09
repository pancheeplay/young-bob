using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 帧同步统计管理类
/// 功能：
/// 1. 统计每10秒收到的帧数据数量（包括空帧）
/// 2. 计算当前帧同步率、平均帧同步率、最低帧同步率
/// 3. 提供统计数据的文本格式化输出
/// 
/// 使用方式：
/// FrameRateStats.Instance.RecordFrame(); // 每收到一帧调用
/// string stats = FrameRateStats.Instance.GetStatsText(); // 获取统计文本
/// 
/// 注意：这里统计的是SDK帧同步的帧率（服务器发送帧的频率），不是游戏渲染帧率
/// </summary>
public class FrameRateStats : MonoBehaviour
{
    #region 单例模式
    
    private static FrameRateStats _instance;
    public static FrameRateStats Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("FrameRateStats");
                _instance = go.AddComponent<FrameRateStats>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    
    #endregion
    
    #region 统计变量
    
    private int frameCount = 0;                    // 当前10秒内收到的帧数
    private float currentRate = 0f;                // 当前帧同步率（最近10秒，帧/秒）
    private float averageRate = 0f;                // 平均帧同步率（所有统计周期）
    private float minRate = float.MaxValue;        // 最低帧同步率
    private List<float> rateHistory = new List<float>(); // 历史帧同步率记录
    
    private int latestFrameId = -1;                // 最新收到的帧ID
    private int displayFrameId = -1;               // 用于显示的帧ID（每秒更新一次）
    
    private bool isRecording = false;              // 是否正在记录
    private Coroutine statsCoroutine = null;       // 统计协程引用
    private Coroutine displayUpdateCoroutine = null; // 显示更新协程引用
    
    #endregion
    
    #region 事件
    
    /// <summary>
    /// 统计数据更新事件（每10秒触发一次）
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
    /// 记录收到一帧数据（包括空帧）
    /// </summary>
    /// <param name="frameId">帧ID（可选）</param>
    public void RecordFrame(int frameId = -1)
    {
        frameCount++;
        
        // 记录最新的帧ID
        if (frameId >= 0)
        {
            latestFrameId = frameId;
        }
        
        // 如果还没开始统计，启动统计协程
        if (!isRecording)
        {
            StartRecording();
        }
    }
    
    /// <summary>
    /// 开始记录统计
    /// </summary>
    public void StartRecording()
    {
        if (isRecording)
        {
            Debug.LogWarning("帧率统计已在进行中");
            return;
        }
        
        Debug.Log("🎯 开始帧同步统计");
        isRecording = true;
        frameCount = 0;
        latestFrameId = -1;
        displayFrameId = -1;
        
        // 启动统计协程
        if (statsCoroutine != null)
        {
            StopCoroutine(statsCoroutine);
        }
        statsCoroutine = StartCoroutine(StatsCoroutine());
        
        // 启动显示更新协程（每秒更新一次帧ID显示）
        if (displayUpdateCoroutine != null)
        {
            StopCoroutine(displayUpdateCoroutine);
        }
        displayUpdateCoroutine = StartCoroutine(DisplayUpdateCoroutine());
    }
    
    /// <summary>
    /// 停止记录统计
    /// </summary>
    public void StopRecording()
    {
        if (!isRecording)
        {
            return;
        }
        
        Debug.Log("🛑 停止帧同步统计");
        isRecording = false;
        
        if (statsCoroutine != null)
        {
            StopCoroutine(statsCoroutine);
            statsCoroutine = null;
        }
        
        if (displayUpdateCoroutine != null)
        {
            StopCoroutine(displayUpdateCoroutine);
            displayUpdateCoroutine = null;
        }
    }
    
    /// <summary>
    /// 重置所有统计数据
    /// </summary>
    public void ResetStats()
    {
        frameCount = 0;
        currentRate = 0f;
        averageRate = 0f;
        minRate = float.MaxValue;
        rateHistory.Clear();
        latestFrameId = -1;
        displayFrameId = -1;
        
        Debug.Log("📊 统计数据已重置");
        
        // 触发更新事件
        OnStatsUpdated?.Invoke();
    }
    
    /// <summary>
    /// 获取统计数据的格式化文本
    /// 格式：帧ID: XXX | 当前: XX 帧/秒 | 平均: XX 帧/秒 | 最低: XX 帧/秒
    /// </summary>
    public string GetStatsText()
    {
        if (rateHistory.Count == 0)
        {
            if (displayFrameId >= 0)
            {
                return $"帧ID: {displayFrameId} | 等待统计数据...";
            }
            return "等待数据...";
        }
        
        string frameIdText = displayFrameId >= 0 ? $"帧ID: {displayFrameId} | " : "";
        return $"{frameIdText}当前: {currentRate:F1} 帧/秒 | 平均: {averageRate:F1} 帧/秒 | 最低: {minRate:F1} 帧/秒";
    }
    
    /// <summary>
    /// 获取详细统计信息（用于调试）
    /// </summary>
    public string GetDetailedStats()
    {
        return $"帧同步统计:\n" +
               $"- 当前帧同步率: {currentRate:F1} 帧/秒\n" +
               $"- 平均帧同步率: {averageRate:F1} 帧/秒\n" +
               $"- 最低帧同步率: {minRate:F1} 帧/秒\n" +
               $"- 统计周期数: {rateHistory.Count}\n" +
               $"- 当前周期帧数: {frameCount}";
    }
    
    /// <summary>
    /// 获取当前帧同步率
    /// </summary>
    public float GetCurrentRate()
    {
        return currentRate;
    }
    
    /// <summary>
    /// 获取平均帧同步率
    /// </summary>
    public float GetAverageRate()
    {
        return averageRate;
    }
    
    /// <summary>
    /// 获取最低帧同步率
    /// </summary>
    public float GetMinRate()
    {
        return minRate == float.MaxValue ? 0f : minRate;
    }
    
    /// <summary>
    /// 获取当前显示的帧ID
    /// </summary>
    public int GetDisplayFrameId()
    {
        return displayFrameId;
    }
    
    #endregion
    
    #region 私有方法
    
    /// <summary>
    /// 统计协程：每10秒计算一次帧同步率
    /// </summary>
    private IEnumerator StatsCoroutine()
    {
        while (isRecording)
        {
            // 等待10秒
            yield return new WaitForSeconds(10f);
            
            // 计算当前帧同步率（帧数 / 10秒）
            currentRate = frameCount / 10f;
            
            // 记录到历史
            rateHistory.Add(currentRate);
            
            // 更新统计数据
            UpdateStats();
            
            // 输出日志
            Debug.Log($"📊 帧同步统计 [{rateHistory.Count}] - 当前: {currentRate:F1} 帧/秒 | 平均: {averageRate:F1} 帧/秒 | 最低: {minRate:F1} 帧/秒 | 10秒收帧: {frameCount}");
            
            // 重置帧计数
            frameCount = 0;
            
            // 触发更新事件
            OnStatsUpdated?.Invoke();
        }
    }
    
    /// <summary>
    /// 更新统计数据（平均值、最低值）
    /// </summary>
    private void UpdateStats()
    {
        if (rateHistory.Count == 0)
        {
            return;
        }
        
        // 计算平均帧同步率
        float sum = 0f;
        float min = float.MaxValue;
        
        foreach (float rate in rateHistory)
        {
            sum += rate;
            if (rate < min)
            {
                min = rate;
            }
        }
        
        averageRate = sum / rateHistory.Count;
        minRate = min;
    }
    
    /// <summary>
    /// 显示更新协程：每秒更新一次帧ID显示
    /// 目的：30帧/秒的更新频率太快看不清，所以每秒更新一次显示
    /// </summary>
    private IEnumerator DisplayUpdateCoroutine()
    {
        while (isRecording)
        {
            // 等待1秒
            yield return new WaitForSeconds(1f);
            
            // 更新显示的帧ID
            displayFrameId = latestFrameId;
            
            // 触发UI更新事件（让UI显示最新的帧ID）
            OnStatsUpdated?.Invoke();
        }
    }
    
    #endregion
}

