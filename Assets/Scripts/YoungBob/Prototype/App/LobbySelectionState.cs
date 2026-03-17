using System;
using System.Collections.Generic;
using YoungBob.Prototype.Data;
using YoungBob.Prototype.Multiplayer;

namespace YoungBob.Prototype.App
{
    internal sealed class LobbySelectionState
    {
        private int _selectedStageIndex;
        private string _selectedLocalDeckId;
        private readonly Dictionary<string, string> _playerSelectedDeckById = new Dictionary<string, string>();

        public LobbySelectionState(string initialLocalDeckId)
        {
            _selectedStageIndex = 0;
            _selectedLocalDeckId = initialLocalDeckId;
        }

        public string LocalSelectedDeckId
        {
            get { return _selectedLocalDeckId; }
        }

        public StageDefinition GetSelectedStage(IReadOnlyList<StageDefinition> availableStages)
        {
            if (availableStages == null || availableStages.Count == 0)
            {
                return null;
            }

            if (_selectedStageIndex < 0 || _selectedStageIndex >= availableStages.Count)
            {
                _selectedStageIndex = 0;
            }

            return availableStages[_selectedStageIndex];
        }

        public bool SelectPreviousStage(IReadOnlyList<StageDefinition> availableStages)
        {
            if (availableStages == null || availableStages.Count == 0)
            {
                return false;
            }

            _selectedStageIndex = (_selectedStageIndex - 1 + availableStages.Count) % availableStages.Count;
            return true;
        }

        public bool SelectNextStage(IReadOnlyList<StageDefinition> availableStages)
        {
            if (availableStages == null || availableStages.Count == 0)
            {
                return false;
            }

            _selectedStageIndex = (_selectedStageIndex + 1) % availableStages.Count;
            return true;
        }

        public bool SelectStageById(IReadOnlyList<StageDefinition> availableStages, string stageId)
        {
            if (availableStages == null || string.IsNullOrWhiteSpace(stageId))
            {
                return false;
            }

            for (var i = 0; i < availableStages.Count; i++)
            {
                if (!string.Equals(availableStages[i].id, stageId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _selectedStageIndex = i;
                return true;
            }

            return false;
        }

        public void SelectLocalDeck(string localPlayerId, string deckId)
        {
            _selectedLocalDeckId = deckId;
            if (!string.IsNullOrWhiteSpace(localPlayerId))
            {
                _playerSelectedDeckById[localPlayerId] = deckId;
            }
        }

        public string GetSelectedDeckIdForPlayer(string playerId, string defaultDeckId)
        {
            if (!string.IsNullOrWhiteSpace(playerId) && _playerSelectedDeckById.TryGetValue(playerId, out var deckId))
            {
                return deckId;
            }

            return defaultDeckId;
        }

        public void EnsureRoomDeckSelections(RoomJoinedEvent room, string localPlayerId, string defaultDeckId)
        {
            if (room == null || room.players == null)
            {
                _playerSelectedDeckById.Clear();
                return;
            }

            for (var i = 0; i < room.players.Count; i++)
            {
                var playerId = room.players[i].playerId;
                if (string.IsNullOrWhiteSpace(playerId))
                {
                    continue;
                }

                if (!_playerSelectedDeckById.ContainsKey(playerId))
                {
                    _playerSelectedDeckById[playerId] = string.Equals(playerId, localPlayerId, StringComparison.Ordinal)
                        ? _selectedLocalDeckId
                        : defaultDeckId;
                }
            }

            if (!string.IsNullOrWhiteSpace(localPlayerId) && _playerSelectedDeckById.TryGetValue(localPlayerId, out var localDeck))
            {
                _selectedLocalDeckId = string.IsNullOrWhiteSpace(localDeck) ? defaultDeckId : localDeck;
            }
        }

        public PlayerDeckChoicePayload[] BuildPlayerDeckChoices(RoomJoinedEvent room, string defaultDeckId)
        {
            if (room == null || room.players == null || room.players.Count == 0)
            {
                return Array.Empty<PlayerDeckChoicePayload>();
            }

            var result = new PlayerDeckChoicePayload[room.players.Count];
            for (var i = 0; i < room.players.Count; i++)
            {
                var playerId = room.players[i].playerId;
                result[i] = new PlayerDeckChoicePayload
                {
                    playerId = playerId,
                    deckId = GetSelectedDeckIdForPlayer(playerId, defaultDeckId)
                };
            }

            return result;
        }

        public void ApplyRemoteDeckSelection(string playerId, string deckId, string localPlayerId)
        {
            _playerSelectedDeckById[playerId] = deckId;
            if (string.Equals(playerId, localPlayerId, StringComparison.Ordinal))
            {
                _selectedLocalDeckId = deckId;
            }
        }

        public void ClearRoomPlayerSelections()
        {
            _playerSelectedDeckById.Clear();
        }
    }
}
