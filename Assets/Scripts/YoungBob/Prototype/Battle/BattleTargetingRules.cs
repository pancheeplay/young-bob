using System;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Battle
{
    internal static class BattleTargetingRules
    {
        public static bool CanTargetArea(BattleState state, PlayerBattleState actingPlayer, CardDefinition card, BattleArea targetArea)
        {
            if (!TryParseTargetType(card, out var targetType) || targetType != BattleTargetType.Area)
            {
                return false;
            }

            if (state == null || actingPlayer == null)
            {
                return false;
            }

            if (targetArea != BattleArea.West && targetArea != BattleArea.East)
            {
                return false;
            }

            return actingPlayer.area != targetArea;
        }

        public static bool CanTargetPlayer(BattleState state, PlayerBattleState actingPlayer, CardDefinition card, BattleTargetType targetType, PlayerBattleState targetPlayer)
        {
            if (state == null || actingPlayer == null || card == null || targetPlayer == null || targetPlayer.hp <= 0)
            {
                return false;
            }

            var baseValid = false;
            switch (targetType)
            {
                case BattleTargetType.Self:
                    baseValid = targetPlayer.playerId == actingPlayer.playerId;
                    break;
                case BattleTargetType.SingleAlly:
                case BattleTargetType.AllAllies:
                    baseValid = true;
                    break;
                case BattleTargetType.OtherAlly:
                    baseValid = targetPlayer.playerId != actingPlayer.playerId;
                    break;
                case BattleTargetType.SingleUnit:
                    baseValid = true;
                    break;
            }

            if (!baseValid)
            {
                return false;
            }

            if (RequiresTargetWithCards(card) && targetPlayer.hand.Count == 0)
            {
                return false;
            }

            if (!BattleTargetResolver.IsPlayerDistanceInRange(card.rangeDistance, actingPlayer.area, targetPlayer.area))
            {
                return false;
            }

            return BattleTargetResolver.IsHeightInRange(card.rangeHeights, targetPlayer.height);
        }

        public static bool CanTargetPart(BattleState state, PlayerBattleState actingPlayer, CardDefinition card, BattleTargetType targetType, MonsterPartState part)
        {
            if (state == null || state.monster == null || actingPlayer == null || card == null || part == null)
            {
                return false;
            }

            var baseValid = targetType == BattleTargetType.MonsterPart
                || targetType == BattleTargetType.AllMonsterParts
                || targetType == BattleTargetType.SingleUnit;
            if (!baseValid)
            {
                return false;
            }

            return BattleTargetResolver.IsPartInRange(state.monster, part, card, actingPlayer.area);
        }

        public static bool TryParseTargetType(CardDefinition card, out BattleTargetType targetType)
        {
            targetType = BattleTargetType.None;
            if (card == null || string.IsNullOrEmpty(card.targetType))
            {
                return false;
            }

            try
            {
                targetType = (BattleTargetType)Enum.Parse(typeof(BattleTargetType), card.targetType, true);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool RequiresTargetWithCards(CardDefinition card)
        {
            if (card == null || card.parsedEffects == null)
            {
                return false;
            }

            foreach (var action in CardEffectCompiler.EnumerateActions(card.parsedEffects))
            {
                if (action != null && string.Equals(action.Head, "copy-and-plunder", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
