using System;
using System.Collections.Generic;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Battle
{
    internal static class CardEffectResolver
    {
        private const int MaxEffectOperationsPerCard = 128;

        private static readonly Dictionary<string, ICardEffectHandler> Handlers =
            new Dictionary<string, ICardEffectHandler>(StringComparer.OrdinalIgnoreCase)
            {
                { "Damage", new DamageEffectHandler() },
                { "Heal", new HealEffectHandler() },
                { "Draw", new DrawEffectHandler() },
                { "GainArmor", new GainArmorEffectHandler() },
                { "ApplyStatus", new ApplyStatusEffectHandler() },
                { "ModifyEnergy", new ModifyEnergyEffectHandler() },
                { "LoseHp", new LoseHpEffectHandler() },
                { "CopyAndPlunder", new CopyAndPlunderEffectHandler() },
                { "RecycleDiscardToHand", new RecycleDiscardToHandEffectHandler() },
                { "ExhaustFromHand", new ExhaustFromHandEffectHandler() },
                { "MoveArea", new MoveAreaEffectHandler() },
                { "ModifyThreat", new ModifyThreatEffectHandler() },
                { "AddSecret", new AddSecretEffectHandler() }
            };

        public static void ResolveCardEffects(
            BattleState state,
            PlayerBattleState actingPlayer,
            BattleCommand command,
            BattleCardState playedCard,
            CardDefinition definition,
            BattleTargetType cardTargetType,
            BattleCommandResult result)
        {
            if (definition == null)
            {
                result.error = "缺少卡牌定义。";
                return;
            }

            if (definition.parsedEffects == null)
            {
                result.error = "卡牌没有效果：" + definition.id;
                return;
            }

            var context = new CardEffectExecutionContext
            {
                state = state,
                actingPlayer = actingPlayer,
                command = command,
                playedCard = playedCard,
                definition = definition,
                cardTargetType = cardTargetType,
                result = result
            };

            var opCount = 0;
            try
            {
                if (!ExecuteDslNode(context, definition.parsedEffects, ref opCount, out var error))
                {
                    result.error = error;
                    return;
                }
            }
            catch (InvalidOperationException exception)
            {
                result.error = exception.Message;
                return;
            }

            if (opCount > MaxEffectOperationsPerCard)
            {
                result.error = "效果操作次数超出上限。";
            }
        }

        private static bool ExecuteDslNode(CardEffectExecutionContext context, SExpressionNode node, ref int opCount, out string error)
        {
            error = null;
            if (!(node is SExpressionListNode list))
            {
                error = "效果 DSL 需要列表节点。";
                return false;
            }

            if (string.Equals(list.Head, "do", StringComparison.Ordinal))
            {
                for (var i = 0; i < list.Arguments.Length; i++)
                {
                    if (!ExecuteDslNode(context, list.Arguments[i], ref opCount, out error))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (string.Equals(list.Head, "if", StringComparison.Ordinal))
            {
                if (list.Arguments.Length != 2)
                {
                    error = "if 需要 2 个参数。";
                    return false;
                }

                if (CardEffectEvaluator.EvaluateBoolean(context, list.Arguments[0], null))
                {
                    return ExecuteDslNode(context, list.Arguments[1], ref opCount, out error);
                }

                return true;
            }

            if (string.Equals(list.Head, "repeat", StringComparison.Ordinal))
            {
                if (list.Arguments.Length != 2)
                {
                    error = "repeat 需要 2 个参数。";
                    return false;
                }

                var times = Math.Max(0, (int)Math.Round(CardEffectEvaluator.EvaluateNumber(context, list.Arguments[0], null)));
                for (var i = 0; i < times; i++)
                {
                    if (!ExecuteDslNode(context, list.Arguments[1], ref opCount, out error))
                    {
                        return false;
                    }
                }

                return true;
            }

            return ExecuteActionNode(context, list, ref opCount, out error);
        }

        private static bool ExecuteActionNode(CardEffectExecutionContext context, SExpressionListNode action, ref int opCount, out string error)
        {
            error = null;
            if (!CardEffectActionCompiler.TryBuildActionPrototype(action, out var effect, out error))
            {
                return false;
            }

            if (!Handlers.TryGetValue(effect.op, out var handler))
            {
                error = "未知效果操作：" + effect.op;
                return false;
            }

            var targets = ResolveTargets(context, effect, out error);
            if (!string.IsNullOrEmpty(error))
            {
                return false;
            }

            if (handler.RequiresTarget && targets.Count == 0)
            {
                error = "效果需要目标：" + effect.op;
                return false;
            }

            context.lastResult.Reset();

            if (targets.Count == 0)
            {
                return ExecuteHandler(context, handler, effect, targets, ref opCount, out error);
            }

            var totalDamageDealt = 0;
            var killed = false;
            for (var i = 0; i < targets.Count; i++)
            {
                var singleTarget = new List<CardEffectTargetRef>(1) { targets[i] };
                var materialized = CardEffectActionCompiler.MaterializeEffectForTarget(context, action, effect, targets[i]);

                if (!ExecuteHandler(context, handler, materialized, singleTarget, ref opCount, out error))
                {
                    return false;
                }

                totalDamageDealt += context.lastResult.damageDealt;
                killed |= context.lastResult.killedTarget;
            }

            context.lastResult.Set(totalDamageDealt, killed);
            return true;
        }

        private static bool ExecuteHandler(
            CardEffectExecutionContext context,
            ICardEffectHandler handler,
            CardEffectDefinition effect,
            List<CardEffectTargetRef> targets,
            ref int opCount,
            out string error)
        {
            var beforePlayerHp = targets.Count > 0 && targets[0].IsPlayer ? targets[0].Player.hp : 0;
            var beforeMonsterCoreHp = context.state.monster == null ? 0 : context.state.monster.coreHp;

            if (!handler.Execute(context, effect, targets, ref opCount, out error))
            {
                return false;
            }

            if (opCount > MaxEffectOperationsPerCard)
            {
                error = "效果操作次数超出上限。";
                return false;
            }

            context.lastResult.Reset();
            if (string.Equals(effect.op, "Damage", StringComparison.OrdinalIgnoreCase))
            {
                if (targets.Count > 0 && targets[0].IsPlayer)
                {
                    context.lastResult.Set(
                        Math.Max(0, beforePlayerHp - targets[0].Player.hp),
                        beforePlayerHp > 0 && targets[0].Player.hp <= 0);
                }
                else
                {
                    var currentMonsterCoreHp = context.state.monster == null ? 0 : context.state.monster.coreHp;
                    context.lastResult.Set(
                        Math.Max(0, beforeMonsterCoreHp - currentMonsterCoreHp),
                        beforeMonsterCoreHp > 0 && currentMonsterCoreHp <= 0);
                }
            }

            return true;
        }

        private static List<CardEffectTargetRef> ResolveTargets(CardEffectExecutionContext context, CardEffectDefinition effect, out string error)
        {
            error = null;
            var targets = new List<CardEffectTargetRef>();
            var targetMode = string.IsNullOrWhiteSpace(effect.target) ? "CardTarget" : effect.target;

            if (string.Equals(targetMode, "None", StringComparison.OrdinalIgnoreCase))
            {
                return targets;
            }

            if (string.Equals(targetMode, "Self", StringComparison.OrdinalIgnoreCase))
            {
                targets.Add(CardEffectTargetRef.ForPlayer(context.actingPlayer));
                return targets;
            }

            if (string.Equals(targetMode, "AllAllies", StringComparison.OrdinalIgnoreCase))
            {
                for (var i = 0; i < context.state.players.Count; i++)
                {
                    var player = context.state.players[i];
                    if (player != null && player.hp > 0
                        && BattleTargetingRules.CanTargetPlayer(context.state, context.actingPlayer, context.definition, BattleTargetType.AllAllies, player))
                    {
                        targets.Add(CardEffectTargetRef.ForPlayer(player));
                    }
                }

                return targets;
            }

            if (string.Equals(targetMode, "AllEnemies", StringComparison.OrdinalIgnoreCase))
            {
                if (context.state.monster != null)
                {
                    for (var i = 0; i < context.state.monster.parts.Count; i++)
                    {
                        var part = context.state.monster.parts[i];
                        if (part != null)
                        {
                            targets.Add(CardEffectTargetRef.ForPart(part));
                        }
                    }
                }

                return targets;
            }

            switch (context.cardTargetType)
            {
                case BattleTargetType.Self:
                    targets.Add(CardEffectTargetRef.ForPlayer(context.actingPlayer));
                    return targets;

                case BattleTargetType.SingleAlly:
                case BattleTargetType.OtherAlly:
                {
                    var disallowSelf = context.cardTargetType == BattleTargetType.OtherAlly;
                    var playerTarget = BattleTargetResolver.ResolvePlayerTarget(context.state, context.command, disallowSelf);
                    if (playerTarget == null || !BattleTargetingRules.CanTargetPlayer(context.state, context.actingPlayer, context.definition, context.cardTargetType, playerTarget))
                    {
                        error = "玩家目标无效。";
                        return targets;
                    }

                    targets.Add(CardEffectTargetRef.ForPlayer(playerTarget));
                    return targets;
                }

                case BattleTargetType.SingleUnit:
                {
                    var playerTarget = BattleTargetResolver.ResolvePlayerTarget(context.state, context.command, false);
                    if (playerTarget != null && BattleTargetingRules.CanTargetPlayer(context.state, context.actingPlayer, context.definition, context.cardTargetType, playerTarget))
                    {
                        targets.Add(CardEffectTargetRef.ForPlayer(playerTarget));
                        return targets;
                    }

                    var partTarget = BattleTargetResolver.ResolvePartTarget(context.state, context.command);
                    if (partTarget == null || !BattleTargetingRules.CanTargetPart(context.state, context.actingPlayer, context.definition, context.cardTargetType, partTarget))
                    {
                        error = "单位目标无效。";
                        return targets;
                    }

                    targets.Add(CardEffectTargetRef.ForPart(partTarget));
                    return targets;
                }

                case BattleTargetType.MonsterPart:
                {
                    var partTarget = BattleTargetResolver.ResolvePartTarget(context.state, context.command);
                    if (partTarget == null || !BattleTargetingRules.CanTargetPart(context.state, context.actingPlayer, context.definition, context.cardTargetType, partTarget))
                    {
                        error = "部位目标无效。";
                        return targets;
                    }

                    targets.Add(CardEffectTargetRef.ForPart(partTarget));
                    return targets;
                }

                case BattleTargetType.AllMonsterParts:
                    if (context.state.monster == null)
                    {
                        error = "没有敌方目标。";
                        return targets;
                    }

                    for (var i = 0; i < context.state.monster.parts.Count; i++)
                    {
                        var part = context.state.monster.parts[i];
                        if (part == null)
                        {
                            continue;
                        }

                        if (BattleTargetingRules.CanTargetPart(context.state, context.actingPlayer, context.definition, context.cardTargetType, part))
                        {
                            targets.Add(CardEffectTargetRef.ForPart(part));
                        }
                    }

                    return targets;

                case BattleTargetType.Area:
                    return targets;

                default:
                    error = "不支持的卡牌目标类型：" + context.cardTargetType;
                    return targets;
            }
        }

        private static int GetStrengthBonus(PlayerBattleState player)
        {
            return BattleStatusSystem.GetStacks(player.statuses, BattleStatusSystem.StrengthStatusId)
                + BattleStatusSystem.GetStacks(player.statuses, BattleStatusSystem.TempStrengthStatusId);
        }

        private interface ICardEffectHandler
        {
            bool RequiresTarget { get; }

            bool Execute(
                CardEffectExecutionContext context,
                CardEffectDefinition effect,
                List<CardEffectTargetRef> targets,
                ref int opCount,
                out string error);
        }

        private sealed class DamageEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                var strength = GetStrengthBonus(context.actingPlayer);
                for (var i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    var amount = Math.Max(0, effect.amount + strength);
                    if (target.IsPlayer)
                    {
                        var applied = BattleMechanics.ApplyDamage(target.Player, amount);
                        context.result.events.Add(new BattleEvent
                        {
                            eventId = "card_damage",
                            actor = context.actingPlayer.displayName,
                            cardId = context.definition.name,
                            target = target.Player.displayName,
                            amount = applied
                        });
                    }
                    else
                    {
                        var applied = BattleMechanics.ApplyDamageToPart(context.state, target.Part, amount, context.result);
                        BattleThreatSystem.ApplyThreatFromDamage(context.state, context.actingPlayer, applied);
                        context.result.events.Add(new BattleEvent
                        {
                            eventId = "card_damage",
                            actor = context.actingPlayer.displayName,
                            cardId = context.definition.name,
                            target = target.Part.displayName,
                            amount = applied
                        });
                    }

                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class DrawEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                for (var i = 0; i < targets.Count; i++)
                {
                    if (!targets[i].IsPlayer)
                    {
                        continue;
                    }

                    var drawn = BattleMechanics.DrawCards(context.state, targets[i].Player, Math.Max(0, effect.amount));
                    context.result.events.Add(new BattleEvent
                    {
                        eventId = "draw_cards",
                        target = targets[i].Player.displayName,
                        amount = drawn
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class HealEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                for (var i = 0; i < targets.Count; i++)
                {
                    if (!targets[i].IsPlayer)
                    {
                        continue;
                    }

                    var healed = BattleMechanics.Heal(targets[i].Player, Math.Max(0, effect.amount));
                    context.result.events.Add(new BattleEvent
                    {
                        eventId = "heal",
                        target = targets[i].Player.displayName,
                        amount = healed
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class GainArmorEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                for (var i = 0; i < targets.Count; i++)
                {
                    if (!targets[i].IsPlayer)
                    {
                        continue;
                    }

                    var amount = Math.Max(0, effect.amount);
                    targets[i].Player.armor += amount;
                    context.result.events.Add(new BattleEvent
                    {
                        eventId = "gain_armor",
                        target = targets[i].Player.displayName,
                        amount = amount
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class ApplyStatusEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                if (string.IsNullOrWhiteSpace(effect.statusId))
                {
                    error = "施加状态时缺少 statusId。";
                    return false;
                }

                var amount = Math.Max(1, effect.amount);
                if (string.Equals(effect.statusId, BattleStatusSystem.VulnerableStatusId, StringComparison.OrdinalIgnoreCase))
                {
                    for (var i = 0; i < targets.Count; i++)
                    {
                        if (!targets[i].IsPlayer)
                        {
                            continue;
                        }

                        BattleStatusSystem.AddStacks(targets[i].Player.statuses, BattleStatusSystem.VulnerableStatusId, amount);
                        context.result.events.Add(new BattleEvent
                        {
                            eventId = "apply_vulnerable",
                            target = targets[i].Player.displayName,
                            amount = amount
                        });
                        opCount += 1;
                    }

                    return true;
                }

                for (var i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    if (target.IsPlayer)
                    {
                        var total = BattleStatusSystem.AddStacks(target.Player.statuses, effect.statusId, amount);
                        context.result.events.Add(new BattleEvent
                        {
                            eventId = "apply_status",
                            target = target.Player.displayName,
                            statusId = effect.statusId,
                            amount = amount,
                            amount2 = total
                        });
                    }
                    else
                    {
                        var total = BattleStatusSystem.AddStacks(context.state.monster == null ? null : context.state.monster.statuses, effect.statusId, amount);
                        context.result.events.Add(new BattleEvent
                        {
                            eventId = "apply_status",
                            target = context.state.monster == null ? "怪物" : context.state.monster.displayName,
                            statusId = effect.statusId,
                            amount = amount,
                            amount2 = total
                        });
                    }

                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class ModifyThreatEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                for (var i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    if (!target.IsPlayer)
                    {
                        error = "ModifyThreat 只能作用于玩家。";
                        return false;
                    }

                    var total = BattleThreatSystem.ApplyThreatGain(context.state, target.Player, effect.amount);
                    context.result.events.Add(new BattleEvent
                    {
                        eventId = "threat_change",
                        target = target.Player.displayName,
                        amount = effect.amount,
                        amount2 = total,
                        turn = target.Player.threatTier
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class AddSecretEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                if (string.IsNullOrWhiteSpace(effect.statusId))
                {
                    error = "添加奥秘时缺少 statusId。";
                    return false;
                }

                var amount = Math.Max(1, effect.amount);
                for (var i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    if (!target.IsPlayer)
                    {
                        error = "AddSecret 只能作用于玩家。";
                        return false;
                    }

                    var total = BattleStatusSystem.AddStacks(target.Player.statuses, effect.statusId, amount);
                    context.result.events.Add(new BattleEvent
                    {
                        eventId = "gain_secret",
                        target = target.Player.displayName,
                        statusId = effect.statusId,
                        amount = amount,
                        amount2 = total
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class ModifyEnergyEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => false;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                context.actingPlayer.energy = Math.Max(0, context.actingPlayer.energy + effect.amount);
                context.result.events.Add(new BattleEvent
                {
                    eventId = "modify_energy",
                    target = context.actingPlayer.displayName,
                    amount = effect.amount
                });
                opCount += 1;
                return true;
            }
        }

        private sealed class LoseHpEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                for (var i = 0; i < targets.Count; i++)
                {
                    if (!targets[i].IsPlayer)
                    {
                        continue;
                    }

                    var player = targets[i].Player;
                    var lose = Math.Max(0, effect.amount);
                    var before = player.hp;
                    player.hp = Math.Max(0, player.hp - lose);
                    var applied = before - player.hp;
                    context.result.events.Add(new BattleEvent
                    {
                        eventId = "lose_hp",
                        target = player.displayName,
                        amount = applied
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class RecycleDiscardToHandEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => false;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                var count = Math.Max(0, effect.amount);
                for (var i = 0; i < count; i++)
                {
                    if (context.actingPlayer.discardPile.Count == 0)
                    {
                        break;
                    }

                    var index = context.actingPlayer.discardPile.Count - 1;
                    var card = context.actingPlayer.discardPile[index];
                    context.actingPlayer.discardPile.RemoveAt(index);
                    card.costDelta += effect.amount2;

                    if (context.actingPlayer.hand.Count >= BattleEngine.MaxHandSize)
                    {
                        context.actingPlayer.discardPile.Add(card);
                        continue;
                    }

                    context.actingPlayer.hand.Add(card);
                    context.result.events.Add(new BattleEvent
                    {
                        eventId = "recycle_from_discard",
                        target = context.actingPlayer.displayName,
                        cardId = card.cardId
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class CopyAndPlunderEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                if (targets == null || targets.Count == 0)
                {
                    error = "复制并掠夺需要目标。";
                    return false;
                }

                var target = targets[0];
                if (!target.IsPlayer || target.Player == null)
                {
                    error = "复制并掠夺需要友方目标。";
                    return false;
                }

                if (target.Player.hand.Count == 0)
                {
                    error = target.Player.displayName + " 没有可掠夺的卡牌。";
                    return false;
                }

                var random = new Random(
                    context.state.randomSeed
                    ^ context.state.turnIndex
                    ^ context.actingPlayer.playerId.GetHashCode()
                    ^ target.Player.playerId.GetHashCode()
                    ^ target.Player.hand.Count);
                var stolenIndex = random.Next(target.Player.hand.Count);
                var stolenCard = target.Player.hand[stolenIndex];

                target.Player.hand.RemoveAt(stolenIndex);
                target.Player.discardPile.Add(stolenCard);

                var copiedCard = new BattleCardState
                {
                    instanceId = context.actingPlayer.playerId + "_plundered_" + stolenCard.cardId + "_" + Guid.NewGuid().ToString("N"),
                    cardId = stolenCard.cardId,
                    costDelta = 0
                };

                if (context.actingPlayer.hand.Count >= BattleEngine.MaxHandSize)
                {
                    context.actingPlayer.discardPile.Add(copiedCard);
                }
                else
                {
                    context.actingPlayer.hand.Add(copiedCard);
                }

                context.result.events.Add(new BattleEvent
                {
                    eventId = "copy_and_plunder",
                    actor = context.actingPlayer.displayName,
                    cardId = context.definition.name,
                    target = target.Player.displayName
                });
                opCount += 1;
                return true;
            }
        }

        private sealed class ExhaustFromHandEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => false;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                var count = Math.Max(0, effect.amount);
                for (var i = 0; i < count; i++)
                {
                    var idx = FindExhaustCandidateIndex(context.actingPlayer.hand, context.playedCard == null ? null : context.playedCard.instanceId);
                    if (idx < 0)
                    {
                        break;
                    }

                    var exhausted = context.actingPlayer.hand[idx];
                    context.actingPlayer.hand.RemoveAt(idx);
                    context.actingPlayer.exhaustPile.Add(exhausted);
                    context.result.events.Add(new BattleEvent
                    {
                        eventId = "exhaust_card",
                        target = context.actingPlayer.displayName,
                        cardId = exhausted.cardId
                    });
                    opCount += 1;
                }

                return true;
            }

            private static int FindExhaustCandidateIndex(List<BattleCardState> hand, string playedCardInstanceId)
            {
                if (hand == null)
                {
                    return -1;
                }

                for (var i = 0; i < hand.Count; i++)
                {
                    if (hand[i] == null)
                    {
                        continue;
                    }

                    if (!string.Equals(hand[i].instanceId, playedCardInstanceId, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        private sealed class MoveAreaEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => false;

            public bool Execute(CardEffectExecutionContext context, CardEffectDefinition effect, List<CardEffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                var destination = ResolveDestination(effect.destinationId, context);
                if (!IsValidDestination(context.actingPlayer, destination))
                {
                    error = "目标区域无效。";
                    return false;
                }

                context.actingPlayer.area = destination;
                context.result.events.Add(new BattleEvent
                {
                    eventId = "move_area",
                    actor = context.actingPlayer.displayName,
                    area = destination
                });
                opCount += 1;
                return true;
            }

            private static BattleArea ResolveDestination(string destinationId, CardEffectExecutionContext context)
            {
                if (string.Equals(destinationId, "selected-area", StringComparison.OrdinalIgnoreCase))
                {
                    return context.command.targetArea;
                }

                if (string.Equals(destinationId, "another-side-area", StringComparison.OrdinalIgnoreCase))
                {
                    switch (context.actingPlayer.area)
                    {
                        case BattleArea.West:
                            return BattleArea.East;
                        case BattleArea.East:
                            return BattleArea.West;
                    }
                }

                return BattleArea.Middle;
            }

            private static bool IsValidDestination(PlayerBattleState actingPlayer, BattleArea destination)
            {
                if (actingPlayer == null)
                {
                    return false;
                }

                if (destination != BattleArea.West && destination != BattleArea.East)
                {
                    return false;
                }

                return actingPlayer.area != destination;
            }
        }
    }
}
