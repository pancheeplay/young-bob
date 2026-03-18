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
                { "ApplyVulnerable", new ApplyVulnerableEffectHandler() },
                { "DamageByArmor", new DamageByArmorEffectHandler() },
                { "ModifyEnergy", new ModifyEnergyEffectHandler() },
                { "LoseHp", new LoseHpEffectHandler() },
                { "CopyAndPlunder", new CopyAndPlunderEffectHandler() },
                { "RecycleDiscardToHand", new RecycleDiscardToHandEffectHandler() },
                { "ExhaustFromHand", new ExhaustFromHandEffectHandler() },
                { "MoveArea", new MoveAreaEffectHandler() }
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

            if (definition.effects == null || definition.effects.Length == 0)
            {
                result.error = "卡牌没有效果：" + definition.id;
                return;
            }

            var context = new EffectExecutionContext
            {
                state = state,
                actingPlayer = actingPlayer,
                command = command,
                playedCard = playedCard,
                definition = definition,
                cardTargetType = cardTargetType,
                result = result
            };

            var plan = new List<PlannedEffect>(definition.effects.Length);
            for (var i = 0; i < definition.effects.Length; i++)
            {
                var effect = definition.effects[i];
                if (effect == null || string.IsNullOrWhiteSpace(effect.op))
                {
                    result.error = "卡牌 " + definition.id + " 的第 " + i + " 个效果无效。";
                    return;
                }

                if (!Handlers.TryGetValue(effect.op, out var handler))
                {
                    result.error = "未知效果操作：" + effect.op;
                    return;
                }

                var targets = ResolveTargets(context, effect, out var targetError);
                if (!string.IsNullOrEmpty(targetError))
                {
                    result.error = targetError;
                    return;
                }

                if (handler.RequiresTarget && targets.Count == 0)
                {
                    result.error = "效果需要目标：" + effect.op;
                    return;
                }

                plan.Add(new PlannedEffect
                {
                    effect = effect,
                    handler = handler,
                    targets = targets
                });
            }

            var opCount = 0;
            for (var i = 0; i < plan.Count; i++)
            {
                var planned = plan[i];
                if (!planned.handler.Execute(context, planned.effect, planned.targets, ref opCount, out var error))
                {
                    result.error = error;
                    return;
                }

                if (opCount > MaxEffectOperationsPerCard)
                {
                    result.error = "效果操作次数超出上限。";
                    return;
                }
            }
        }

        private static List<EffectTargetRef> ResolveTargets(EffectExecutionContext context, CardEffectDefinition effect, out string error)
        {
            error = null;
            var targets = new List<EffectTargetRef>();
            var targetMode = string.IsNullOrWhiteSpace(effect.target) ? "CardTarget" : effect.target;

            if (string.Equals(targetMode, "None", StringComparison.OrdinalIgnoreCase))
            {
                return targets;
            }

            if (string.Equals(targetMode, "Self", StringComparison.OrdinalIgnoreCase))
            {
                targets.Add(EffectTargetRef.ForPlayer(context.actingPlayer));
                return targets;
            }

            if (string.Equals(targetMode, "AllAllies", StringComparison.OrdinalIgnoreCase))
            {
                for (var i = 0; i < context.state.players.Count; i++)
                {
                    var player = context.state.players[i];
                    if (player != null && player.hp > 0)
                    {
                        targets.Add(EffectTargetRef.ForPlayer(player));
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
                            targets.Add(EffectTargetRef.ForPart(part));
                        }
                    }
                }

                return targets;
            }

            switch (context.cardTargetType)
            {
                case BattleTargetType.Self:
                    targets.Add(EffectTargetRef.ForPlayer(context.actingPlayer));
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

                    targets.Add(EffectTargetRef.ForPlayer(playerTarget));
                    return targets;
                }

                case BattleTargetType.SingleUnit:
                {
                    var playerTarget = BattleTargetResolver.ResolvePlayerTarget(context.state, context.command, false);
                    if (playerTarget != null && BattleTargetingRules.CanTargetPlayer(context.state, context.actingPlayer, context.definition, context.cardTargetType, playerTarget))
                    {
                        targets.Add(EffectTargetRef.ForPlayer(playerTarget));
                        return targets;
                    }

                    var partTarget = BattleTargetResolver.ResolvePartTarget(context.state, context.command);
                    if (partTarget == null || !BattleTargetingRules.CanTargetPart(context.state, context.actingPlayer, context.definition, context.cardTargetType, partTarget))
                    {
                        error = "单位目标无效。";
                        return targets;
                    }

                    targets.Add(EffectTargetRef.ForPart(partTarget));
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

                    targets.Add(EffectTargetRef.ForPart(partTarget));
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
                            targets.Add(EffectTargetRef.ForPart(part));
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

        private static int CalculateScaledAmount(EffectExecutionContext context, CardEffectDefinition effect, EffectTargetRef target)
        {
            var scaleBy = effect.scaleBy;
            if (string.IsNullOrWhiteSpace(scaleBy))
            {
                return 0;
            }

            if (string.Equals(scaleBy, "SelfArmor", StringComparison.OrdinalIgnoreCase))
            {
                return (int)Math.Round(context.actingPlayer.armor * effect.ratio);
            }

            if (string.Equals(scaleBy, "CardsPlayedThisTurn", StringComparison.OrdinalIgnoreCase))
            {
                return (int)Math.Round(context.actingPlayer.cardsPlayedThisTurn * effect.ratio);
            }

            if (string.Equals(scaleBy, "TargetPoison", StringComparison.OrdinalIgnoreCase))
            {
                var poison = target.IsPlayer
                    ? BattleStatusSystem.GetStacks(target.Player.statuses, BattleStatusSystem.PoisonStatusId)
                    : BattleStatusSystem.GetStacks(context.state.monster == null ? null : context.state.monster.statuses, BattleStatusSystem.PoisonStatusId);
                return (int)Math.Round(poison * effect.ratio);
            }

            return 0;
        }

        private static int GetStrengthBonus(PlayerBattleState player)
        {
            return BattleStatusSystem.GetStacks(player.statuses, BattleStatusSystem.StrengthStatusId);
        }

        private sealed class EffectExecutionContext
        {
            public BattleState state;
            public PlayerBattleState actingPlayer;
            public BattleCommand command;
            public BattleCardState playedCard;
            public CardDefinition definition;
            public BattleTargetType cardTargetType;
            public BattleCommandResult result;
        }

        private sealed class PlannedEffect
        {
            public CardEffectDefinition effect;
            public ICardEffectHandler handler;
            public List<EffectTargetRef> targets;
        }

        private sealed class EffectTargetRef
        {
            public PlayerBattleState Player;
            public MonsterPartState Part;
            public bool IsPlayer => Player != null;

            public static EffectTargetRef ForPlayer(PlayerBattleState player)
            {
                return new EffectTargetRef { Player = player };
            }

            public static EffectTargetRef ForPart(MonsterPartState part)
            {
                return new EffectTargetRef { Part = part };
            }
        }

        private interface ICardEffectHandler
        {
            bool RequiresTarget { get; }

            bool Execute(
                EffectExecutionContext context,
                CardEffectDefinition effect,
                List<EffectTargetRef> targets,
                ref int opCount,
                out string error);
        }

        private sealed class DamageEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                var strength = GetStrengthBonus(context.actingPlayer);
                for (var i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    var scaled = CalculateScaledAmount(context, effect, target);
                    var amount = Math.Max(0, effect.amount + scaled + strength);
                    if (target.IsPlayer)
                    {
                        var applied = BattleMechanics.ApplyDamage(target.Player, amount);
                        context.result.events.Add(new BattleEvent
                        {
                            message = BattleTextHelper.Actor(context.actingPlayer.displayName) + " 使用 " + BattleTextHelper.Card(context.definition.name) + " 攻击 " + BattleTextHelper.Unit(target.Player.displayName) + "，造成 " + BattleTextHelper.DamageText(applied) + "。"
                        });
                    }
                    else
                    {
                        var applied = BattleMechanics.ApplyDamageToPart(context.state, target.Part, amount, context.result);
                        context.result.events.Add(new BattleEvent
                        {
                            message = BattleTextHelper.Actor(context.actingPlayer.displayName) + " 使用 " + BattleTextHelper.Card(context.definition.name) + " 攻击 " + BattleTextHelper.Unit(target.Part.displayName) + "，造成 " + BattleTextHelper.DamageText(applied) + "。"
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

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
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
                        message = BattleTextHelper.Unit(targets[i].Player.displayName) + " 抽了 " + BattleTextHelper.DrawText(drawn) + "。"
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class HealEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
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
                        message = BattleTextHelper.Unit(targets[i].Player.displayName) + " 恢复了 " + BattleTextHelper.HealText(healed) + "。"
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class GainArmorEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
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
                        message = BattleTextHelper.Unit(targets[i].Player.displayName) + " 获得了 " + BattleTextHelper.ArmorText(amount) + "。"
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class ApplyStatusEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                if (string.IsNullOrWhiteSpace(effect.statusId))
                {
                    error = "施加状态时缺少 statusId。";
                    return false;
                }

                var amount = Math.Max(1, effect.amount);
                for (var i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    if (target.IsPlayer)
                    {
                        var total = BattleStatusSystem.AddStacks(target.Player.statuses, effect.statusId, amount);
                        context.result.events.Add(new BattleEvent
                        {
                            message = BattleTextHelper.Unit(target.Player.displayName) + " 获得 " + effect.statusId + " x" + amount + "（总计 " + total + "）。"
                        });
                    }
                    else
                    {
                        var total = BattleStatusSystem.AddStacks(context.state.monster == null ? null : context.state.monster.statuses, effect.statusId, amount);
                        context.result.events.Add(new BattleEvent
                        {
                            message = BattleTextHelper.Unit(context.state.monster == null ? "怪物" : context.state.monster.displayName) + " 获得 " + effect.statusId + " x" + amount + "（总计 " + total + "）。"
                        });
                    }

                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class DamageByArmorEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                var scaled = (int)Math.Round(context.actingPlayer.armor * effect.ratio);
                var amount = Math.Max(0, effect.amount + scaled);
                if (amount <= 0)
                {
                    return true;
                }

                if (effect.amount2 > 0)
                {
                    var consumed = Math.Min(context.actingPlayer.armor, scaled);
                    context.actingPlayer.armor -= consumed;
                }

                for (var i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    if (target.IsPlayer)
                    {
                        var applied = BattleMechanics.ApplyDamage(target.Player, amount);
                        context.result.events.Add(new BattleEvent
                        {
                            message = BattleTextHelper.Unit(target.Player.displayName) + " 受到" + BattleTextHelper.DamageText(applied) + "，来自护甲冲击。"
                        });
                    }
                    else
                    {
                        var applied = BattleMechanics.ApplyDamageToPart(context.state, target.Part, amount, context.result);
                        context.result.events.Add(new BattleEvent
                        {
                            message = BattleTextHelper.Unit(target.Part.displayName) + " 受到" + BattleTextHelper.DamageText(applied) + "，来自护甲冲击。"
                        });
                    }

                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class ApplyVulnerableEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                var stacks = Math.Max(1, effect.amount);
                for (var i = 0; i < targets.Count; i++)
                {
                    if (!targets[i].IsPlayer)
                    {
                        continue;
                    }

                    targets[i].Player.vulnerableStacks += stacks;
                    context.result.events.Add(new BattleEvent
                    {
                        message = BattleTextHelper.Unit(targets[i].Player.displayName) + " 获得易伤 x" + stacks + "。"
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class ModifyEnergyEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => false;

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                context.actingPlayer.energy = Math.Max(0, context.actingPlayer.energy + effect.amount);
                context.result.events.Add(new BattleEvent
                {
                    message = BattleTextHelper.Unit(context.actingPlayer.displayName) + " 能量变化 " + effect.amount + "。"
                });
                opCount += 1;
                return true;
            }
        }

        private sealed class LoseHpEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
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
                        message = BattleTextHelper.Unit(player.displayName) + " 失去 " + BattleTextHelper.DamageText(applied) + "。"
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class RecycleDiscardToHandEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => false;

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
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
                        message = BattleTextHelper.Unit(context.actingPlayer.displayName) + " 从弃牌堆回收了 " + BattleTextHelper.Card(card.cardId) + "。"
                    });
                    opCount += 1;
                }

                return true;
            }
        }

        private sealed class CopyAndPlunderEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => true;

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
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
                    message = BattleTextHelper.Actor(context.actingPlayer.displayName) + " 使用 " + BattleTextHelper.Card(context.definition.name) + " 对 " + BattleTextHelper.Unit(target.Player.displayName) + " 进行掠夺并复制了一张。"
                });
                opCount += 1;
                return true;
            }
        }

        private sealed class ExhaustFromHandEffectHandler : ICardEffectHandler
        {
            public bool RequiresTarget => false;

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
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
                        message = BattleTextHelper.Unit(context.actingPlayer.displayName) + " 消耗了 " + BattleTextHelper.Card(exhausted.cardId) + "。"
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

            public bool Execute(EffectExecutionContext context, CardEffectDefinition effect, List<EffectTargetRef> targets, ref int opCount, out string error)
            {
                error = null;
                if (!BattleTargetingRules.CanTargetArea(context.state, context.actingPlayer, context.definition, context.command.targetArea))
                {
                    error = "目标区域无效。";
                    return false;
                }

                context.actingPlayer.area = context.command.targetArea;
                context.result.events.Add(new BattleEvent
                {
                    message = BattleTextHelper.Actor(context.actingPlayer.displayName) + " 移动到了 " + BattleTextHelper.AreaText(context.command.targetArea) + "。"
                });
                opCount += 1;
                return true;
            }
        }
    }
}
