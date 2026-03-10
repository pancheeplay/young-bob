using System;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Battle
{
    internal static class CardEffectResolver
    {
        public static void ResolveCardEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            switch (definition.effectType)
            {
                case "Damage":
                    ResolveDamageEffect(state, actingPlayer, command, definition, targetType, result);
                    break;

                case "Heal":
                    ResolveHealEffect(state, actingPlayer, command, definition, targetType, result);
                    break;

                case "GainArmor":
                    ResolveGainArmorEffect(state, actingPlayer, command, definition, targetType, result);
                    break;

                case "DrawCards":
                    ResolveDrawCardsEffect(state, actingPlayer, command, definition, targetType, result);
                    break;

                case "DamageAndDrawSelf":
                    ResolveDamageAndDrawSelfEffect(state, actingPlayer, command, definition, targetType, result);
                    break;

                case "DamageAndTargetHeroDraw":
                    ResolveDamageAndTargetHeroDrawEffect(state, actingPlayer, command, definition, targetType, result);
                    break;

                case "CurseLoseHp":
                    ResolveCurseLoseHpEffect(state, actingPlayer, command, definition, targetType, result);
                    break;

                case "CopyAndPlunder":
                    ResolveCopyAndPlunderEffect(state, actingPlayer, command, targetType, result);
                    break;

                case "MoveArea":
                    ResolveMoveAreaEffect(state, actingPlayer, command, targetType, result);
                    break;

                case "ChargeUp":
                    ResolveChargeUpEffect(actingPlayer, definition, targetType, result);
                    break;

                case "ComboFinisherDamage":
                    ResolveDamageEffect(state, actingPlayer, command, definition, targetType, result, includeComboBonus: true);
                    break;

                default:
                    result.error = "Unknown card effect: " + definition.effectType;
                    break;
            }
        }

        private static void ResolveDamageEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            ResolveDamageEffect(state, actingPlayer, command, definition, targetType, result, includeComboBonus: false);
        }

        private static void ResolveDamageEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result, bool includeComboBonus)
        {
            var comboBonus = includeComboBonus ? Math.Max(0, actingPlayer.cardsPlayedThisTurn) : 0;
            var chargeBonus = Math.Max(0, actingPlayer.nextAttackBonus);
            var finalDamage = Math.Max(0, definition.value + comboBonus + chargeBonus);

            switch (targetType)
            {
                case BattleTargetType.MonsterPart:
                case BattleTargetType.SingleUnit:
                    var part = BattleTargetResolver.ResolvePartTarget(state, command);
                    if (part == null)
                    {
                        var playerTarget = BattleTargetResolver.ResolvePlayerTarget(state, command, false);
                        if (playerTarget != null && targetType == BattleTargetType.SingleUnit)
                        {
                            var damageToHero = BattleMechanics.ApplyDamage(playerTarget, finalDamage);
                            ConsumeAttackCharge(actingPlayer);
                            result.events.Add(new BattleEvent
                            {
                                message = BattleTextHelper.Actor(actingPlayer.displayName) + " used " + BattleTextHelper.Card(definition.name) + " on " + BattleTextHelper.Unit(playerTarget.displayName) + " for " + BattleTextHelper.DamageText(damageToHero) + "."
                            });
                            AppendDamageBonusLog(actingPlayer, comboBonus, chargeBonus, result);
                            return;
                        }

                        result.error = "Invalid target.";
                        return;
                    }

                    if (!BattleTargetResolver.IsPartInRange(state.monster, part, definition, actingPlayer.area))
                    {
                        result.error = "Target out of range.";
                        return;
                    }

                    var damageApplied = BattleMechanics.ApplyDamageToPart(state, part, finalDamage, result);
                    ConsumeAttackCharge(actingPlayer);
                    result.events.Add(new BattleEvent
                    {
                        message = BattleTextHelper.Actor(actingPlayer.displayName) + " used " + BattleTextHelper.Card(definition.name) + " on " + BattleTextHelper.Unit(part.displayName) + " for " + BattleTextHelper.DamageText(damageApplied) + "."
                    });
                    AppendDamageBonusLog(actingPlayer, comboBonus, chargeBonus, result);
                    break;

                case BattleTargetType.AllMonsterParts:
                    var hitCount = 0;
                    for (var i = 0; i < state.monster.parts.Count; i++)
                    {
                        var target = state.monster.parts[i];
                        if (target.hp <= 0)
                        {
                            continue;
                        }

                        if (!BattleTargetResolver.IsPartInRange(state.monster, target, definition, actingPlayer.area))
                        {
                            continue;
                        }

                        BattleMechanics.ApplyDamageToPart(state, target, finalDamage, result);
                        hitCount += 1;
                    }

                    if (hitCount == 0)
                    {
                        result.error = "No valid parts to target.";
                        return;
                    }

                    ConsumeAttackCharge(actingPlayer);
                    result.events.Add(new BattleEvent
                    {
                        message = BattleTextHelper.Actor(actingPlayer.displayName) + " used " + BattleTextHelper.Card(definition.name) + " and hit all parts for " + BattleTextHelper.DamageText(finalDamage) + "."
                    });
                    AppendDamageBonusLog(actingPlayer, comboBonus, chargeBonus, result);
                    break;

                default:
                    result.error = "Damage target type mismatch.";
                    break;
            }
        }

        private static void ResolveHealEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            switch (targetType)
            {
                case BattleTargetType.SingleAlly:
                    var ally = BattleTargetResolver.ResolvePlayerTarget(state, command, false);
                    if (ally == null)
                    {
                        result.error = "Invalid ally target.";
                        return;
                    }

                    var recovered = BattleMechanics.Heal(ally, definition.value);
                    result.events.Add(new BattleEvent
                    {
                        message = BattleTextHelper.Actor(actingPlayer.displayName) + " used " + BattleTextHelper.Card(definition.name) + " on " + BattleTextHelper.Unit(ally.displayName) + " for " + BattleTextHelper.HealText(recovered) + "."
                    });
                    break;

                case BattleTargetType.AllAllies:
                    var healedTargets = 0;
                    for (var i = 0; i < state.players.Count; i++)
                    {
                        var teamMate = state.players[i];
                        if (teamMate.hp <= 0)
                        {
                            continue;
                        }

                        BattleMechanics.Heal(teamMate, definition.value);
                        healedTargets += 1;
                    }

                    if (healedTargets == 0)
                    {
                        result.error = "No living allies to heal.";
                        return;
                    }

                    result.events.Add(new BattleEvent
                    {
                        message = BattleTextHelper.Actor(actingPlayer.displayName) + " used " + BattleTextHelper.Card(definition.name) + " and healed the whole team for " + BattleTextHelper.HealText(definition.value) + "."
                    });
                    break;

                default:
                    result.error = "Heal target type mismatch.";
                    break;
            }
        }

        private static void ResolveGainArmorEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            var target = BattleTargetResolver.ResolveHeroTargetForUtility(state, actingPlayer, command, targetType, allowSelf: true, allowOtherAlly: true);
            if (target == null)
            {
                result.error = "Invalid armor target.";
                return;
            }

            target.armor += definition.value;
            result.events.Add(new BattleEvent
            {
                message = BattleTextHelper.Actor(actingPlayer.displayName) + " used " + BattleTextHelper.Card(definition.name) + " on " + BattleTextHelper.Unit(target.displayName) + ", granting " + BattleTextHelper.ArmorText(definition.value) + "."
            });
        }

        private static void ResolveDrawCardsEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            var target = BattleTargetResolver.ResolveHeroTargetForUtility(state, actingPlayer, command, targetType, allowSelf: true, allowOtherAlly: false);
            if (target == null)
            {
                result.error = "Invalid draw target.";
                return;
            }

            var drawn = BattleMechanics.DrawCards(state, target, definition.value);
            result.events.Add(new BattleEvent
            {
                message = BattleTextHelper.Unit(target.displayName) + " drew " + BattleTextHelper.DrawText(drawn) + " from " + BattleTextHelper.Card(definition.name) + "."
            });
        }

        private static void ResolveDamageAndDrawSelfEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            ResolveDamageEffect(state, actingPlayer, command, definition, targetType, result);
            if (!string.IsNullOrEmpty(result.error))
            {
                return;
            }

            var drawn = BattleMechanics.DrawCards(state, actingPlayer, 1);
            result.events.Add(new BattleEvent
            {
                message = BattleTextHelper.Actor(actingPlayer.displayName) + " drew " + BattleTextHelper.DrawText(drawn) + "."
            });
        }

        private static void ResolveDamageAndTargetHeroDrawEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            if (targetType != BattleTargetType.SingleUnit)
            {
                result.error = "DamageAndTargetHeroDraw requires SingleUnit target.";
                return;
            }

            var playerTarget = BattleTargetResolver.ResolvePlayerTarget(state, command, false);
            if (playerTarget != null)
            {
                var chargeBonus = Math.Max(0, actingPlayer.nextAttackBonus);
                var damage = BattleMechanics.ApplyDamage(playerTarget, Math.Max(0, definition.value + chargeBonus));
                ConsumeAttackCharge(actingPlayer);
                result.events.Add(new BattleEvent
                {
                    message = BattleTextHelper.Actor(actingPlayer.displayName) + " used " + BattleTextHelper.Card(definition.name) + " on " + BattleTextHelper.Unit(playerTarget.displayName) + " for " + BattleTextHelper.DamageText(damage) + "."
                });
                AppendDamageBonusLog(actingPlayer, 0, chargeBonus, result);

                var drawn = BattleMechanics.DrawCards(state, playerTarget, 1);
                result.events.Add(new BattleEvent
                {
                    message = BattleTextHelper.Unit(playerTarget.displayName) + " drew " + BattleTextHelper.DrawText(drawn) + " because they are a hero."
                });
                return;
            }

            var part = BattleTargetResolver.ResolvePartTarget(state, command);
            if (part == null)
            {
                result.error = "Invalid unit target.";
                return;
            }

            if (!BattleTargetResolver.IsPartInRange(state.monster, part, definition, actingPlayer.area))
            {
                result.error = "Target out of range.";
                return;
            }

            var partChargeBonus = Math.Max(0, actingPlayer.nextAttackBonus);
            var enemyDamage = BattleMechanics.ApplyDamageToPart(state, part, Math.Max(0, definition.value + partChargeBonus), result);
            ConsumeAttackCharge(actingPlayer);
            result.events.Add(new BattleEvent
            {
                message = BattleTextHelper.Actor(actingPlayer.displayName) + " used " + BattleTextHelper.Card(definition.name) + " on " + BattleTextHelper.Unit(part.displayName) + " for " + BattleTextHelper.DamageText(enemyDamage) + "."
            });
            AppendDamageBonusLog(actingPlayer, 0, partChargeBonus, result);
        }

        private static void ResolveCurseLoseHpEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            var target = BattleTargetResolver.ResolveHeroTargetForUtility(state, actingPlayer, command, targetType, allowSelf: true, allowOtherAlly: true);
            if (target == null)
            {
                result.error = "Invalid curse target.";
                return;
            }

            if (target.hp <= 0)
            {
                result.error = "Target already has no life.";
                return;
            }

            target.hp = Math.Max(0, target.hp - definition.value);
            result.events.Add(new BattleEvent
            {
                message = BattleTextHelper.Actor(actingPlayer.displayName) + " used " + BattleTextHelper.Card(definition.name) + " on " + BattleTextHelper.Unit(target.displayName) + ", reducing life by " + BattleTextHelper.DamageText(definition.value) + "."
            });
        }

        private static void ResolveCopyAndPlunderEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, BattleTargetType targetType, BattleCommandResult result)
        {
            if (targetType != BattleTargetType.OtherAlly)
            {
                result.error = "CopyAndPlunder requires OtherAlly target.";
                return;
            }

            var target = BattleTargetResolver.ResolvePlayerTarget(state, command, true);
            if (target == null)
            {
                result.error = "Invalid ally target.";
                return;
            }

            if (target.hand.Count == 0)
            {
                result.error = target.displayName + " has no cards to plunder.";
                return;
            }

            var random = new Random(state.randomSeed ^ state.turnIndex ^ actingPlayer.playerId.GetHashCode() ^ target.playerId.GetHashCode() ^ target.hand.Count);
            var stolenIndex = random.Next(target.hand.Count);
            var stolenCard = target.hand[stolenIndex];

            // Effect: Target's card goes to their discard pile
            target.hand.RemoveAt(stolenIndex);
            target.discardPile.Add(stolenCard);

            // Effect: Actor gets a NEW card with the same ID
            var copiedCard = new BattleCardState
            {
                instanceId = actingPlayer.playerId + "_plundered_" + stolenCard.cardId + "_" + Guid.NewGuid().ToString("N"),
                cardId = stolenCard.cardId
            };

            if (actingPlayer.hand.Count >= BattleEngine.MaxHandSize)
            {
                actingPlayer.discardPile.Add(copiedCard);
            }
            else
            {
                actingPlayer.hand.Add(copiedCard);
            }

            result.events.Add(new BattleEvent
            {
                message = BattleTextHelper.Actor(actingPlayer.displayName) + " used " + BattleTextHelper.Card("复制掠夺") + ", forcing " + BattleTextHelper.Unit(target.displayName) + " to discard a card and gaining a copy of it."
            });
        }

        private static void ResolveMoveAreaEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, BattleTargetType targetType, BattleCommandResult result)
        {
            if (targetType != BattleTargetType.Area)
            {
                result.error = "MoveArea requires Area target.";
                return;
            }

            if (command.targetArea != BattleArea.West && command.targetArea != BattleArea.East)
            {
                result.error = "Invalid target area.";
                return;
            }

            if (actingPlayer.area == command.targetArea)
            {
                result.error = "Already in that area.";
                return;
            }

            actingPlayer.area = command.targetArea;
            result.events.Add(new BattleEvent
            {
                message = BattleTextHelper.Actor(actingPlayer.displayName) + " moved to " + BattleTextHelper.AreaText(command.targetArea) + "."
            });
        }

        private static void ResolveChargeUpEffect(PlayerBattleState actingPlayer, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            if (targetType != BattleTargetType.Self)
            {
                result.error = "ChargeUp requires Self target.";
                return;
            }

            if (actingPlayer.attackChargeStage < 3)
            {
                actingPlayer.attackChargeStage += 1;
            }

            switch (actingPlayer.attackChargeStage)
            {
                case 1:
                    actingPlayer.nextAttackBonus = 1;
                    break;
                case 2:
                    actingPlayer.nextAttackBonus = 3;
                    break;
                default:
                    actingPlayer.nextAttackBonus = 5;
                    break;
            }

            result.events.Add(new BattleEvent
            {
                message = BattleTextHelper.Actor(actingPlayer.displayName) + " used " + BattleTextHelper.Card(definition.name) + ", next attack bonus is " + BattleTextHelper.DamageText(actingPlayer.nextAttackBonus) + "."
            });
        }

        private static void ConsumeAttackCharge(PlayerBattleState actingPlayer)
        {
            if (actingPlayer == null || actingPlayer.nextAttackBonus <= 0)
            {
                return;
            }

            actingPlayer.nextAttackBonus = 0;
            actingPlayer.attackChargeStage = 0;
        }

        private static void AppendDamageBonusLog(PlayerBattleState actingPlayer, int comboBonus, int chargeBonus, BattleCommandResult result)
        {
            if (comboBonus <= 0 && chargeBonus <= 0)
            {
                return;
            }

            result.events.Add(new BattleEvent
            {
                message = "<color=#8A8A8A>Damage bonus:</color> combo +" + comboBonus + ", charge +" + chargeBonus + "."
            });
        }
    }
}
