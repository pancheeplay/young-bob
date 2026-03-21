using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YoungBob.Prototype.App;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleStageFxController
    {
        private readonly Canvas _canvas;
        private readonly PrototypeSessionController _session;
        private readonly BattleBoardSection _boardSection;
        private readonly RectTransform _fxLayer;
        private readonly List<FlyingCardFx> _activeCards = new List<FlyingCardFx>();

        public BattleStageFxController(Canvas canvas, PrototypeSessionController session, BattleBoardSection boardSection)
        {
            _canvas = canvas;
            _session = session;
            _boardSection = boardSection;

            var layerObject = new GameObject("BattleStageFxLayer");
            layerObject.transform.SetParent(_canvas.transform, false);
            _fxLayer = layerObject.AddComponent<RectTransform>();
            _fxLayer.anchorMin = Vector2.zero;
            _fxLayer.anchorMax = Vector2.one;
            _fxLayer.offsetMin = Vector2.zero;
            _fxLayer.offsetMax = Vector2.zero;
        }

        public void PlayEvents(IReadOnlyList<BattleEvent> battleEvents)
        {
            if (battleEvents == null || battleEvents.Count == 0)
            {
                return;
            }

            for (var i = 0; i < battleEvents.Count; i++)
            {
                var battleEvent = battleEvents[i];
                if (battleEvent == null || string.IsNullOrWhiteSpace(battleEvent.eventId))
                {
                    continue;
                }

                if (string.Equals(battleEvent.eventId, "card_played", StringComparison.Ordinal))
                {
                    PlayCardFx(battleEvent);
                }
            }
        }

        private void PlayCardFx(BattleEvent battleEvent)
        {
            var actorSlot = _boardSection.FindPlayerSlot(battleEvent.actor);
            if (actorSlot == null)
            {
                return;
            }

            var actorRect = actorSlot.GetComponent<RectTransform>();
            if (actorRect == null)
            {
                return;
            }

            var startWorld = actorRect.TransformPoint(new Vector3(0f, actorRect.rect.height * 0.72f, 0f));
            var endWorld = _boardSection.BoardPanelRect.TransformPoint(new Vector3(0f, _boardSection.BoardPanelRect.rect.height * 0.68f, 0f));
            var eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 startLocal;
            Vector2 endLocal;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_fxLayer, RectTransformUtility.WorldToScreenPoint(eventCamera, startWorld), eventCamera, out startLocal)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_fxLayer, RectTransformUtility.WorldToScreenPoint(eventCamera, endWorld), eventCamera, out endLocal))
            {
                return;
            }

            var cardName = battleEvent.cardId;
            var cardDef = _session.GetCardDefinition(battleEvent.cardId);
            if (cardDef != null && !string.IsNullOrWhiteSpace(cardDef.name))
            {
                cardName = cardDef.name;
            }

            var cardObj = new GameObject("FlyingCardFx");
            cardObj.transform.SetParent(_fxLayer, false);
            var rect = cardObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(112f, 152f);

            var image = cardObj.AddComponent<Image>();
            image.color = new Color(0.91f, 0.94f, 0.97f, 0.96f);
            image.raycastTarget = false;

            var title = UiFactory.CreateText(cardObj.transform, "Title", 14, TextAnchor.MiddleCenter, new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.84f), Vector2.zero, Vector2.zero);
            title.text = cardName;
            title.color = new Color(0.07f, 0.11f, 0.16f, 0.96f);
            title.fontStyle = FontStyle.Bold;
            title.raycastTarget = false;

            var fx = cardObj.AddComponent<FlyingCardFx>();
            fx.Initialize(rect, image, title, startLocal, endLocal, () => _activeCards.Remove(fx));
            _activeCards.Add(fx);
        }

        private sealed class FlyingCardFx : MonoBehaviour
        {
            private RectTransform _rect;
            private Image _image;
            private Text _title;
            private Vector2 _start;
            private Vector2 _end;
            private Action _onComplete;
            private float _startTime;
            private const float Duration = 0.48f;

            public void Initialize(RectTransform rect, Image image, Text title, Vector2 start, Vector2 end, Action onComplete)
            {
                _rect = rect;
                _image = image;
                _title = title;
                _start = start;
                _end = end;
                _onComplete = onComplete;
                _startTime = Time.unscaledTime;
                _rect.anchoredPosition = start;
            }

            private void Update()
            {
                var t = Mathf.Clamp01((Time.unscaledTime - _startTime) / Duration);
                var arcOffset = Mathf.Sin(t * Mathf.PI) * 54f;
                var pos = Vector2.Lerp(_start, _end, t);
                pos.y += arcOffset;
                _rect.anchoredPosition = pos;
                _rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.76f, t);
                _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 8f, t));

                var alpha = 1f - t;
                var imageColor = _image.color;
                imageColor.a = 0.96f * alpha;
                _image.color = imageColor;
                var titleColor = _title.color;
                titleColor.a = alpha;
                _title.color = titleColor;

                if (t >= 1f)
                {
                    _onComplete?.Invoke();
                    Destroy(gameObject);
                }
            }
        }
    }
}
