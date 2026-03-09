using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家组件
/// 功能：显示用户头像和昵称，用于UGUI实现
/// </summary>
public class PlayerComponent : MonoBehaviour
{
    [Header("UI组件")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Text ownerText;
    [SerializeField] private Button kickButton;
    [SerializeField] private Mask avatarMask;  // 头像裁剪组件（仅战斗中启用）

    /// <summary>
    /// 当前玩家ID（用于踢人）
    /// </summary>
    private string currentPlayerId;

    private void Awake()
    {
        // 绑定踢人按钮事件
        if (kickButton != null)
        {
            kickButton.onClick.AddListener(OnKickButtonClicked);
        }

        // 默认禁用头像裁剪（房间界面不裁剪）
        if (avatarMask != null)
        {
            avatarMask.enabled = false;
        }
    }

    /// <summary>
    /// 设置玩家信息
    /// </summary>
    /// <param name="playerName">玩家昵称</param>
    /// <param name="avatarSprite">头像图片（可选）</param>
    /// <param name="isOwner">是否为房主</param>
    /// <param name="playerId">玩家ID</param>
    /// <param name="isCurrentPlayerOwner">当前用户是否为房主</param>
    public void SetPlayerInfo(string playerName, Sprite avatarSprite = null, bool isOwner = false, string playerId = "", bool isCurrentPlayerOwner = false)
    {
        // 保存玩家ID
        currentPlayerId = playerId;

        if (nameText != null)
        {
            // 只设置玩家昵称,不包含房主标识
            nameText.text = playerName;
        }

        // 控制房主标识文本的显示
        if (ownerText != null)
        {
            ownerText.gameObject.SetActive(isOwner);
        }

        if (avatarImage != null && avatarSprite != null)
        {
            avatarImage.sprite = avatarSprite;
        }

        // 控制踢人按钮显示：当前玩家是房主，并且这不是房主自己的组件
        if (kickButton != null)
        {
            bool shouldShowKick = isCurrentPlayerOwner && !isOwner;
            kickButton.gameObject.SetActive(shouldShowKick);
        }
    }

    /// <summary>
    /// 踢人按钮点击事件
    /// </summary>
    private void OnKickButtonClicked()
    {
        if (string.IsNullOrEmpty(currentPlayerId))
        {
            Debug.LogError("无效的玩家ID,无法踢人");
            return;
        }

        Debug.Log($"房主踢人: {currentPlayerId}");
        TapSDKService.Instance.KickPlayer(currentPlayerId);
    }
    
    /// <summary>
    /// 设置头像颜色（当没有头像图片时）
    /// </summary>
    /// <param name="color">头像颜色</param>
    public void SetAvatarColor(Color color)
    {
        if (avatarImage != null)
        {
            avatarImage.color = color;
        }
    }
    
    /// <summary>
    /// 只设置头像图片（不改变其他信息）
    /// </summary>
    /// <param name="avatarSprite">头像图片</param>
    public void SetAvatarSprite(Sprite avatarSprite)
    {
        if (avatarImage != null && avatarSprite != null)
        {
            avatarImage.sprite = avatarSprite;
            // 重置颜色为白色，让图片正常显示
            avatarImage.color = Color.white;
        }
    }

    /// <summary>
    /// 设置房间列表模式（放大头像2.6倍）
    /// </summary>
    public void SetRoomListMode()
    {
        if (avatarMask != null)
        {
            // avatarMask.transform.localScale = Vector3.one * 2.6f;
        }
    }

    /// <summary>
    /// 启用战斗模式（开启头像裁剪,恢复原始大小）
    /// </summary>
    public void EnableBattleMode()
    {
        if (avatarMask != null)
        {
            // 恢复原始大小
            // avatarMask.transform.localScale = Vector3.one;

            // 启用头像裁剪
            avatarMask.enabled = true;
        }
    }
}