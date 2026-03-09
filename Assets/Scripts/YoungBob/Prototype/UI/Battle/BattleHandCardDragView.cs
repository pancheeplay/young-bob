using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleHandCardDragView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Canvas _canvas;
        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Vector2 _originalAnchoredPosition;

        public Action<BattleHandCardDragView, PointerEventData> BeganDrag;
        public Action<BattleHandCardDragView, PointerEventData> Dragged;
        public Action<BattleHandCardDragView, PointerEventData> EndedDrag;

        public void Initialize(Canvas canvas)
        {
            _canvas = canvas;
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _originalParent = transform.parent;
            _originalSiblingIndex = transform.GetSiblingIndex();
            _originalAnchoredPosition = _rectTransform.anchoredPosition;
            transform.SetParent(_canvas.transform, true);
            _canvasGroup.blocksRaycasts = false;
            BeganDrag?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _rectTransform.anchoredPosition += eventData.delta / _canvas.scaleFactor;
            Dragged?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            transform.SetParent(_originalParent, false);
            transform.SetSiblingIndex(_originalSiblingIndex);
            _rectTransform.anchoredPosition = _originalAnchoredPosition;
            _canvasGroup.blocksRaycasts = true;
            EndedDrag?.Invoke(this, eventData);
        }
    }
}
