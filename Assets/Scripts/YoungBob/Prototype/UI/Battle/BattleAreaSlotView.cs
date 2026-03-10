using UnityEngine;
using UnityEngine.UI;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleAreaSlotView : MonoBehaviour
    {
        private Image _background;
        private Image _highlightBorder;
        private Text _nameLabel;

        public BattleArea Area { get; private set; }

        public void Initialize(Image background, Text nameLabel, Image highlightBorder, BattleArea area)
        {
            _background = background;
            _nameLabel = nameLabel;
            _highlightBorder = highlightBorder;
            Area = area;
            _nameLabel.text = area.ToString();
            if (_highlightBorder != null) _highlightBorder.gameObject.SetActive(false);
        }

        public void SetHighlight(bool highlight)
        {
            if (_highlightBorder != null)
            {
                _highlightBorder.gameObject.SetActive(highlight);
            }
        }
    }
}
