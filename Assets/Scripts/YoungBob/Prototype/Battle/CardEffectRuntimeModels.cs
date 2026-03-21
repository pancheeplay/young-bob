using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Battle
{
    internal sealed class CardEffectExecutionContext
    {
        public BattleState state;
        public PlayerBattleState actingPlayer;
        public BattleCommand command;
        public BattleCardState playedCard;
        public CardDefinition definition;
        public BattleTargetType cardTargetType;
        public BattleCommandResult result;
        public CardEffectLastResult lastResult = new CardEffectLastResult();
    }

    internal sealed class CardEffectLastResult
    {
        public int damageDealt;
        public bool killedTarget;

        public void Reset()
        {
            damageDealt = 0;
            killedTarget = false;
        }

        public void Set(int damage, bool killed)
        {
            damageDealt = damage;
            killedTarget = killed;
        }
    }

    internal sealed class CardEffectTargetRef
    {
        public PlayerBattleState Player;
        public MonsterPartState Part;
        public bool IsPlayer => Player != null;

        public static CardEffectTargetRef ForPlayer(PlayerBattleState player)
        {
            return new CardEffectTargetRef { Player = player };
        }

        public static CardEffectTargetRef ForPart(MonsterPartState part)
        {
            return new CardEffectTargetRef { Part = part };
        }
    }
}
