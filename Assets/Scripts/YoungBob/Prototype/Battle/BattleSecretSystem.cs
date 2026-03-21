using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Battle
{
    internal static class BattleSecretSystem
    {
        public static PlayerBattleState ResolveMonsterAttackTarget(BattleState state, PlayerBattleState intendedTarget)
        {
            return ResolveGuardTarget(state, intendedTarget);
        }

        public static void ConsumeGuardSecret(PlayerBattleState player, int turnIndex)
        {
            if (player == null)
            {
                return;
            }

            player.secretGuardLastTriggerTurn = turnIndex;
            BattleStatusSystem.ConsumeStacks(player, BattleStatusSystem.SecretGuardStatusId);
        }

        public static void TriggerPostHitSecrets(BattleState state, PlayerBattleState target, BattleCommandResult result, string attackName)
        {
            if (state == null || state.monster == null || target == null)
            {
                return;
            }

            if (TryTriggerSidestep(state, target, state.turnIndex, result, attackName))
            {
                // Side-step has already been consumed and logged.
            }

            TryTriggerArmorOnHit(target, result, attackName);
            TryTriggerStrengthOnHit(target, result, attackName);
            TryTriggerCounterattack(state, target, state.turnIndex, result, attackName);
        }

        public static void ResolveMonsterAttackOnPlayer(
            BattleState state,
            PlayerBattleState intendedTarget,
            int rawDamage,
            BattleCommandResult result,
            string attackName)
        {
            if (state == null || state.monster == null || intendedTarget == null)
            {
                return;
            }

            var actualTarget = ResolveGuardTarget(state, intendedTarget);
            if (actualTarget != intendedTarget)
            {
                ConsumeGuardSecret(actualTarget, state.turnIndex);
                result.events.Add(new BattleEvent
                {
                    eventId = "secret_guard_redirect",
                    target = actualTarget.displayName,
                    actor = intendedTarget.displayName,
                    statusId = BattleStatusSystem.SecretGuardStatusId
                });
            }

            var appliedDamage = BattleMechanics.ApplyDamage(actualTarget, Math.Max(0, rawDamage));
            result.events.Add(new BattleEvent
            {
                eventId = "monster_hit",
                target = state.monster.displayName,
                actor = actualTarget.displayName,
                cardId = attackName,
                amount = appliedDamage
            });

            TriggerPostHitSecrets(state, actualTarget, result, attackName);
        }

        public static void ResolveMonsterAttackOnPlayer(
            BattleState state,
            PlayerBattleState intendedTarget,
            int rawDamage,
            BattleCommandResult result,
            BattleCardState attackCard)
        {
            var attackName = attackCard == null ? "攻击" : attackCard.cardId;
            ResolveMonsterAttackOnPlayer(state, intendedTarget, rawDamage, result, attackName);
        }

        private static PlayerBattleState ResolveGuardTarget(BattleState state, PlayerBattleState intendedTarget)
        {
            if (state == null || state.players == null || intendedTarget == null)
            {
                return intendedTarget;
            }

            for (var i = 0; i < state.players.Count; i++)
            {
                var player = state.players[i];
                if (player == null || player.hp <= 0)
                {
                    continue;
                }

                if (player.playerId == intendedTarget.playerId)
                {
                    continue;
                }

                if (BattleStatusSystem.GetStacks(player.statuses, BattleStatusSystem.SecretGuardStatusId) <= 0)
                {
                    continue;
                }

                if (player.secretGuardLastTriggerTurn == state.turnIndex)
                {
                    continue;
                }

                return player;
            }

            return intendedTarget;
        }

        private static bool TryTriggerSidestep(BattleState state, PlayerBattleState target, int turnIndex, BattleCommandResult result, string attackName)
        {
            if (BattleStatusSystem.GetStacks(target.statuses, BattleStatusSystem.SecretSidestepOnHitStatusId) <= 0)
            {
                return false;
            }

            if (target.secretSidestepLastTriggerTurn == turnIndex)
            {
                return false;
            }

            target.secretSidestepLastTriggerTurn = turnIndex;
            BattleStatusSystem.ConsumeStacks(target, BattleStatusSystem.SecretSidestepOnHitStatusId);
            var nextArea = GetOppositeArea(target.area);
            var previousArea = target.area;
            target.area = nextArea;
            result.events.Add(new BattleEvent
            {
                eventId = "secret_sidestep",
                target = target.displayName,
                statusId = BattleStatusSystem.SecretSidestepOnHitStatusId,
                cardId = attackName,
                area = nextArea
            });
            if (previousArea != nextArea)
            {
                result.events.Add(new BattleEvent
                {
                    eventId = "secret_moved",
                    target = target.displayName,
                    context = AreaToText(previousArea),
                    area = nextArea
                });
            }

            return true;
        }

        private static void TryTriggerCounterattack(BattleState state, PlayerBattleState target, int turnIndex, BattleCommandResult result, string attackName)
        {
            var stacks = BattleStatusSystem.GetStacks(target.statuses, BattleStatusSystem.SecretCounterattackStatusId);
            if (stacks <= 0)
            {
                return;
            }

            if (target.secretCounterattackLastTriggerTurn == turnIndex)
            {
                return;
            }

            target.secretCounterattackLastTriggerTurn = turnIndex;
            BattleStatusSystem.ConsumeStacks(target, BattleStatusSystem.SecretCounterattackStatusId);

            var applied = BattleMechanics.ApplyDamageToMonsterCore(state.monster, stacks);
            result.events.Add(new BattleEvent
            {
                eventId = "secret_counter",
                target = target.displayName,
                actor = state.monster.displayName,
                statusId = BattleStatusSystem.SecretCounterattackStatusId,
                amount = applied
            });
        }

        private static void TryTriggerStrengthOnHit(PlayerBattleState target, BattleCommandResult result, string attackName)
        {
            var stacks = BattleStatusSystem.ConsumeStacks(target, BattleStatusSystem.SecretStrengthOnHitStatusId);
            if (stacks <= 0)
            {
                return;
            }

            var totalStrength = BattleStatusSystem.AddStacks(target.statuses, BattleStatusSystem.TempStrengthStatusId, stacks);
            result.events.Add(new BattleEvent
            {
                eventId = "secret_gain_strength",
                target = target.displayName,
                statusId = BattleStatusSystem.SecretStrengthOnHitStatusId,
                cardId = attackName,
                amount = stacks,
                amount2 = totalStrength
            });
        }

        private static void TryTriggerArmorOnHit(PlayerBattleState target, BattleCommandResult result, string attackName)
        {
            var stacks = BattleStatusSystem.ConsumeStacks(target, BattleStatusSystem.SecretArmorOnHitStatusId);
            if (stacks <= 0)
            {
                return;
            }

            target.armor += stacks;
            result.events.Add(new BattleEvent
            {
                eventId = "secret_gain_armor",
                target = target.displayName,
                statusId = BattleStatusSystem.SecretArmorOnHitStatusId,
                cardId = attackName,
                amount = stacks,
                amount2 = target.armor
            });
        }

        private static BattleArea GetOppositeArea(BattleArea area)
        {
            switch (area)
            {
                case BattleArea.West:
                    return BattleArea.East;
                case BattleArea.East:
                    return BattleArea.West;
                default:
                    return BattleArea.West;
            }
        }

        private static string AreaToText(BattleArea area)
        {
            switch (area)
            {
                case BattleArea.West:
                    return "西侧";
                case BattleArea.East:
                    return "东侧";
                case BattleArea.Middle:
                    return "中间";
                default:
                    return "未知";
            }
        }
    }
}
