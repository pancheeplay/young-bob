using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YoungBob.Prototype.App;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;
using YoungBob.Prototype.UI.Battle;

namespace YoungBob.Prototype.UI.Pages
{
    internal sealed class BattlePage : PageBase
    {
        private readonly Canvas _canvas;
        private readonly BattleTopBarSection _topBarSection;
        private readonly BattleLogSection _logSection;
        private readonly BattleHandSection _handSection;
        private readonly BattleBoardSection _boardSection;
        private readonly BattleTargetingController _targetingController;
        private readonly BattlePhaseBannerSection _phaseBannerSection;
        private readonly BattleStageFxController _stageFxController;

        private BattleState _lastState;
        private bool _isDetailedStatusMode = true;

        public BattlePage(Transform parent, PrototypeSessionController session)
            : base(parent, "BattlePage", session, new Color(0.12f, 0.14f, 0.17f), new Vector2(0f, 0f), new Vector2(1f, 1f))
        {
            _canvas = parent.GetComponent<Canvas>();
            _topBarSection = new BattleTopBarSection(Root.transform, ToggleStatusMode, Session.EndBattleAndReturnToLobby);
            _logSection = new BattleLogSection(Root.transform);
            _handSection = new BattleHandSection(Root.transform, Session, _canvas, ToggleQuickChatWheel, SendQuickChat, Session.ToggleTurnReady);
            _boardSection = new BattleBoardSection(Root.transform, Session);
            _targetingController = new BattleTargetingController(_canvas, Session, _boardSection, () => RenderBoard(_lastState), text => _handSection.SetTargetHint(text));
            _phaseBannerSection = new BattlePhaseBannerSection(Root.transform);
            _stageFxController = new BattleStageFxController(_canvas, Session, _boardSection);

            Hide();
        }


        public void Render(BattleState state)
        {
            _lastState = state;
            RenderSummary(state);
            RenderBoard(state);
            RenderHand(state);
            RenderPhaseBanner(state);
        }

        private void RenderSummary(BattleState state)
        {
            var localPlayer = Session.GetLocalBattlePlayer();
            _topBarSection.Render(state, localPlayer, Session.LocalPlayerId, ResolvePhaseTitle, ResolvePhaseColor, ResolvePlayerMarker);
            _topBarSection.StatusModeButton.GetComponentInChildren<Text>().text = _isDetailedStatusMode ? "模式: 详" : "模式: 简";
            _topBarSection.StatusModeButton.interactable = state != null;
            _topBarSection.ExitBattleButton.interactable = state != null;
            _handSection.ChatButton.interactable = state != null;
            _handSection.EndTurnButton.interactable = localPlayer != null
                && localPlayer.hp > 0
                && state != null
                && state.phase == BattlePhase.PlayerTurn;
            _handSection.RenderEndTurnState(state, Session.LocalPlayerId, ResolvePlayerMarker, ResolvePhaseTitle);
        }

        private void RenderPhaseBanner(BattleState state)
        {
            _phaseBannerSection.Render(state, ResolvePhaseTitle, ResolvePhaseColor);
        }

        private static string ResolvePhaseTitle(BattlePhase phase)
        {
            return BattlePhaseMapper.GetTitle(phase);
        }

        private static string ResolvePhaseColor(BattlePhase phase)
        {
            return BattlePhaseMapper.GetColor(phase);
        }

        public void AppendBattleLog(string message)
        {
            _logSection.Append(message);
        }

        public void PlayBattleEvents(IReadOnlyList<BattleEvent> battleEvents)
        {
            _stageFxController.PlayEvents(battleEvents);
        }

        private void RenderBoard(BattleState state)
        {
            _boardSection.Render(state, _isDetailedStatusMode, ResolvePlayerMarker);
        }

        private void RenderHand(BattleState state)
        {
            var player = Session.GetLocalBattlePlayer();
            _targetingController.SyncState(state, _isDetailedStatusMode, player);

            var canAct = Session.CanLocalPlayerAct();
            _handSection.Render(
                state,
                player,
                canAct,
                _targetingController.DraggingCardInstanceId,
                BeginCardDrag,
                UpdateCardDrag,
                EndCardDrag);
        }

        private void BeginCardDrag(BattleHandCardDragView view, string cardInstanceId, CardDefinition cardDef, PointerEventData eventData)
        {
            _targetingController.BeginDrag(_lastState, _isDetailedStatusMode, view, cardInstanceId, cardDef);
        }

        private void UpdateCardDrag(BattleHandCardDragView view, PointerEventData eventData)
        {
            _targetingController.UpdateDrag(_lastState, _isDetailedStatusMode, view, eventData);
        }

        private void EndCardDrag(BattleHandCardDragView view, PointerEventData eventData)
        {
            _targetingController.EndDrag(_lastState, _isDetailedStatusMode, view, eventData);
        }

        private string ResolvePlayerMarker(string playerId)
        {
            if (_lastState == null || _lastState.players == null || string.IsNullOrWhiteSpace(playerId))
            {
                return "●";
            }

            var index = 0;
            for (var i = 0; i < _lastState.players.Count; i++)
            {
                var player = _lastState.players[i];
                if (player != null && string.Equals(player.playerId, playerId, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            switch (index % 4)
            {
                case 0:
                    return "●";
                case 1:
                    return "▲";
                case 2:
                    return "■";
                default:
                    return "◆";
            }
        }

        private void ToggleStatusMode()
        {
            _isDetailedStatusMode = !_isDetailedStatusMode;
            _topBarSection.StatusModeButton.GetComponentInChildren<Text>().text = _isDetailedStatusMode ? "模式: 详" : "模式: 简";
            if (_lastState != null)
            {
                RenderBoard(_lastState);
                _targetingController.RefreshHighlights(_lastState, _isDetailedStatusMode);
            }
        }

        private void ToggleQuickChatWheel()
        {
            _handSection.ToggleQuickChat();
        }

        private void CloseQuickChatWheel()
        {
            _handSection.CloseQuickChat();
        }

        private void SendQuickChat(string presetId)
        {
            Session.SendQuickChat(presetId);
            CloseQuickChatWheel();
        }
    }

}
