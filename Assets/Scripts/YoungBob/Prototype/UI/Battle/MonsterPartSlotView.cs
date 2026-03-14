using UnityEngine;
using UnityEngine.UI;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class MonsterPartSlotView : MonoBehaviour
    {
        private const float MoveLerpSpeed = 12f;

        private Image _shapeImage;
        private Image _highlightBorder;
        private Text _label;
        private RectTransform _rectTransform;
        private Vector2 _targetAnchoredPosition;
        private bool _hasTargetPosition;

        public string PartId { get; private set; }
        public string InstanceId { get; private set; }
        public bool IsAlive { get; private set; }

        public void Initialize(Image shapeImage, Text label, Image highlightBorder)
        {
            _shapeImage = shapeImage;
            _label = label;
            _highlightBorder = highlightBorder;
            _rectTransform = GetComponent<RectTransform>();
            if (_highlightBorder != null)
            {
                _highlightBorder.gameObject.SetActive(false);
            }
        }

        public void SetTargetPosition(Vector2 position, bool snapImmediately)
        {
            _targetAnchoredPosition = position;
            _hasTargetPosition = true;
            if (snapImmediately && _rectTransform != null)
            {
                _rectTransform.anchoredPosition = position;
            }
        }

        public void SetData(MonsterPartState part, SlotHighlightMode highlightMode)
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
                bool showHighlight = IsAlive && highlightMode != SlotHighlightMode.None;
                _highlightBorder.gameObject.SetActive(showHighlight);
                if (showHighlight)
                {
                    _highlightBorder.color = highlightMode == SlotHighlightMode.Selected ? new Color(1f, 0.85f, 0f, 1f) : new Color(1f, 1f, 1f, 0.4f);
                }
            }
        }

        private void Update()
        {
            if (!_hasTargetPosition || _rectTransform == null)
            {
                return;
            }

            _rectTransform.anchoredPosition = Vector2.Lerp(
                _rectTransform.anchoredPosition,
                _targetAnchoredPosition,
                1f - Mathf.Exp(-MoveLerpSpeed * Time.unscaledDeltaTime));
        }
    }
}
