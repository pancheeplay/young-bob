using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using YoungBob.Prototype.App;
using YoungBob.Prototype.Multiplayer;

namespace YoungBob.Prototype.UI.Pages
{
    internal sealed class RoomPage : PageBase
    {
        private readonly Text _roomText;
        private readonly Text _playersText;
        private readonly Text _stageText;
        private readonly Button _stagePrevButton;
        private readonly Button _stageNextButton;
        private readonly Button _startBattleButton;
        private readonly Button _leaveRoomButton;

        public RoomPage(Transform parent, PrototypeSessionController session)
            : base(parent, "RoomPage", session, new Color(0.14f, 0.17f, 0.2f), new Vector2(0f, 0f), new Vector2(1f, 0.8f))
        {
            var title = UiFactory.CreateText(Root.transform, "Title", 36, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -120f), new Vector2(-40f, -40f));
            title.text = "Room";

            _roomText = UiFactory.CreateText(Root.transform, "RoomInfo", 24, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(40f, -220f), new Vector2(-40f, -140f));
            
            var playersPanel = UiFactory.CreatePanel(Root.transform, "PlayersPanel", new Color(0.1f, 0.12f, 0.15f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 250f), new Vector2(-40f, -240f));
            _playersText = UiFactory.CreateText(playersPanel.transform, "PlayersList", 20, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(20f, 20f), new Vector2(-20f, -20f));

            var stagePanel = UiFactory.CreatePanel(Root.transform, "StagePanel", new Color(0.11f, 0.14f, 0.17f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 230f), new Vector2(-40f, 320f));
            _stageText = UiFactory.CreateText(stagePanel.transform, "StageText", 20, TextAnchor.MiddleCenter, new Vector2(0.2f, 0f), new Vector2(0.8f, 1f), Vector2.zero, Vector2.zero);
            _stageText.supportRichText = true;
            _stagePrevButton = UiFactory.CreateButton(stagePanel.transform, "PrevStageButton", "<", Session.SelectPreviousStage);
            var prevRect = _stagePrevButton.GetComponent<RectTransform>();
            prevRect.anchorMin = new Vector2(0f, 0.15f);
            prevRect.anchorMax = new Vector2(0.18f, 0.85f);
            prevRect.offsetMin = new Vector2(10f, 0f);
            prevRect.offsetMax = new Vector2(-10f, 0f);
            _stageNextButton = UiFactory.CreateButton(stagePanel.transform, "NextStageButton", ">", Session.SelectNextStage);
            var nextRect = _stageNextButton.GetComponent<RectTransform>();
            nextRect.anchorMin = new Vector2(0.82f, 0.15f);
            nextRect.anchorMax = new Vector2(1f, 0.85f);
            nextRect.offsetMin = new Vector2(10f, 0f);
            nextRect.offsetMax = new Vector2(-10f, 0f);

            var actionsPanel = UiFactory.CreatePanel(Root.transform, "Actions", Color.clear, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 40f), new Vector2(-40f, 220f));
            var layout = actionsPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            _startBattleButton = UiFactory.CreateButton(actionsPanel.transform, "StartBattleButton", "Start Stage", Session.StartBattle);
            _startBattleButton.GetComponent<LayoutElement>().preferredHeight = 80f;
            _leaveRoomButton = UiFactory.CreateButton(actionsPanel.transform, "LeaveRoomButton", "Leave Room", Session.LeaveRoom);
            _leaveRoomButton.GetComponent<LayoutElement>().preferredHeight = 80f;
            
            Hide();
        }

        public void Render(RoomJoinedEvent room)
        {
            if (room == null)
            {
                _roomText.text = "No room joined.";
                _playersText.text = string.Empty;
                RenderStageInfo();
                _startBattleButton.interactable = false;
                _leaveRoomButton.interactable = false;
                return;
            }

            _roomText.text = "Room: " + room.roomId + "\nHost: " + room.hostPlayerId;
            var builder = new StringBuilder();
            builder.AppendLine("Players in Room:");
            builder.AppendLine();
            for (var i = 0; i < room.players.Count; i++)
            {
                var player = room.players[i];
                var tags = new List<string>();
                if (player.isHost) tags.Add("host");
                if (player.isLocal) tags.Add("you");

                var suffix = tags.Count > 0 ? " [" + string.Join(", ", tags) + "]" : string.Empty;
                builder.AppendLine("• " + player.displayName + suffix);
            }

            _playersText.text = builder.ToString();
            RenderStageInfo();
            _startBattleButton.interactable = room.localPlayerId == room.hostPlayerId && room.players.Count >= 1 && Session.SelectedStage != null;
            _leaveRoomButton.interactable = true;
        }

        private void RenderStageInfo()
        {
            var stage = Session.SelectedStage;
            if (stage == null)
            {
                _stageText.text = "<color=#FF8080>No stage data</color>";
                _stagePrevButton.interactable = false;
                _stageNextButton.interactable = false;
                return;
            }

            var count = stage.encounterIds == null ? 0 : stage.encounterIds.Length;
            _stageText.text = "Stage: <b>" + stage.name + "</b>\nID: " + stage.id + "  |  Encounters: " + count;
            var canSwitch = Session.AvailableStageCount > 1;
            _stagePrevButton.interactable = canSwitch;
            _stageNextButton.interactable = canSwitch;
        }
    }
}
