using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Battle
{
    internal static class BattleStatusSystem
    {
        public const string PoisonStatusId = "Poison";
        public const string VulnerableStatusId = "Vulnerable";
        public const string StrengthStatusId = "Strength";
        public const string TempArmorStatusId = "TempArmor";
        public const string SecretCounterattackStatusId = "SecretCounterattack";
        public const string SecretGuardStatusId = "SecretGuard";
        public const string SecretSidestepOnHitStatusId = "SecretSidestepOnHit";
        public const string SecretStrengthOnHitStatusId = "SecretStrengthOnHit";
        public const string SecretArmorOnHitStatusId = "SecretArmorOnHit";

        public static int GetDurationTurns(BattleStatusState status)
        {
            if (status == null || status.durationKind == BattleStatusDurationKind.Permanent)
            {
                return 0;
            }

            return Math.Max(1, status.durationTurns);
        }

        public static void SetDuration(BattleStatusState status, BattleStatusDurationKind durationKind, int durationTurns)
        {
            if (status == null)
            {
                return;
            }

            status.durationKind = durationKind;
            status.durationTurns = durationKind == BattleStatusDurationKind.Permanent
                ? 0
                : Math.Max(1, durationTurns);
        }

        public static int GetStacks(List<BattleStatusState> statuses, string statusId)
        {
            return GetStacks(statuses, statusId, null);
        }

        public static int GetStacks(List<BattleStatusState> statuses, string statusId, BattleStatusDurationKind? durationKind)
        {
            if (statuses == null || string.IsNullOrWhiteSpace(statusId))
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (status == null || !string.Equals(status.id, statusId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (durationKind.HasValue && status.durationKind != durationKind.Value)
                {
                    continue;
                }

                total += Math.Max(0, status.stacks);
            }

            return total;
        }

        public static int AddStacks(List<BattleStatusState> statuses, string statusId, int delta)
        {
            return AddStacks(statuses, statusId, delta, BattleStatusDurationKind.Permanent, 0);
        }

        public static int AddStacks(List<BattleStatusState> statuses, string statusId, int delta, BattleStatusDurationKind durationKind, int durationTurns)
        {
            if (statuses == null || string.IsNullOrWhiteSpace(statusId) || delta == 0)
            {
                return GetStacks(statuses, statusId);
            }

            if (delta < 0)
            {
                var remaining = Math.Abs(delta);
                for (var i = statuses.Count - 1; i >= 0 && remaining > 0; i--)
                {
                    var status = statuses[i];
                    if (status == null || !string.Equals(status.id, statusId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var removed = Math.Min(status.stacks, remaining);
                    status.stacks -= removed;
                    remaining -= removed;
                    if (status.stacks <= 0)
                    {
                        statuses.RemoveAt(i);
                    }
                }

                return GetStacks(statuses, statusId);
            }

            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (status == null || !string.Equals(status.id, statusId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (status.durationKind != durationKind)
                {
                    continue;
                }

                status.stacks = Math.Max(0, status.stacks + delta);
                if (status.stacks <= 0)
                {
                    statuses.RemoveAt(i);
                    return 0;
                }

                if (durationKind != BattleStatusDurationKind.Permanent)
                {
                    status.durationKind = durationKind;
                    if (durationKind == BattleStatusDurationKind.TurnCount)
                    {
                        status.durationTurns = Math.Max(status.durationTurns, Math.Max(1, durationTurns));
                    }
                    else
                    {
                        status.durationTurns = 1;
                    }
                }

                return GetStacks(statuses, statusId);
            }

            statuses.Add(new BattleStatusState
            {
                id = statusId,
                stacks = delta,
                durationKind = durationKind,
                durationTurns = durationKind == BattleStatusDurationKind.Permanent
                    ? 0
                    : durationKind == BattleStatusDurationKind.TurnCount
                        ? Math.Max(1, durationTurns)
                        : 1
            });
            return GetStacks(statuses, statusId);
        }

        public static int ConsumeStacks(List<BattleStatusState> statuses, string statusId)
        {
            var stacks = GetStacks(statuses, statusId);
            if (stacks > 0)
            {
                RemoveAllStacks(statuses, statusId);
            }

            return stacks;
        }

        public static int ConsumeStacks(List<BattleStatusState> statuses, string statusId, BattleStatusDurationKind durationKind)
        {
            var stacks = GetStacks(statuses, statusId, durationKind);
            if (stacks > 0)
            {
                RemoveAllStacks(statuses, statusId, durationKind);
            }

            return stacks;
        }

        public static int ConsumeStacks(PlayerBattleState player, string statusId)
        {
            return player == null ? 0 : ConsumeStacks(player.statuses, statusId);
        }

        public static int ConsumeStacks(PlayerBattleState player, string statusId, BattleStatusDurationKind durationKind)
        {
            return player == null ? 0 : ConsumeStacks(player.statuses, statusId, durationKind);
        }

        public static void TickTurnStartDurations(BattleState state, bool playerTurnStart)
        {
            if (state == null)
            {
                return;
            }

            if (playerTurnStart)
            {
                if (state.players == null)
                {
                    return;
                }

                for (var i = 0; i < state.players.Count; i++)
                {
                    TickTurnStartDurations(state.players[i]);
                }

                return;
            }

            if (state.monster != null)
            {
                TickTurnStartDurations(state.monster.statuses, null);
            }
        }

        private static void TickTurnStartDurations(PlayerBattleState player)
        {
            TickTurnStartDurations(player == null ? null : player.statuses, player);
        }

        private static void TickTurnStartDurations(List<BattleStatusState> statuses, PlayerBattleState owner)
        {
            if (statuses == null)
            {
                return;
            }

            for (var i = statuses.Count - 1; i >= 0; i--)
            {
                var status = statuses[i];
                if (status == null || status.durationKind == BattleStatusDurationKind.Permanent)
                {
                    continue;
                }

                if (status.durationKind == BattleStatusDurationKind.UntilTurnStart)
                {
                    if (string.Equals(status.id, TempArmorStatusId, StringComparison.OrdinalIgnoreCase) && owner != null)
                    {
                        owner.armor = Math.Max(0, owner.armor - Math.Max(0, status.stacks));
                    }

                    statuses.RemoveAt(i);
                    continue;
                }

                if (status.durationKind == BattleStatusDurationKind.TurnCount)
                {
                    status.durationTurns -= 1;
                    if (status.durationTurns <= 0)
                    {
                        if (string.Equals(status.id, TempArmorStatusId, StringComparison.OrdinalIgnoreCase) && owner != null)
                        {
                            owner.armor = Math.Max(0, owner.armor - Math.Max(0, status.stacks));
                        }

                        statuses.RemoveAt(i);
                    }
                    continue;
                }

                if (string.Equals(status.id, TempArmorStatusId, StringComparison.OrdinalIgnoreCase) && owner != null)
                {
                    owner.armor = Math.Max(0, owner.armor - Math.Max(0, status.stacks));
                }

                statuses.RemoveAt(i);
            }
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

        public static string ResolveStatusDisplayName(string statusId, bool detailedMode)
        {
            if (string.IsNullOrWhiteSpace(statusId))
            {
                return detailedMode ? "状态" : "状";
            }

            if (string.Equals(statusId, PoisonStatusId, StringComparison.OrdinalIgnoreCase))
            {
                return detailedMode ? "中毒" : "☠";
            }

            if (string.Equals(statusId, StrengthStatusId, StringComparison.OrdinalIgnoreCase))
            {
                return detailedMode ? "力量" : "💪";
            }

            if (string.Equals(statusId, TempArmorStatusId, StringComparison.OrdinalIgnoreCase))
            {
                return detailedMode ? "临时护甲" : "🛡";
            }

            if (string.Equals(statusId, VulnerableStatusId, StringComparison.OrdinalIgnoreCase))
            {
                return detailedMode ? "易伤" : "🎯";
            }

            return statusId + ":";
        }

        public static string BuildStatusLabel(BattleStatusState status, bool detailedMode)
        {
            if (status == null || status.stacks <= 0 || string.IsNullOrWhiteSpace(status.id))
            {
                return string.Empty;
            }

            return BuildStatusLabel(status.id, status.stacks, status.durationKind, status.durationTurns, detailedMode);
        }

        public static string BuildStatusLabel(string statusId, int stacks, BattleStatusDurationKind durationKind, int durationTurns, bool detailedMode)
        {
            if (stacks <= 0 || string.IsNullOrWhiteSpace(statusId))
            {
                return string.Empty;
            }

            var label = ResolveStatusDisplayName(statusId, detailedMode) + stacks;
            var durationSuffix = BuildDurationSuffix(durationKind, durationTurns, detailedMode);
            if (!string.IsNullOrEmpty(durationSuffix))
            {
                label += durationSuffix;
            }

            return label;
        }

        public static string BuildDurationSuffix(BattleStatusDurationKind durationKind, int durationTurns, bool detailedMode)
        {
            switch (durationKind)
            {
                case BattleStatusDurationKind.UntilTurnStart:
                    return detailedMode ? "（至下次行动开始）" : "（至下回合）";
                case BattleStatusDurationKind.TurnCount:
                {
                    var turns = Math.Max(1, durationTurns);
                    return detailedMode ? "（持续" + turns + "回合）" : "（" + turns + "回合）";
                }
                default:
                    return string.Empty;
            }
        }

        private static void RemoveAllStacks(List<BattleStatusState> statuses, string statusId)
        {
            if (statuses == null || string.IsNullOrWhiteSpace(statusId))
            {
                return;
            }

            for (var i = statuses.Count - 1; i >= 0; i--)
            {
                var status = statuses[i];
                if (status != null && string.Equals(status.id, statusId, StringComparison.OrdinalIgnoreCase))
                {
                    statuses.RemoveAt(i);
                }
            }
        }

        private static void RemoveAllStacks(List<BattleStatusState> statuses, string statusId, BattleStatusDurationKind durationKind)
        {
            if (statuses == null || string.IsNullOrWhiteSpace(statusId))
            {
                return;
            }

            for (var i = statuses.Count - 1; i >= 0; i--)
            {
                var status = statuses[i];
                if (status != null
                    && string.Equals(status.id, statusId, StringComparison.OrdinalIgnoreCase)
                    && status.durationKind == durationKind)
                {
                    statuses.RemoveAt(i);
                }
            }
        }
    }
}
