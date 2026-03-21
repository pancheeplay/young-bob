using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Battle
{
    internal static class BattleStatusSystem
    {
        public const string PoisonStatusId = "Poison";
        public const string VulnerableStatusId = "Vulnerable";
        public const string StrengthStatusId = "Strength";
        public const string TempStrengthStatusId = "TempStrength";
        public const string SecretCounterattackStatusId = "SecretCounterattack";
        public const string SecretGuardStatusId = "SecretGuard";
        public const string SecretSidestepOnHitStatusId = "SecretSidestepOnHit";
        public const string SecretStrengthOnHitStatusId = "SecretStrengthOnHit";
        public const string SecretArmorOnHitStatusId = "SecretArmorOnHit";

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

        public static int ConsumeStacks(List<BattleStatusState> statuses, string statusId)
        {
            var stacks = GetStacks(statuses, statusId);
            if (stacks > 0)
            {
                AddStacks(statuses, statusId, -stacks);
            }

            return stacks;
        }

        public static int ConsumeStacks(PlayerBattleState player, string statusId)
        {
            return player == null ? 0 : ConsumeStacks(player.statuses, statusId);
        }

        public static int GetVulnerableStacks(PlayerBattleState player)
        {
            return player == null ? 0 : GetStacks(player.statuses, VulnerableStatusId);
        }

        public static void TickPoisonOnMonsterAtTurnStart(BattleState state, BattleCommandResult result)
        {
            if (state == null || state.monster == null)
            {
                return;
            }

            var poison = GetStacks(state.monster.statuses, PoisonStatusId);
            if (poison <= 0)
            {
                return;
            }

            var applied = BattleMechanics.ApplyDamageToMonsterCore(state.monster, poison);
            AddStacks(state.monster.statuses, PoisonStatusId, -1);
            result.events.Add(new BattleEvent
            {
                eventId = "poison_damage",
                target = state.monster.displayName,
                amount = applied
            });
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
                    eventId = "poison_damage",
                    target = player.displayName,
                    amount = applied
                });
            }
        }
    }
}
