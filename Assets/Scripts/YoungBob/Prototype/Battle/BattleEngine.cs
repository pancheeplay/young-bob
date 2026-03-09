using System;
using System.Collections.Generic;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Battle
{
    public sealed class BattleEngine
    {
        private readonly GameDataRepository _dataRepository;

        public BattleEngine(GameDataRepository dataRepository)
        {
            _dataRepository = dataRepository;
        }

        public BattleState CreateInitialState(BattleSetupDefinition setup)
        {
            var encounter = _dataRepository.GetEncounter(setup.encounterId);
            var deck = _dataRepository.GetDeck(setup.starterDeckId);
            var state = new BattleState
            {
                roomId = setup.roomId,
                randomSeed = setup.randomSeed,
                turnIndex = 1,
                phase = BattlePhase.PlayerTurn,
                currentPrompt = "Team turn"
            };

            foreach (var participant in setup.players)
            {
                var player = new PlayerBattleState
                {
                    playerId = participant.playerId,
                    displayName = participant.displayName,
                    maxHp = 24,
                    hp = 24,
                    armor = 0,
                    hasEndedTurn = false
                };

                for (var i = 0; i < deck.cards.Length; i++)
                {
                    player.drawPile.Add(new BattleCardState
                    {
                        instanceId = participant.playerId + "_" + deck.cards[i] + "_" + i,
                        cardId = deck.cards[i]
                    });
                }

                Shuffle(player.drawPile, setup.randomSeed ^ participant.playerId.GetHashCode());
                DrawCards(player, 4);
                state.players.Add(player);
            }

            for (var i = 0; i < encounter.enemies.Length; i++)
            {
                var enemy = encounter.enemies[i];
                state.enemies.Add(new EnemyBattleState
                {
                    enemyId = enemy.enemyId,
                    instanceId = encounter.id + "_" + enemy.enemyId + "_" + i,
                    displayName = enemy.enemyName,
                    maxHp = enemy.maxHp,
                    hp = enemy.maxHp,
                    armor = 0,
                    attackDamage = enemy.attackDamage
                });
            }

            return state;
        }

        public BattleCommandResult Apply(BattleState state, BattleCommand command)
        {
            var result = new BattleCommandResult();
            if (state.phase == BattlePhase.Victory || state.phase == BattlePhase.Defeat)
            {
                result.error = "Battle already finished.";
                return result;
            }

            if (state.phase != BattlePhase.PlayerTurn)
            {
                result.error = "Players cannot act right now.";
                return result;
            }

            var actingPlayer = state.GetPlayer(command.actorPlayerId);
            if (actingPlayer == null)
            {
                result.error = "Unknown player.";
                return result;
            }

            if (actingPlayer.hp <= 0)
            {
                result.error = "Player is down.";
                return result;
            }

            if (actingPlayer.hasEndedTurn)
            {
                result.error = "Player already ended turn.";
                return result;
            }

            switch (command.action)
            {
                case "play_card":
                    return PlayCard(state, actingPlayer, command);
                case "end_turn":
                    return EndTurn(state, actingPlayer);
                default:
                    result.error = "Unknown action: " + command.action;
                    return result;
            }
        }

        private BattleCommandResult PlayCard(BattleState state, PlayerBattleState actingPlayer, BattleCommand command)
        {
            var result = new BattleCommandResult();
            var card = actingPlayer.hand.Find(item => item.instanceId == command.cardInstanceId);
            if (card == null)
            {
                result.error = "Card not found in hand.";
                return result;
            }

            var definition = _dataRepository.GetCard(card.cardId);
            var targetType = ParseTargetType(definition.targetType);
            ResolveCardEffect(state, actingPlayer, command, definition, targetType, result);
            if (!string.IsNullOrEmpty(result.error))
            {
                return result;
            }

            actingPlayer.hand.Remove(card);
            actingPlayer.discardPile.Add(card);
            TryResolveBattleEnd(state, result);
            result.success = true;
            return result;
        }

        private void ResolveCardEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
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

                case "StealRandomCard":
                    ResolveStealRandomCardEffect(state, actingPlayer, command, targetType, result);
                    break;

                default:
                    result.error = "Unknown card effect: " + definition.effectType;
                    break;
            }
        }

        private static void ResolveDamageEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            switch (targetType)
            {
                case BattleTargetType.SingleEnemy:
                    var enemy = ResolveEnemyTarget(state, command);
                    if (enemy == null)
                    {
                        result.error = "Invalid enemy target.";
                        return;
                    }

                    var damageToEnemy = ApplyDamage(enemy, definition.value);
                    result.events.Add(new BattleEvent
                    {
                        message = Actor(actingPlayer.displayName) + " used " + Card(definition.name) + " on " + Unit(enemy.displayName) + " for " + DamageText(damageToEnemy) + "."
                    });
                    break;

                case BattleTargetType.AllEnemies:
                    var hitCount = 0;
                    for (var i = 0; i < state.enemies.Count; i++)
                    {
                        var target = state.enemies[i];
                        if (target.hp <= 0)
                        {
                            continue;
                        }

                        ApplyDamage(target, definition.value);
                        hitCount += 1;
                    }

                    if (hitCount == 0)
                    {
                        result.error = "No living enemies to target.";
                        return;
                    }

                    result.events.Add(new BattleEvent
                    {
                        message = Actor(actingPlayer.displayName) + " used " + Card(definition.name) + " and hit all enemies for " + DamageText(definition.value) + "."
                    });
                    break;

                case BattleTargetType.SingleUnit:
                    var playerTarget = ResolvePlayerTarget(state, command, false);
                    if (playerTarget != null)
                    {
                        var damageToHero = ApplyDamage(playerTarget, definition.value);
                        result.events.Add(new BattleEvent
                        {
                            message = Actor(actingPlayer.displayName) + " used " + Card(definition.name) + " on " + Unit(playerTarget.displayName) + " for " + DamageText(damageToHero) + "."
                        });
                        return;
                    }

                    var anyEnemy = ResolveEnemyTarget(state, command);
                    if (anyEnemy == null)
                    {
                        result.error = "Invalid unit target.";
                        return;
                    }

                    var anyDamage = ApplyDamage(anyEnemy, definition.value);
                    result.events.Add(new BattleEvent
                    {
                        message = Actor(actingPlayer.displayName) + " used " + Card(definition.name) + " on " + Unit(anyEnemy.displayName) + " for " + DamageText(anyDamage) + "."
                    });
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
                    var ally = ResolvePlayerTarget(state, command, false);
                    if (ally == null)
                    {
                        result.error = "Invalid ally target.";
                        return;
                    }

                    var recovered = Heal(ally, definition.value);
                    result.events.Add(new BattleEvent
                    {
                        message = Actor(actingPlayer.displayName) + " used " + Card(definition.name) + " on " + Unit(ally.displayName) + " for " + HealText(recovered) + "."
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

                        Heal(teamMate, definition.value);
                        healedTargets += 1;
                    }

                    if (healedTargets == 0)
                    {
                        result.error = "No living allies to heal.";
                        return;
                    }

                    result.events.Add(new BattleEvent
                    {
                        message = Actor(actingPlayer.displayName) + " used " + Card(definition.name) + " and healed the whole team for " + HealText(definition.value) + "."
                    });
                    break;

                default:
                    result.error = "Heal target type mismatch.";
                    break;
            }
        }

        private static void ResolveGainArmorEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            var target = ResolveHeroTargetForUtility(state, actingPlayer, command, targetType, allowSelf: true, allowOtherAlly: true);
            if (target == null)
            {
                result.error = "Invalid armor target.";
                return;
            }

            target.armor += definition.value;
            result.events.Add(new BattleEvent
            {
                message = Actor(actingPlayer.displayName) + " used " + Card(definition.name) + " on " + Unit(target.displayName) + ", granting " + ArmorText(definition.value) + "."
            });
        }

        private static void ResolveDrawCardsEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            var target = ResolveHeroTargetForUtility(state, actingPlayer, command, targetType, allowSelf: true, allowOtherAlly: false);
            if (target == null)
            {
                result.error = "Invalid draw target.";
                return;
            }

            var drawn = DrawCards(target, definition.value);
            result.events.Add(new BattleEvent
            {
                message = Unit(target.displayName) + " drew " + DrawText(drawn) + " from " + Card(definition.name) + "."
            });
        }

        private static void ResolveDamageAndDrawSelfEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            ResolveDamageEffect(state, actingPlayer, command, definition, targetType, result);
            if (!string.IsNullOrEmpty(result.error))
            {
                return;
            }

            var drawn = DrawCards(actingPlayer, 1);
            result.events.Add(new BattleEvent
            {
                message = Actor(actingPlayer.displayName) + " drew " + DrawText(drawn) + "."
            });
        }

        private static void ResolveDamageAndTargetHeroDrawEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            if (targetType != BattleTargetType.SingleUnit)
            {
                result.error = "DamageAndTargetHeroDraw requires SingleUnit target.";
                return;
            }

            var playerTarget = ResolvePlayerTarget(state, command, false);
            if (playerTarget != null)
            {
                var damage = ApplyDamage(playerTarget, definition.value);
                result.events.Add(new BattleEvent
                {
                    message = Actor(actingPlayer.displayName) + " used " + Card(definition.name) + " on " + Unit(playerTarget.displayName) + " for " + DamageText(damage) + "."
                });

                var drawn = DrawCards(playerTarget, 1);
                result.events.Add(new BattleEvent
                {
                    message = Unit(playerTarget.displayName) + " drew " + DrawText(drawn) + " because they are a hero."
                });
                return;
            }

            var enemyTarget = ResolveEnemyTarget(state, command);
            if (enemyTarget == null)
            {
                result.error = "Invalid unit target.";
                return;
            }

            var enemyDamage = ApplyDamage(enemyTarget, definition.value);
            result.events.Add(new BattleEvent
            {
                message = Actor(actingPlayer.displayName) + " used " + Card(definition.name) + " on " + Unit(enemyTarget.displayName) + " for " + DamageText(enemyDamage) + "."
            });
        }

        private static void ResolveCurseLoseHpEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, CardDefinition definition, BattleTargetType targetType, BattleCommandResult result)
        {
            var target = ResolveHeroTargetForUtility(state, actingPlayer, command, targetType, allowSelf: true, allowOtherAlly: true);
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
                message = Actor(actingPlayer.displayName) + " used " + Card(definition.name) + " on " + Unit(target.displayName) + ", reducing life by " + DamageText(definition.value) + "."
            });
        }

        private static void ResolveStealRandomCardEffect(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, BattleTargetType targetType, BattleCommandResult result)
        {
            if (targetType != BattleTargetType.OtherAlly)
            {
                result.error = "StealRandomCard requires OtherAlly target.";
                return;
            }

            var target = ResolvePlayerTarget(state, command, true);
            if (target == null)
            {
                result.error = "Invalid ally target.";
                return;
            }

            if (target.hand.Count == 0)
            {
                result.error = target.displayName + " has no cards to steal.";
                return;
            }

            var random = new Random(state.randomSeed ^ state.turnIndex ^ actingPlayer.playerId.GetHashCode() ^ target.playerId.GetHashCode() ^ target.hand.Count);
            var stolenIndex = random.Next(target.hand.Count);
            var stolenCard = target.hand[stolenIndex];
            target.hand.RemoveAt(stolenIndex);
            actingPlayer.hand.Add(stolenCard);
            result.events.Add(new BattleEvent
            {
                message = Actor(actingPlayer.displayName) + " used " + Card("借牌") + " and stole a random card from " + Unit(target.displayName) + "."
            });
        }

        private BattleCommandResult EndTurn(BattleState state, PlayerBattleState player)
        {
            var result = new BattleCommandResult();
            player.hasEndedTurn = true;
            result.events.Add(new BattleEvent
            {
                message = Actor(player.displayName) + " ended their turn."
            });

            if (!HaveAllAlivePlayersEnded(state.players))
            {
                state.currentPrompt = "Waiting for teammates to end turn";
                result.success = true;
                return result;
            }

            state.phase = BattlePhase.MonsterTurn;
            RunEnemyTurn(state, result);
            result.success = true;
            return result;
        }

        private void RunEnemyTurn(BattleState state, BattleCommandResult result)
        {
            for (var i = 0; i < state.enemies.Count; i++)
            {
                var enemy = state.enemies[i];
                if (enemy.hp <= 0)
                {
                    continue;
                }

                var target = FindLowestHpAlivePlayer(state.players);
                if (target == null)
                {
                    state.phase = BattlePhase.Defeat;
                    state.currentPrompt = "Defeat";
                    result.events.Add(new BattleEvent { message = "All players are down." });
                    return;
                }

                var damage = ApplyDamage(target, enemy.attackDamage);
                result.events.Add(new BattleEvent
                {
                    message = Unit(enemy.displayName) + " attacks " + Unit(target.displayName) + " for " + DamageText(damage) + "."
                });
            }

            if (AllPlayersDead(state.players))
            {
                state.phase = BattlePhase.Defeat;
                state.currentPrompt = "Defeat";
                result.events.Add(new BattleEvent { message = "The party was defeated." });
                return;
            }

            state.phase = BattlePhase.PlayerTurn;
            state.turnIndex += 1;
            ResetTeamTurn(state.players);
            for (var i = 0; i < state.players.Count; i++)
            {
                if (state.players[i].hp > 0)
                {
                    DrawUpTo(state.players[i], 4);
                }
            }

            state.currentPrompt = "Team turn";
            result.events.Add(new BattleEvent
            {
                message = "<color=#E6C36A>Turn " + state.turnIndex + " begins.</color>"
            });
        }

        private static int ApplyDamage(PlayerBattleState target, int amount)
        {
            var mitigatedByArmor = Math.Min(target.armor, amount);
            target.armor -= mitigatedByArmor;
            var remainingDamage = amount - mitigatedByArmor;
            target.hp = Math.Max(0, target.hp - remainingDamage);
            return remainingDamage;
        }

        private static int ApplyDamage(EnemyBattleState target, int amount)
        {
            var mitigatedByArmor = Math.Min(target.armor, amount);
            target.armor -= mitigatedByArmor;
            var remainingDamage = amount - mitigatedByArmor;
            target.hp = Math.Max(0, target.hp - remainingDamage);
            return remainingDamage;
        }

        private static int Heal(PlayerBattleState player, int amount)
        {
            var previousHp = player.hp;
            player.hp = Math.Min(player.maxHp, player.hp + amount);
            return player.hp - previousHp;
        }

        private void TryResolveBattleEnd(BattleState state, BattleCommandResult result)
        {
            if (HasAliveEnemies(state.enemies))
            {
                return;
            }

            state.phase = BattlePhase.Victory;
            state.currentPrompt = "Victory";
            result.events.Add(new BattleEvent
            {
                message = "<color=#7FD67F>All enemies were defeated. Victory.</color>"
            });
        }

        private static PlayerBattleState ResolveHeroTargetForUtility(BattleState state, PlayerBattleState actingPlayer, BattleCommand command, BattleTargetType targetType, bool allowSelf, bool allowOtherAlly)
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

        private static PlayerBattleState ResolvePlayerTarget(BattleState state, BattleCommand command, bool disallowSelf)
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

        private static EnemyBattleState ResolveEnemyTarget(BattleState state, BattleCommand command)
        {
            if (command.targetFaction != BattleTargetFaction.Enemies || string.IsNullOrEmpty(command.targetUnitId))
            {
                return null;
            }

            var target = state.GetEnemy(command.targetUnitId);
            return target != null && target.hp > 0 ? target : null;
        }

        private static bool HasAliveEnemies(List<EnemyBattleState> enemies)
        {
            for (var i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].hp > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DrawUpTo(PlayerBattleState player, int targetHandSize)
        {
            while (player.hand.Count < targetHandSize)
            {
                if (!TryDrawOne(player))
                {
                    break;
                }
            }
        }

        private static int DrawCards(PlayerBattleState player, int count)
        {
            var drawn = 0;
            for (var i = 0; i < count; i++)
            {
                if (!TryDrawOne(player))
                {
                    break;
                }

                drawn += 1;
            }

            return drawn;
        }

        private static bool TryDrawOne(PlayerBattleState player)
        {
            if (player.drawPile.Count == 0 && player.discardPile.Count > 0)
            {
                player.drawPile.AddRange(player.discardPile);
                player.discardPile.Clear();
            }

            if (player.drawPile.Count == 0)
            {
                return false;
            }

            var nextCard = player.drawPile[0];
            player.drawPile.RemoveAt(0);
            player.hand.Add(nextCard);
            return true;
        }

        private static void Shuffle(List<BattleCardState> list, int seed)
        {
            var random = new Random(seed);
            for (var i = list.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                var temp = list[i];
                list[i] = list[swapIndex];
                list[swapIndex] = temp;
            }
        }

        private static BattleTargetType ParseTargetType(string raw)
        {
            return (BattleTargetType)Enum.Parse(typeof(BattleTargetType), raw, true);
        }

        private static PlayerBattleState FindLowestHpAlivePlayer(List<PlayerBattleState> players)
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

        private static bool AllPlayersDead(List<PlayerBattleState> players)
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

        private static bool HaveAllAlivePlayersEnded(List<PlayerBattleState> players)
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

        private static void ResetTeamTurn(List<PlayerBattleState> players)
        {
            for (var i = 0; i < players.Count; i++)
            {
                players[i].hasEndedTurn = players[i].hp <= 0;
            }
        }

        private static string Actor(string value)
        {
            return "<color=#6FC3FF>" + value + "</color>";
        }

        private static string Unit(string value)
        {
            return "<color=#FFD27A>" + value + "</color>";
        }

        private static string Card(string value)
        {
            return "<color=#C9A0FF>" + value + "</color>";
        }

        private static string DamageText(int value)
        {
            return "<color=#FF6B6B>" + value + " damage</color>";
        }

        private static string HealText(int value)
        {
            return "<color=#6EDC8C>" + value + " healing</color>";
        }

        private static string ArmorText(int value)
        {
            return "<color=#73BFFF>" + value + " armor</color>";
        }

        private static string DrawText(int value)
        {
            return "<color=#F7E08A>" + value + " card(s)</color>";
        }
    }
}
