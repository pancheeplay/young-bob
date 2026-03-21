using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YoungBob.Prototype.App;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.UI.Battle
{
    internal sealed class BattleTargetingController
    {
        private readonly Canvas _canvas;
        private readonly PrototypeSessionController _session;
        private readonly BattleBoardSection _boardSection;
        private readonly Action _rerenderBoard;
        private readonly Action<string> _setTargetHint;
        private readonly RectTransform _dragGuideRoot;
        private readonly List<RectTransform> _dragGuideSegments = new List<RectTransform>();
        private readonly RectTransform _dragGuideArrow;

        private CardDefinition _draggingCardDefinition;
        private string _draggingCardInstanceId;
        private BattleHandCardDragView _draggingCardView;
        private HoveredTargets _hoveredTargets;

        public string DraggingCardInstanceId => _draggingCardInstanceId;

        public BattleTargetingController(
            Canvas canvas,
            PrototypeSessionController session,
            BattleBoardSection boardSection,
            Action rerenderBoard,
            Action<string> setTargetHint)
        {
            _canvas = canvas;
            _session = session;
            _boardSection = boardSection;
            _rerenderBoard = rerenderBoard;
            _setTargetHint = setTargetHint;

            var dragCurveObject = new GameObject("CardDragCurve");
            dragCurveObject.transform.SetParent(_canvas.transform, false);
            _dragGuideRoot = dragCurveObject.AddComponent<RectTransform>();
            _dragGuideRoot.anchorMin = Vector2.zero;
            _dragGuideRoot.anchorMax = Vector2.one;
            _dragGuideRoot.offsetMin = Vector2.zero;
            _dragGuideRoot.offsetMax = Vector2.zero;

            const int guideSegmentCount = 18;
            for (var i = 0; i < guideSegmentCount; i++)
            {
                var segmentObj = new GameObject("Segment_" + i);
                segmentObj.transform.SetParent(_dragGuideRoot, false);
                var segmentRect = segmentObj.AddComponent<RectTransform>();
                segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
                segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
                segmentRect.pivot = new Vector2(0.5f, 0.5f);
                var segmentImage = segmentObj.AddComponent<RawImage>();
                segmentImage.texture = Texture2D.whiteTexture;
                segmentImage.color = new Color(1f, 0.95f, 0.4f, 0.95f);
                segmentImage.raycastTarget = false;
                segmentObj.SetActive(false);
                _dragGuideSegments.Add(segmentRect);
            }

            var arrowObj = new GameObject("Arrow");
            arrowObj.transform.SetParent(_dragGuideRoot, false);
            _dragGuideArrow = arrowObj.AddComponent<RectTransform>();
            _dragGuideArrow.anchorMin = new Vector2(0.5f, 0.5f);
            _dragGuideArrow.anchorMax = new Vector2(0.5f, 0.5f);
            _dragGuideArrow.pivot = new Vector2(0.5f, 0.5f);
            var arrowImage = arrowObj.AddComponent<RawImage>();
            arrowImage.texture = Texture2D.whiteTexture;
            arrowImage.color = new Color(1f, 0.95f, 0.4f, 0.95f);
            arrowImage.raycastTarget = false;
            arrowObj.SetActive(false);
            _dragGuideRoot.gameObject.SetActive(false);
        }

        public void SyncState(BattleState state, bool detailedMode, PlayerBattleState localPlayer)
        {
            if (string.IsNullOrEmpty(_draggingCardInstanceId))
            {
                return;
            }

            if (localPlayer == null || !HasCardInHand(localPlayer, _draggingCardInstanceId))
            {
                if (_draggingCardView != null)
                {
                    UnityEngine.Object.Destroy(_draggingCardView.gameObject);
                    _draggingCardView = null;
                }

                ClearTransientState(state);
                return;
            }

            RefreshHighlights(state, detailedMode);
        }

        public void BeginDrag(BattleState state, bool detailedMode, BattleHandCardDragView view, string cardInstanceId, CardDefinition cardDef)
        {
            if (!_session.CanLocalPlayerAct())
            {
                return;
            }

            _draggingCardDefinition = cardDef;
            _draggingCardInstanceId = cardInstanceId;
            _draggingCardView = view;
            _hoveredTargets = default;
            view.FollowMouse = false;
            view.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -320f);
            _setTargetHint(BuildEffectsTargetHint(cardDef));

            UpdateDragCurve(view, view.GetTopCenterScreenPoint());
            RefreshHighlights(state, detailedMode);
        }

        public void UpdateDrag(BattleState state, bool detailedMode, BattleHandCardDragView view, PointerEventData eventData)
        {
            if (_draggingCardDefinition == null || state == null)
            {
                return;
            }

            UpdateDragCurve(view, eventData.position);
            _hoveredTargets = FindHoveredTargetsAtScreenPosition(view, eventData.position);
            RefreshHighlights(state, detailedMode);
        }

        public void EndDrag(BattleState state, bool detailedMode, BattleHandCardDragView view, PointerEventData eventData)
        {
            HideDragGuide();

            if (_draggingCardDefinition == null || string.IsNullOrEmpty(_draggingCardInstanceId))
            {
                return;
            }

            var localPlayer = _session.GetLocalBattlePlayer();
            if (localPlayer == null || !HasCardInHand(localPlayer, _draggingCardInstanceId))
            {
                ClearTransientState(state);
                return;
            }

            if (!TryParseTargetType(_draggingCardDefinition.targetType, out var targetType))
            {
                ClearTransientState(state);
                return;
            }

            _hoveredTargets = FindHoveredTargetsAtScreenPosition(view, eventData.position);

            if (targetType == BattleTargetType.Area)
            {
                if (_hoveredTargets.Area != null
                    && BattleTargetingRules.CanTargetArea(state, localPlayer, _draggingCardDefinition, _hoveredTargets.Area.Area))
                {
                    _session.PlayCard(_draggingCardInstanceId, BattleTargetFaction.None, string.Empty, _hoveredTargets.Area.Area);
                }

                ClearTransientState(state);
                return;
            }

            if (targetType == BattleTargetType.MonsterPart
                || targetType == BattleTargetType.SingleAlly
                || targetType == BattleTargetType.AllAllies
                || targetType == BattleTargetType.Self
                || targetType == BattleTargetType.OtherAlly
                || targetType == BattleTargetType.SingleUnit
                || targetType == BattleTargetType.AllMonsterParts)
            {
                if (IsValidPlayerTarget(state, _draggingCardDefinition, targetType, _hoveredTargets.Player)
                    || IsValidPartTarget(state, _draggingCardDefinition, targetType, _hoveredTargets.Part))
                {
                    var isAoe = targetType == BattleTargetType.AllMonsterParts || targetType == BattleTargetType.AllAllies;
                    var unitId = string.Empty;
                    var faction = BattleTargetFaction.None;

                    if (!isAoe)
                    {
                        if (_hoveredTargets.Part != null && IsValidPartTarget(state, _draggingCardDefinition, targetType, _hoveredTargets.Part))
                        {
                            unitId = _hoveredTargets.Part.InstanceId;
                            faction = BattleTargetFaction.Enemies;
                        }
                        else if (_hoveredTargets.Player != null && IsValidPlayerTarget(state, _draggingCardDefinition, targetType, _hoveredTargets.Player))
                        {
                            unitId = _hoveredTargets.Player.UnitId;
                            faction = BattleTargetFaction.Allies;
                        }
                    }
                    else
                    {
                        faction = targetType == BattleTargetType.AllAllies ? BattleTargetFaction.Allies : BattleTargetFaction.Enemies;
                    }

                    _session.PlayCard(_draggingCardInstanceId, faction, unitId, BattleArea.Middle);
                }
            }

            ClearTransientState(state);
        }

        public void RefreshHighlights(BattleState state, bool detailedMode)
        {
            if (state == null || _draggingCardDefinition == null)
            {
                return;
            }

            if (!TryParseTargetType(_draggingCardDefinition.targetType, out var targetType))
            {
                targetType = BattleTargetType.None;
            }

            _boardSection.ReapplyHighlights(state, _draggingCardDefinition, targetType, _hoveredTargets.Player, _hoveredTargets.Part, _hoveredTargets.Area, detailedMode);
        }

        private void ClearTransientState(BattleState state)
        {
            _hoveredTargets = default;
            _draggingCardDefinition = null;
            _draggingCardInstanceId = null;
            _draggingCardView = null;
            _setTargetHint(string.Empty);
            ClearHighlights(state);
        }

        private void ClearHighlights(BattleState state)
        {
            if (state == null)
            {
                return;
            }

            _rerenderBoard();
            _boardSection.ClearAreaHighlights();
        }

        private bool IsValidPlayerTarget(BattleState state, CardDefinition cardDef, BattleTargetType targetType, BattleUnitSlotView slot)
        {
            if (slot == null || !slot.IsAlive || state == null)
            {
                return false;
            }

            var localPlayer = state.GetPlayer(_session.LocalPlayerId);
            var targetPlayer = state.GetPlayer(slot.UnitId);
            return BattleTargetingRules.CanTargetPlayer(state, localPlayer, cardDef, targetType, targetPlayer);
        }

        private bool IsValidPartTarget(BattleState state, CardDefinition cardDef, BattleTargetType targetType, MonsterPartSlotView slot)
        {
            if (slot == null || state == null)
            {
                return false;
            }

            var localPlayer = state.GetPlayer(_session.LocalPlayerId);
            var targetPart = state.GetPart(slot.InstanceId);
            return BattleTargetingRules.CanTargetPart(state, localPlayer, cardDef, targetType, targetPart);
        }

        private HoveredTargets FindHoveredTargetsAtScreenPosition(BattleHandCardDragView view, Vector2 screenPosition)
        {
            var targets = default(HoveredTargets);
            var raycaster = _canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                return targets;
            }

            var results = RaycastAtScreenPosition(screenPosition, raycaster);
            for (var i = 0; i < results.Count; i++)
            {
                var hitObject = results[i].gameObject;
                if (hitObject == view.gameObject || hitObject.transform.IsChildOf(view.transform))
                {
                    continue;
                }

                if (targets.Player == null)
                {
                    targets.Player = FindHoveredPlayerSlot(hitObject);
                }

                if (targets.Part == null)
                {
                    targets.Part = FindHoveredPartSlot(hitObject);
                }

                if (targets.Area == null)
                {
                    targets.Area = FindHoveredAreaDropZone(hitObject);
                }

                if (targets.Player != null && targets.Part != null && targets.Area != null)
                {
                    break;
                }
            }

            return targets;
        }

        private void UpdateDragCurve(BattleHandCardDragView view, Vector2 pointerScreenPosition)
        {
            _dragGuideRoot.gameObject.SetActive(true);
            _dragGuideRoot.SetAsLastSibling();

            var eventCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragGuideRoot, view.GetTopCenterScreenPoint(), eventCamera, out var startLocal)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(_dragGuideRoot, pointerScreenPosition, eventCamera, out var endLocal))
            {
                HideDragGuide();
                return;
            }

            var delta = endLocal - startLocal;
            if (delta.sqrMagnitude < 16f)
            {
                HideDragGuide();
                return;
            }

            var control = startLocal + new Vector2(0f, Mathf.Max(90f, Mathf.Abs(delta.y) * 0.38f));
            var segmentCount = _dragGuideSegments.Count;
            var prev = EvaluateQuadratic(startLocal, control, endLocal, 0f);
            for (var i = 0; i < segmentCount; i++)
            {
                var t = (i + 1) / (float)segmentCount;
                var next = EvaluateQuadratic(startLocal, control, endLocal, t);
                ConfigureGuideSegment(_dragGuideSegments[i], prev, next, 8f);
                prev = next;
            }

            var tangent = EvaluateQuadraticTangent(startLocal, control, endLocal, 1f);
            tangent = tangent.sqrMagnitude < 0.0001f ? delta.normalized : tangent.normalized;
            ConfigureGuideArrow(endLocal, tangent, 20f, 14f);
        }

        private void HideDragGuide()
        {
            for (var i = 0; i < _dragGuideSegments.Count; i++)
            {
                _dragGuideSegments[i].gameObject.SetActive(false);
            }

            _dragGuideArrow.gameObject.SetActive(false);
            _dragGuideRoot.gameObject.SetActive(false);
        }

        private static void ConfigureGuideSegment(RectTransform segment, Vector2 from, Vector2 to, float thickness)
        {
            if (segment == null)
            {
                return;
            }

            var dir = to - from;
            var len = dir.magnitude;
            if (len < 0.5f)
            {
                segment.gameObject.SetActive(false);
                return;
            }

            segment.gameObject.SetActive(true);
            segment.anchoredPosition = (from + to) * 0.5f;
            segment.sizeDelta = new Vector2(len, thickness);
            segment.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        private void ConfigureGuideArrow(Vector2 tip, Vector2 tangent, float length, float width)
        {
            _dragGuideArrow.gameObject.SetActive(true);
            _dragGuideArrow.anchoredPosition = tip - tangent * (length * 0.5f);
            _dragGuideArrow.sizeDelta = new Vector2(length, width);
            _dragGuideArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
        }

        private static Vector2 EvaluateQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            var inv = 1f - t;
            return inv * inv * p0 + 2f * inv * t * p1 + t * t * p2;
        }

        private static Vector2 EvaluateQuadraticTangent(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
        }

        private static List<RaycastResult> RaycastAtScreenPosition(Vector2 screenPosition, GraphicRaycaster raycaster)
        {
            var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
            var results = new List<RaycastResult>();
            raycaster.Raycast(pointerData, results);
            return results;
        }

        private static BattleUnitSlotView FindHoveredPlayerSlot(GameObject hoveredObject)
        {
            while (hoveredObject != null)
            {
                var slot = hoveredObject.GetComponent<BattleUnitSlotView>();
                if (slot != null)
                {
                    return slot;
                }

                hoveredObject = hoveredObject.transform.parent == null ? null : hoveredObject.transform.parent.gameObject;
            }

            return null;
        }

        private static MonsterPartSlotView FindHoveredPartSlot(GameObject hoveredObject)
        {
            while (hoveredObject != null)
            {
                var slot = hoveredObject.GetComponent<MonsterPartSlotView>();
                if (slot != null)
                {
                    return slot;
                }

                hoveredObject = hoveredObject.transform.parent == null ? null : hoveredObject.transform.parent.gameObject;
            }

            return null;
        }

        private static BattleAreaDropZoneView FindHoveredAreaDropZone(GameObject hoveredObject)
        {
            while (hoveredObject != null)
            {
                var slot = hoveredObject.GetComponent<BattleAreaDropZoneView>();
                if (slot != null)
                {
                    return slot;
                }

                hoveredObject = hoveredObject.transform.parent == null ? null : hoveredObject.transform.parent.gameObject;
            }

            return null;
        }

        private static string BuildEffectsTargetHint(CardDefinition cardDef)
        {
            return CardEffectTextFormatter.BuildEffectsTargetHint(cardDef);
        }

        private static bool TryParseTargetType(string raw, out BattleTargetType targetType)
        {
            targetType = BattleTargetType.None;
            return !string.IsNullOrEmpty(raw) && Enum.TryParse(raw, true, out targetType);
        }

        private static bool HasCardInHand(PlayerBattleState player, string cardInstanceId)
        {
            if (player == null || string.IsNullOrEmpty(cardInstanceId))
            {
                return false;
            }

            for (var i = 0; i < player.hand.Count; i++)
            {
                if (player.hand[i].instanceId == cardInstanceId)
                {
                    return true;
                }
            }

            return false;
        }

        private struct HoveredTargets
        {
            public BattleUnitSlotView Player;
            public MonsterPartSlotView Part;
            public BattleAreaDropZoneView Area;
        }
    }
}
