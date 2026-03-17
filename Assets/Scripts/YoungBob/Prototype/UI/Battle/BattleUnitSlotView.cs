using UnityEngine;
using UnityEngine.UI;
using YoungBob.Prototype.Battle;
using System.Text;
using System.Collections.Generic;

namespace YoungBob.Prototype.UI.Battle
{
    public enum SlotHighlightMode
    {
        None,
        Potential, // Card can target this unit
        Selected   // Card is currently targeting this unit (hovered)
    }

    internal sealed class BattleUnitSlotView : MonoBehaviour
    {
        private Image _background;
        private Image _highlightBorder;
        private RectTransform _hpFillRect;
        private Image _hpFillImage;
        private Text _nameLabel;
        private Text _hpLabel;
        private Text _armorLabel;
        private Color _baseColor;

        private Text _statusLabel;

        public string UnitId { get; private set; }
        public BattleTargetFaction Faction { get; private set; }
        public bool IsAlive { get; private set; }

        public void Initialize(Image background, Text nameLabel, Text hpLabel, RectTransform hpFillRect, Text armorLabel, Text statusLabel, Color baseColor, Image highlightBorder)
        {
            _background = background;
            _nameLabel = nameLabel;
            _hpLabel = hpLabel;
            _hpFillRect = hpFillRect;
            if (_hpFillRect != null) _hpFillImage = _hpFillRect.GetComponent<Image>();
            _armorLabel = armorLabel;
            _statusLabel = statusLabel;
            _baseColor = baseColor;
            _highlightBorder = highlightBorder;
            if (_highlightBorder != null) _highlightBorder.gameObject.SetActive(false);
        }

        public void SetData(BattleTargetFaction faction, string unitId, string displayName, int hp, int maxHp, int armor, int charge, int bonus, int vulnerableStacks, List<BattleStatusState> statuses, bool detailedMode, SlotHighlightMode highlightMode)
        {
            Faction = faction;
            UnitId = unitId;
            IsAlive = hp > 0;
            
            _nameLabel.text = displayName;
            _hpLabel.text = $"{hp} / {maxHp}";
            
            if (_hpFillRect != null)
            {
                float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
                _hpFillRect.anchorMax = new Vector2(ratio, 1f);
                
                if (_hpFillImage != null)
                {
                    _hpFillImage.color = faction == BattleTargetFaction.Allies ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.8f, 0.2f, 0.2f);
                }
            }

            if (_armorLabel != null)
            {
                _armorLabel.text = armor > 0 ? (detailedMode ? "护甲 " + armor : "🛡️ " + armor) : "";
                _armorLabel.gameObject.SetActive(armor > 0);
            }

            if (_statusLabel != null)
            {
                var status = BuildStatusText(charge, bonus, vulnerableStacks, statuses, detailedMode);
                _statusLabel.text = status;
                _statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(status) && IsAlive);
            }
            
            if (IsAlive)
            {
                _background.color = _baseColor;
                _nameLabel.color = Color.white;
                _hpLabel.color = Color.white;
                if (_armorLabel != null) 
                {
                    _armorLabel.color = new Color(0.6f, 0.8f, 1f);
                    _armorLabel.gameObject.SetActive(armor > 0);
                }
                if (_hpFillRect != null && _hpFillRect.parent != null) _hpFillRect.parent.gameObject.SetActive(true);
                
                var cg = GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1.0f;
            }
            else
            {
                _background.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
                _nameLabel.color = new Color(0.4f, 0.4f, 0.4f);
                _hpLabel.color = new Color(0.4f, 0.4f, 0.4f);
                _hpLabel.text = "已阵亡";
                
                if (_armorLabel != null) _armorLabel.gameObject.SetActive(false);
                if (_statusLabel != null) _statusLabel.gameObject.SetActive(false);
                if (_hpFillRect != null && _hpFillRect.parent != null) _hpFillRect.parent.gameObject.SetActive(false);
                
                var cg = GetComponent<CanvasGroup>();
                if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0.6f;
            }

            if (_highlightBorder != null)
            {
                bool showHighlight = IsAlive && highlightMode != SlotHighlightMode.None;
                _highlightBorder.gameObject.SetActive(showHighlight);
                if (showHighlight)
                {
                    _highlightBorder.color = highlightMode == SlotHighlightMode.Selected ? new Color(1f, 0.85f, 0f, 1f) : new Color(1f, 1f, 1f, 0.4f);
                }
            }
        }

        private static string BuildStatusText(int charge, int bonus, int vulnerableStacks, List<BattleStatusState> statuses, bool detailedMode)
        {
            var sb = new StringBuilder();
            if (charge > 0) sb.Append(detailedMode ? "蓄力" : "⚡").Append(charge).Append(' ');
            if (bonus > 0) sb.Append(detailedMode ? "增伤+" : "💥+").Append(bonus).Append(' ');
            if (vulnerableStacks > 0) sb.Append(detailedMode ? "易伤" : "🎯").Append(vulnerableStacks).Append(' ');

            if (statuses != null)
            {
                for (var i = 0; i < statuses.Count; i++)
                {
                    var status = statuses[i];
                    if (status == null || status.stacks <= 0 || string.IsNullOrWhiteSpace(status.id))
                    {
                        continue;
                    }

                    if (string.Equals(status.id, "Poison", System.StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append(detailedMode ? "中毒" : "☠").Append(status.stacks).Append(' ');
                    }
                    else if (string.Equals(status.id, "Strength", System.StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append(detailedMode ? "力量" : "💪").Append(status.stacks).Append(' ');
                    }
                    else
                    {
                        sb.Append(status.id).Append(':').Append(status.stacks).Append(' ');
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }
    }
}
