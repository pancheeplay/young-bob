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

        public void SetHighlight(bool highlight)
        {
            if (_highlight != null)
            {
                _highlight.enabled = highlight;
            }
        }
    }
}
