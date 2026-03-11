using System;
using System.Collections.Generic;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.Testing
{
    public enum BattleTestTopology
    {
        SingleClient = 0,
        MultiClient = 1
    }

    [Serializable]
    public sealed class DriverPlayerSpec
    {
        public string playerId;
        public string displayName;
    }

    [Serializable]
    public sealed class DriverSetupOptions
    {
        public string roomId;
        public string stageId;
        public string encounterId;
        public string monsterId;
        public string starterDeckId;
        public int randomSeed;
        public BattleTestTopology topology;
        public List<DriverPlayerSpec> players = new List<DriverPlayerSpec>();
    }

    [Serializable]
    public sealed class DriverSnapshot
    {
        public string tag;
        public string actorPlayerId;
        public string stateJson;
        public string stateHash;
        public string[] eventMessages;
        public string error;
    }

    [Serializable]
    public sealed class DriverActionResult
    {
        public bool success;
        public string error;
        public DriverSnapshot snapshot;
    }

    public interface IBattleTestDriver
    {
        DriverActionResult Setup(DriverSetupOptions options);
        DriverActionResult StartBattle(string hostPlayerId);
        DriverActionResult PlayCard(string actorPlayerId, string cardInstanceId, BattleTargetFaction targetFaction, string targetUnitId, BattleArea targetArea);
        DriverActionResult EndTurn(string actorPlayerId);
        DriverActionResult DebugDamageMonster(int amount);
        DriverActionResult DebugSetPlayerHp(string playerId, int hp);
        DriverActionResult Snapshot(string tag);
    }
}
