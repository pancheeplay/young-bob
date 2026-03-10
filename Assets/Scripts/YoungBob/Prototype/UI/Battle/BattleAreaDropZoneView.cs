using UnityEngine.UI;
using UnityEngine;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleAreaDropZoneView : MonoBehaviour
    {
        private Image _image;
        private Image _highlight;

        public BattleArea Area { get; private set; }

        public void Initialize(Image image, Image highlight, BattleArea area)
        {
            _image = image;
            _highlight = highlight;
            Area = area;
        }

        public void SetHighlight(SlotHighlightMode mode)
        {
            if (_highlight != null)
            {
                _highlight.enabled = mode != SlotHighlightMode.None;
                if (_highlight.enabled)
                {
                    _highlight.color = mode == SlotHighlightMode.Selected ? new Color(1f, 0.85f, 0f, 0.45f) : new Color(1f, 1f, 1f, 0.2f);
                }
            }
        }
    }
}
