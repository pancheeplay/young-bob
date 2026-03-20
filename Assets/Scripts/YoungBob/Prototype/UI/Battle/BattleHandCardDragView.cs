using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleHandCardDragView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float DragScaleMultiplier = 1.28f;
        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Canvas _canvas;
        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Vector2 _originalAnchoredPosition;
        private Vector3 _originalLocalScale;

        public Action<BattleHandCardDragView, PointerEventData> BeganDrag;
        public Action<BattleHandCardDragView, PointerEventData> Dragged;
        public Action<BattleHandCardDragView, PointerEventData> EndedDrag;

        public bool FollowMouse { get; set; } = true;

        public void Initialize(Canvas canvas)
        {
            _canvas = canvas;
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public Vector2 GetTopCenterScreenPoint()
        {
            Vector3[] corners = new Vector3[4];
            _rectTransform.GetWorldCorners(corners);
            // corners[1] = top-left, corners[2] = top-right in World Space
            Vector3 worldTopCenter = (corners[1] + corners[2]) * 0.5f;
            
            if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return new Vector2(worldTopCenter.x, worldTopCenter.y);
            }
            
            return RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, worldTopCenter);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _originalParent = transform.parent;
            _originalSiblingIndex = transform.GetSiblingIndex();
            _originalAnchoredPosition = _rectTransform.anchoredPosition;
            _originalLocalScale = _rectTransform.localScale;
            
            transform.SetParent(_canvas.transform, true);
            transform.SetAsLastSibling();
            
            // Reset anchors and pivot to center so anchoredPosition works as expected
            _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            
            _rectTransform.localScale = _originalLocalScale * DragScaleMultiplier;
            _canvasGroup.blocksRaycasts = false;
            BeganDrag?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (FollowMouse)
            {
                _rectTransform.anchoredPosition += eventData.delta / _canvas.scaleFactor;
            }
            Dragged?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            try
            {
                EndedDrag?.Invoke(this, eventData);
            }
            finally
            {
                transform.SetParent(_originalParent, false);
                transform.SetSiblingIndex(_originalSiblingIndex);
                // Return to original state (layout group will handle anchors/pivot if applicable, 
                // but we should restore them if they were modified)
                _rectTransform.anchoredPosition = _originalAnchoredPosition;
                _rectTransform.localScale = _originalLocalScale;
                _canvasGroup.blocksRaycasts = true;
            }
        }
    }
}
