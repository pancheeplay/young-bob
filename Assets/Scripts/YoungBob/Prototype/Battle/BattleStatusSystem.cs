using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Battle
{
    internal static class BattleStatusSystem
    {
        public const string PoisonStatusId = "Poison";
        public const string StrengthStatusId = "Strength";

        public static int GetStacks(List<BattleStatusState> statuses, string statusId)
        {
            if (statuses == null || string.IsNullOrWhiteSpace(statusId))
            {
                return 0;
            }

            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (status != null && string.Equals(status.id, statusId, StringComparison.OrdinalIgnoreCase))
                {
                    return Math.Max(0, status.stacks);
                }
            }

            return 0;
        }

        public static int AddStacks(List<BattleStatusState> statuses, string statusId, int delta)
        {
            if (statuses == null || string.IsNullOrWhiteSpace(statusId) || delta == 0)
            {
                return GetStacks(statuses, statusId);
            }

            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (status == null || !string.Equals(status.id, statusId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                status.stacks = Math.Max(0, status.stacks + delta);
                if (status.stacks <= 0)
                {
                    statuses.RemoveAt(i);
                    return 0;
                }

                return status.stacks;
            }

            if (delta <= 0)
            {
                return 0;
            }

            statuses.Add(new BattleStatusState
            {
                id = statusId,
                stacks = delta
            });
            return delta;
        }

        public static void TickPoisonOnMonsterAtTurnStart(BattleState state, BattleCommandResult result)
        {
            if (state == null || state.monster == null || state.monster.parts == null)
            {
                return;
            }

            for (var i = 0; i < state.monster.parts.Count; i++)
            {
                var part = state.monster.parts[i];
                if (part == null)
                {
                    continue;
                }

                var poison = GetStacks(part.statuses, PoisonStatusId);
                if (poison <= 0)
                {
                    continue;
                }

                var applied = BattleMechanics.ApplyDamageToPart(state, part, poison, result);
                AddStacks(part.statuses, PoisonStatusId, -1);
                result.events.Add(new BattleEvent
                {
                    message = BattleTextHelper.Unit(part.displayName) + " 受到" + BattleTextHelper.DamageText(applied) + "，来自中毒。"
                });
            }
        }

        public static void TickPoisonOnPlayersAtTurnStart(BattleState state, BattleCommandResult result)
        {
            if (state == null || state.players == null)
            {
                return;
            }

            for (var i = 0; i < state.players.Count; i++)
            {
                var player = state.players[i];
                if (player == null || player.hp <= 0)
                {
                    continue;
                }

                var poison = GetStacks(player.statuses, PoisonStatusId);
                if (poison <= 0)
                {
                    continue;
                }

                var applied = BattleMechanics.ApplyDamage(player, poison);
                AddStacks(player.statuses, PoisonStatusId, -1);
                result.events.Add(new BattleEvent
                {
                    message = BattleTextHelper.Unit(player.displayName) + " 受到" + BattleTextHelper.DamageText(applied) + "，来自中毒。"
                });
            }
        }
    }
}
