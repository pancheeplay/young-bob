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
        private RectTransform _threatFillRoot;
        private readonly Image[] _threatSegmentFills = new Image[3];
        private readonly Text[] _threatSegmentLabels = new Text[3];
        private Text _threatLabel;
        private Text _nameLabel;
        private Text _hpLabel;
        private Text _armorLabel;
        private Color _baseColor;

        private Text _statusLabel;

        public string UnitId { get; private set; }
        public BattleTargetFaction Faction { get; private set; }
        public bool IsAlive { get; private set; }

        public void Initialize(Image background, Text nameLabel, Text hpLabel, RectTransform hpFillRect, Text armorLabel, Text statusLabel, RectTransform threatFillRoot, Image[] threatSegmentFills, Text[] threatSegmentLabels, Text threatLabel, Color baseColor, Image highlightBorder)
        {
            _background = background;
            _nameLabel = nameLabel;
            _hpLabel = hpLabel;
            _hpFillRect = hpFillRect;
            if (_hpFillRect != null) _hpFillImage = _hpFillRect.GetComponent<Image>();
            _armorLabel = armorLabel;
            _statusLabel = statusLabel;
            _threatFillRoot = threatFillRoot;
            _threatLabel = threatLabel;
            for (var i = 0; i < _threatSegmentFills.Length; i++)
            {
                _threatSegmentFills[i] = threatSegmentFills != null && i < threatSegmentFills.Length ? threatSegmentFills[i] : null;
                _threatSegmentLabels[i] = threatSegmentLabels != null && i < threatSegmentLabels.Length ? threatSegmentLabels[i] : null;
            }
            _baseColor = baseColor;
            _highlightBorder = highlightBorder;
            if (_highlightBorder != null) _highlightBorder.gameObject.SetActive(false);
        }

        public void SetData(BattleTargetFaction faction, string unitId, string displayName, int hp, int maxHp, int armor, int charge, int bonus, int vulnerableStacks, List<BattleStatusState> statuses, int threatValue, int threatTier, string secretSummary, bool detailedMode, SlotHighlightMode highlightMode)
        {
            Faction = faction;
            UnitId = unitId;
            IsAlive = hp > 0;
            
            _nameLabel.text = displayName;
            _hpLabel.text = $"{hp} / {maxHp}";

            UpdateThreatVisuals(threatValue, threatTier);
            
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
                var status = BuildStatusText(charge, bonus, vulnerableStacks, statuses, secretSummary, detailedMode);
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
                if (_threatLabel != null)
                {
                    _threatLabel.gameObject.SetActive(false);
                }
                if (_threatFillRoot != null)
                {
                    _threatFillRoot.gameObject.SetActive(false);
                }
                
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

        private void UpdateThreatVisuals(int threatValue, int threatTier)
        {
            if (_threatFillRoot == null)
            {
                return;
            }

            _threatFillRoot.gameObject.SetActive(IsAlive);
            if (!IsAlive)
            {
                return;
            }

            var resolvedTier = ResolveThreatTier(threatValue, threatTier);
            var clampedValue = Mathf.Max(0, threatValue);
            var tierNames = new[] { "I", "II", "III" };
            var tierIndex = Mathf.Clamp(resolvedTier - 1, 0, 2);
            var tierTitle = tierNames[tierIndex];
            var valueLabel = clampedValue >= 60 ? "60+" : clampedValue + "/60";

            if (_threatLabel != null)
            {
                _threatLabel.gameObject.SetActive(true);
                _threatLabel.text = $"仇恨 {tierTitle}  {valueLabel}";
                _threatLabel.color = GetThreatTierColor(resolvedTier);
            }

            var segmentProgress = new[]
            {
                Mathf.Clamp01(clampedValue / 20f),
                Mathf.Clamp01((clampedValue - 20f) / 20f),
                Mathf.Clamp01((clampedValue - 40f) / 20f)
            };
            var currentTierIndex = Mathf.Clamp(resolvedTier - 1, 0, 2);

            for (var i = 0; i < _threatSegmentFills.Length; i++)
            {
                var fill = _threatSegmentFills[i];
                if (fill != null)
                {
                    fill.gameObject.SetActive(true);
                    fill.color = i <= currentTierIndex ? GetThreatTierColor(i + 1) : new Color(0.16f, 0.19f, 0.24f, 0.92f);
                    var fillRect = fill.rectTransform;
                    fillRect.anchorMin = Vector2.zero;
                    fillRect.anchorMax = new Vector2(segmentProgress[i], 1f);
                    fillRect.offsetMin = Vector2.zero;
                    fillRect.offsetMax = Vector2.zero;
                }

                var label = _threatSegmentLabels[i];
                if (label != null)
                {
                    label.gameObject.SetActive(true);
                    label.text = i == 0 ? "0-20" : (i == 1 ? "20-40" : "40-60+");
                    label.color = i == currentTierIndex ? new Color(1f, 1f, 1f, 0.95f) : new Color(0.82f, 0.84f, 0.88f, 0.85f);
                }
            }
        }

        private static int ResolveThreatTier(int threatValue, int threatTier)
        {
            if (threatTier > 0)
            {
                return Mathf.Clamp(threatTier, 1, 3);
            }

            if (threatValue >= 40)
            {
                return 3;
            }

            if (threatValue >= 20)
            {
                return 2;
            }

            return 1;
        }

        private static Color GetThreatTierColor(int tier)
        {
            switch (Mathf.Clamp(tier, 1, 3))
            {
                case 1:
                    return new Color(0.25f, 0.78f, 0.44f, 0.98f);
                case 2:
                    return new Color(0.92f, 0.72f, 0.25f, 0.98f);
                default:
                    return new Color(0.92f, 0.34f, 0.25f, 0.98f);
            }
        }

        private static string BuildStatusText(int charge, int bonus, int vulnerableStacks, List<BattleStatusState> statuses, string secretSummary, bool detailedMode)
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
                    else if (IsSecretStatusId(status.id))
                    {
                        continue;
                    }
                    else
                    {
                        sb.Append(status.id).Append(':').Append(status.stacks).Append(' ');
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(secretSummary))
            {
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(secretSummary);
            }

            return sb.ToString().TrimEnd();
        }

        private static bool IsSecretStatusId(string statusId)
        {
            if (string.IsNullOrWhiteSpace(statusId))
            {
                return false;
            }

            return statusId.IndexOf("secret", System.StringComparison.OrdinalIgnoreCase) >= 0
                || statusId.IndexOf("奥秘", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
