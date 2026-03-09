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

            var actionsPanel = UiFactory.CreatePanel(Root.transform, "Actions", Color.clear, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 40f), new Vector2(-40f, 220f));
            var layout = actionsPanel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 20f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            _startBattleButton = UiFactory.CreateButton(actionsPanel.transform, "StartBattleButton", "Start Battle", Session.StartBattle);
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
            _startBattleButton.interactable = room.localPlayerId == room.hostPlayerId && room.players.Count >= 1;
            _leaveRoomButton.interactable = true;
        }
    }
}

