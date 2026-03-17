using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Battle
{
    [Serializable]
    public enum BattleCardEffectType
    {
        Damage = 0,
        Heal = 1,
        GainArmor = 2,
        DrawCards = 3,
        DamageAndDrawSelf = 4,
        DamageAndTargetHeroDraw = 5,
        CurseLoseHp = 6,
        CopyAndPlunder = 7,
        MoveArea = 8
    }

    [Serializable]
    public enum BattleTargetType
    {
        None = 0,
        Self = 1,
        SingleAlly = 2,
        AllAllies = 3,
        OtherAlly = 4,
        MonsterPart = 5,
        AllMonsterParts = 6,
        SingleUnit = 7,
        Area = 8
    }

    [Serializable]
    public enum BattleTargetFaction
    {
        None = 0,
        Allies = 1,
        Enemies = 2
    }

    [Serializable]
    public enum BattleArea
    {
        West = 0,
        Middle = 1,
        East = 2
    }

    [Serializable]
    public enum BattleZone
    {
        Front = 0,
        Back = 1
    }

    [Serializable]
    public enum BattleHeight
    {
        Ground = 0,
        Air = 1
    }

    [Serializable]
    public enum BattleFacing
    {
        East = 0,
        West = 1
    }

    [Serializable]
    public enum BattleStance
    {
        Normal = 0,
        Prone = 1
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
        public int costDelta;
    }

    [Serializable]
    public sealed class BattleStatusState
    {
        public string id;
        public int stacks;
    }

    [Serializable]
    public sealed class PlayerBattleState
    {
        public string playerId;
        public string displayName;
        public int maxHp;
        public int hp;
        public int armor;
        public int vulnerableStacks;
        public int energy;
        public BattleArea area;
        public BattleHeight height;
        public bool hasEndedTurn;
        public int cardsPlayedThisTurn;
        public int nextAttackBonus;
        public int attackChargeStage;
        public List<BattleStatusState> statuses = new List<BattleStatusState>();
        public List<BattleCardState> drawPile = new List<BattleCardState>();
        public List<BattleCardState> hand = new List<BattleCardState>();
        public List<BattleCardState> discardPile = new List<BattleCardState>();
        public List<BattleCardState> exhaustPile = new List<BattleCardState>();
    }

    [Serializable]
    public sealed class MonsterPartState
    {
        public string partId;
        public string instanceId;
        public string displayName;
        public int maxHp;
        public int hp;
        public bool isBroken;
        public BattleZone relativeZone;
        public BattleHeight relativeHeight;
        public float offsetX;
        public float offsetY;
        public float width;
        public float height;
        public float radius;
        public string shape;
        public string[] lootOnBreak;
        public List<BattleStatusState> statuses = new List<BattleStatusState>();
    }

    [Serializable]
    public sealed class MonsterSkillState
    {
        public int skillIndex;
        public string skillId;
        public string displayName;
        public int remainingWindup;
        public int damage;
        public string castPoseId;
        public string onHitAddCardId;
        public int onHitApplyVulnerable;
        public BattleArea targetArea;
        public BattleHeight targetHeight;
        public bool targetsBothHeights;
    }

    [Serializable]
    public sealed class MonsterBattleState
    {
        public string monsterId;
        public string displayName;
        public int coreMaxHp;
        public int coreHp;
        public BattleFacing facing;
        public BattleStance stance;
        public List<MonsterPartState> parts = new List<MonsterPartState>();
        public bool hasActiveSkill;
        public MonsterSkillState activeSkill;
        public MonsterSkillDefinition[] skills;
        public int[] skillCooldowns;
        public MonsterPoseDefinition[] poses;
        public string currentPoseId;
    }

    [Serializable]
    public sealed class BattleState
    {
        public string roomId;
        public string stageId;
        public string stageName;
        public string[] stageEncounterIds;
        public int stageEncounterIndex;
        public string encounterId;
        public int randomSeed;
        public int turnIndex;
        public BattlePhase phase;
        public string currentPrompt;
        public List<PlayerBattleState> players = new List<PlayerBattleState>();
        public MonsterBattleState monster;
        public List<string> loot = new List<string>();

        public PlayerBattleState GetPlayer(string playerId)
        {
            return players.Find(player => player.playerId == playerId);
        }

        public MonsterPartState GetPart(string instanceId)
        {
            return monster == null ? null : monster.parts.Find(part => part.instanceId == instanceId);
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
        public BattleArea targetArea;
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
        public string stageId;
        public string encounterId;
        public string monsterId;
        public string starterDeckId;
        public List<BattleParticipantDefinition> players = new List<BattleParticipantDefinition>();
    }

    [Serializable]
    public sealed class BattleParticipantDefinition
    {
        public string playerId;
        public string displayName;
        public string starterDeckId;
    }

    [Serializable]
    public sealed class EncounterEnemyDefinition
    {
        public string enemyId;
        public string enemyName;
        public int maxHp;
        public int attackDamage;
    }

    [Serializable]
    public sealed class MonsterPartDefinition
    {
        public string partId;
        public string displayName;
        public int maxHp;
        public string relativeZone;
        public string relativeHeight;
        public float offsetX;
        public float offsetY;
        public float width;
        public float height;
        public float radius;
        public string shape;
        public string[] lootOnBreak;
    }

    [Serializable]
    public sealed class MonsterPartPoseDefinition
    {
        public string partId;
        public string relativeZone;
        public string relativeHeight;
        public float offsetX;
        public float offsetY;
        public float width;
        public float height;
        public float radius;
        public string shape;
    }

    [Serializable]
    public sealed class MonsterPoseDefinition
    {
        public string poseId;
        public MonsterPartPoseDefinition[] parts;
    }

    [Serializable]
    public sealed class MonsterSkillDefinition
    {
        public string skillId;
        public string name;
        public int windupTurns;
        public int cooldownTurns;
        public int damage;
        public string castPoseId;
        public string onHitAddCardId;
        public int onHitApplyVulnerable;
        public string targetArea;
        public string targetHeight;
    }

    [Serializable]
    public sealed class MonsterDefinition
    {
        public string monsterId;
        public string monsterName;
        public int coreMaxHp;
        public string facing;
        public string stance;
        public MonsterPartDefinition[] parts;
        public MonsterSkillDefinition[] skills;
        public string defaultPose;
        public MonsterPoseDefinition[] poses;
    }
}
