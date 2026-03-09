using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Battle
{
    [Serializable]
    public enum BattleCardEffectType
    {
        Damage = 0,
        Heal = 1
    }

    [Serializable]
    public enum BattleTargetType
    {
        Self = 0,
        SingleEnemy = 1,
        AllEnemies = 2,
        SingleAlly = 3,
        AllAllies = 4,
        OtherAlly = 5,
        SingleUnit = 6
    }

    [Serializable]
    public enum BattleTargetFaction
    {
        None = 0,
        Allies = 1,
        Enemies = 2
    }

    [Serializable]
    public enum BattlePhase
    {
        WaitingForPlayers = 0,
        PlayerTurn = 1,
        MonsterTurn = 2,
        Victory = 3,
        Defeat = 4
    }

    [Serializable]
    public sealed class BattleCardState
    {
        public string instanceId;
        public string cardId;
    }

    [Serializable]
    public sealed class PlayerBattleState
    {
        public string playerId;
        public string displayName;
        public int maxHp;
        public int hp;
        public int armor;
        public bool hasEndedTurn;
        public List<BattleCardState> drawPile = new List<BattleCardState>();
        public List<BattleCardState> hand = new List<BattleCardState>();
        public List<BattleCardState> discardPile = new List<BattleCardState>();
    }

    [Serializable]
    public sealed class EnemyBattleState
    {
        public string enemyId;
        public string instanceId;
        public string displayName;
        public int maxHp;
        public int hp;
        public int armor;
        public int attackDamage;
    }

    [Serializable]
    public sealed class BattleState
    {
        public string roomId;
        public int randomSeed;
        public int turnIndex;
        public BattlePhase phase;
        public string currentPrompt;
        public List<PlayerBattleState> players = new List<PlayerBattleState>();
        public List<EnemyBattleState> enemies = new List<EnemyBattleState>();

        public PlayerBattleState GetPlayer(string playerId)
        {
            return players.Find(player => player.playerId == playerId);
        }

        public EnemyBattleState GetEnemy(string instanceId)
        {
            return enemies.Find(enemy => enemy.instanceId == instanceId);
        }
    }

    [Serializable]
    public sealed class BattleEvent
    {
        public string message;
    }

    [Serializable]
    public sealed class BattleCommand
    {
        public string commandId;
        public string actorPlayerId;
        public string action;
        public string cardInstanceId;
        public BattleTargetFaction targetFaction;
        public string targetUnitId;
    }

    [Serializable]
    public sealed class BattleCommandResult
    {
        public bool success;
        public string error;
        public List<BattleEvent> events = new List<BattleEvent>();
    }

    [Serializable]
    public sealed class BattleSetupDefinition
    {
        public string roomId;
        public int randomSeed;
        public string encounterId;
        public string starterDeckId;
        public List<BattleParticipantDefinition> players = new List<BattleParticipantDefinition>();
    }

    [Serializable]
    public sealed class BattleParticipantDefinition
    {
        public string playerId;
        public string displayName;
    }

    [Serializable]
    public sealed class EncounterEnemyDefinition
    {
        public string enemyId;
        public string enemyName;
        public int maxHp;
        public int attackDamage;
    }
}
