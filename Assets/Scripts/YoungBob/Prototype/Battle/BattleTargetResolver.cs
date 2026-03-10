using System;
using System.Collections.Generic;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Battle
{
    internal static class BattleTargetResolver
    {
        public static PlayerBattleState ResolveHeroTargetForUtility(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, BattleTargetType targetType, bool allowSelf, bool allowOtherAlly)
        {
            switch (targetType)
            {
                case BattleTargetType.Self:
                    return allowSelf ? actingPlayer : null;
                case BattleTargetType.SingleAlly:
                    return ResolvePlayerTarget(state, command, false);
                case BattleTargetType.OtherAlly:
                    return allowOtherAlly ? ResolvePlayerTarget(state, command, true) : null;
                default:
                    return null;
            }
        }

        public static PlayerBattleState ResolvePlayerTarget(BattleState state, BattleCommand command, bool disallowSelf)
        {
            if (command.targetFaction != BattleTargetFaction.Allies || string.IsNullOrEmpty(command.targetUnitId))
            {
                return null;
            }

            var target = state.GetPlayer(command.targetUnitId);
            if (target == null || target.hp <= 0)
            {
                return null;
            }

            if (disallowSelf && target.playerId == command.actorPlayerId)
            {
                return null;
            }

            return target;
        }

        public static MonsterPartState ResolvePartTarget(BattleState state, BattleCommand command)
        {
            if (command.targetFaction != BattleTargetFaction.Enemies || string.IsNullOrEmpty(command.targetUnitId))
            {
                return null;
            }

            var target = state.GetPart(command.targetUnitId);
            return target != null && target.hp > 0 ? target : null;
        }

        public static BattleTargetType ParseTargetType(string raw)
        {
            return (BattleTargetType)Enum.Parse(typeof(BattleTargetType), raw, true);
        }

        public static BattleZone ParseZone(string raw)
        {
            return (BattleZone)Enum.Parse(typeof(BattleZone), raw, true);
        }

        public static BattleHeight ParseHeight(string raw)
        {
            return (BattleHeight)Enum.Parse(typeof(BattleHeight), raw, true);
        }

        public static bool IsPartInRange(MonsterBattleState monster, MonsterPartState part, CardDefinition definition, BattleArea actingPlayerArea)
        {
            var resolved = ResolvePartPosition(monster, part);
            if (!IsHeightInRange(definition.rangeHeights, resolved.height))
            {
                return false;
            }

            var distanceRange = string.IsNullOrEmpty(definition.rangeDistance)
                ? ConvertZoneRangeToDistanceRange(definition.rangeZones)
                : definition.rangeDistance;
            return IsDistanceInRange(distanceRange, monster, part, actingPlayerArea);
        }

        public static (BattleHeight height, BattleZone zone) ResolvePartPosition(MonsterBattleState monster, MonsterPartState part)
        {
            var height = part.relativeHeight;
            var zone = part.relativeZone;

            if (monster.stance == BattleStance.Prone)
            {
                height = BattleHeight.Ground;
            }

            if (monster.facing == BattleFacing.West)
            {
                zone = zone == BattleZone.Front ? BattleZone.Back : BattleZone.Front;
            }

            return (height, zone);
        }

        public static bool IsHeightInRange(string range, BattleHeight height)
        {
            if (string.IsNullOrEmpty(range))
            {
                return true;
            }

            if (string.Equals(range, "Both", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(range, height.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsZoneInRange(string range, BattleZone zone)
        {
            if (string.IsNullOrEmpty(range))
            {
                return true;
            }

            if (string.Equals(range, "Both", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(range, zone.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDistanceInRange(string range, MonsterBattleState monster, MonsterPartState part, BattleArea actingPlayerArea)
        {
            if (string.IsNullOrEmpty(range) || string.Equals(range, "Both", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var localX = ResolvePartLocalX(monster, part);
            if (Math.Abs(localX) <= 0.05f)
            {
                return true;
            }

            var isNear = IsNearSide(localX, actingPlayerArea);
            if (string.Equals(range, "Near", StringComparison.OrdinalIgnoreCase))
            {
                return isNear;
            }

            if (string.Equals(range, "Far", StringComparison.OrdinalIgnoreCase))
            {
                return !isNear;
            }

            return true;
        }

        private static float ResolvePartLocalX(MonsterBattleState monster, MonsterPartState part)
        {
            if (part == null)
            {
                return 0f;
            }

            var x = part.offsetX;
            if (monster != null && monster.facing == BattleFacing.West)
            {
                x = -x;
            }

            return x;
        }

        private static bool IsNearSide(float localX, BattleArea actingPlayerArea)
        {
            if (actingPlayerArea == BattleArea.East)
            {
                return localX >= 0f;
            }

            return localX <= 0f;
        }

        private static string ConvertZoneRangeToDistanceRange(string zoneRange)
        {
            if (string.IsNullOrEmpty(zoneRange))
            {
                return "Both";
            }

            if (string.Equals(zoneRange, "Front", StringComparison.OrdinalIgnoreCase))
            {
                return "Far";
            }

            if (string.Equals(zoneRange, "Back", StringComparison.OrdinalIgnoreCase))
            {
                return "Near";
            }

            return zoneRange;
        }

        public static PlayerBattleState FindLowestHpAlivePlayer(List<PlayerBattleState> players)
        {
            PlayerBattleState result = null;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player.hp <= 0)
                {
                    continue;
                }

                if (result == null || player.hp < result.hp)
                {
                    result = player;
                }
            }

            return result;
        }

        public static bool AllPlayersDead(List<PlayerBattleState> players)
        {
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].hp > 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool HaveAllAlivePlayersEnded(List<PlayerBattleState> players)
        {
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].hp > 0 && !players[i].hasEndedTurn)
                {
                    return false;
                }
            }

            return true;
        }

        public static void ResetTeamTurn(List<PlayerBattleState> players)
        {
            for (var i = 0; i < players.Count; i++)
            {
                players[i].hasEndedTurn = players[i].hp <= 0;
            }
        }
    }
}
