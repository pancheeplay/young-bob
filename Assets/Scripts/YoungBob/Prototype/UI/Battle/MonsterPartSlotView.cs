using UnityEngine;
using UnityEngine.UI;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class MonsterPartSlotView : MonoBehaviour
    {
        private Image _shapeImage;
        private Image _highlightBorder;
        private Text _label;

        public string PartId { get; private set; }
        public string InstanceId { get; private set; }
        public bool IsAlive { get; private set; }

        public void Initialize(Image shapeImage, Text label, Image highlightBorder)
        {
            _shapeImage = shapeImage;
            _label = label;
            _highlightBorder = highlightBorder;
            if (_highlightBorder != null)
            {
                _highlightBorder.gameObject.SetActive(false);
            }
        }

        public void SetData(MonsterPartState part, bool highlight)
        {
            PartId = part.partId;
            InstanceId = part.instanceId;
            IsAlive = part.hp > 0;

            if (_label != null)
            {
                _label.text = part.displayName + "\n" + "<color=#fbbf24>Break: " + part.hp + "/" + part.maxHp + "</color>";
                _label.color = IsAlive ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            }

            if (_shapeImage != null)
            {
                _shapeImage.color = IsAlive ? new Color(0.55f, 0.25f, 0.25f, 0.95f) : new Color(0.2f, 0.2f, 0.2f, 0.8f);
            }

            if (_highlightBorder != null)
            {
                _highlightBorder.gameObject.SetActive(highlight && IsAlive);
            }
        }
    }
}
