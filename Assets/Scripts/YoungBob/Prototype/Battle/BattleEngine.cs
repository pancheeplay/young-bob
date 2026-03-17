using System;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Battle
{
    public sealed class BattleEngine
    {
        internal const int BaseEnergyPerTurn = 3;
        internal const int CardsDrawnPerTurn = 2;
        internal const int MaxHandSize = 5;
        internal const string MoveCardId = "move";
        internal const string PoseIdleId = "idle";
        internal const string PoseChargeId = "charge";
        internal const string PoseAttackId = "attack";

        private readonly GameDataRepository _dataRepository;

        public BattleEngine(GameDataRepository dataRepository)
        {
            _dataRepository = dataRepository;
        }

        public BattleState CreateInitialState(BattleSetupDefinition setup)
        {
            var deck = _dataRepository.GetDeck(setup.starterDeckId);
            var state = new BattleState
            {
                roomId = setup.roomId,
                randomSeed = setup.randomSeed,
                turnIndex = 1,
                phase = BattlePhase.PlayerTurn
            };

            ResolveInitialStageAndEncounter(setup, state, out var openingEncounterId, out var openingMonsterDefinition);
            state.currentPrompt = BuildTeamTurnPrompt(state);

            foreach (var participant in setup.players)
            {
                var player = new PlayerBattleState
                {
                    playerId = participant.playerId,
                    displayName = participant.displayName,
                    maxHp = 24,
                    hp = 24,
                    armor = 0,
                    vulnerableStacks = 0,
                    energy = BaseEnergyPerTurn,
                    area = BattleArea.West,
                    height = BattleHeight.Ground,
                    hasEndedTurn = false,
                    cardsPlayedThisTurn = 0,
                    nextAttackBonus = 0,
                    attackChargeStage = 0
                };

                for (var i = 0; i < deck.cards.Length; i++)
                {
                    player.drawPile.Add(new BattleCardState
                    {
                        instanceId = participant.playerId + "_" + deck.cards[i] + "_" + i,
                        cardId = deck.cards[i],
                        costDelta = 0
                    });
                }

                BattleMechanics.Shuffle(player.drawPile, setup.randomSeed ^ participant.playerId.GetHashCode());
                BattleMechanics.DrawCards(state, player, CardsDrawnPerTurn);
                BattleMechanics.AddMoveCard(state, player);
                state.players.Add(player);
            }

            state.monster = MonsterAI.BuildMonster(openingMonsterDefinition, setup.randomSeed);
            if (state.monster == null && !string.IsNullOrWhiteSpace(openingEncounterId))
            {
                throw new InvalidOperationException("Encounter has no monster definition: " + openingEncounterId);
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
            var effectiveCost = BattleMechanics.GetEffectiveEnergyCost(card, definition);
            if (actingPlayer.energy < effectiveCost)
            {
                result.error = "Not enough energy.";
                return result;
            }

            var targetType = BattleTargetResolver.ParseTargetType(definition.targetType);
            if (targetType == BattleTargetType.None)
            {
                result.error = "Invalid card target type.";
                return result;
            }

            var handIndex = actingPlayer.hand.IndexOf(card);
            if (handIndex < 0)
            {
                result.error = "Card not found in hand.";
                return result;
            }

            // STS-like timing: played card leaves hand before effects resolve,
            // so draw effects can use the freed hand slot.
            actingPlayer.hand.RemoveAt(handIndex);
            CardEffectResolver.ResolveCardEffects(state, actingPlayer, command, card, definition, targetType, result);
            if (!string.IsNullOrEmpty(result.error))
            {
                if (handIndex <= actingPlayer.hand.Count)
                {
                    actingPlayer.hand.Insert(handIndex, card);
                }
                else
                {
                    actingPlayer.hand.Add(card);
                }
                return result;
            }

            actingPlayer.energy -= effectiveCost;
            card.costDelta = 0;
            actingPlayer.discardPile.Add(card);
            actingPlayer.cardsPlayedThisTurn += 1;
            TryResolveEncounterEnd(state, result);
            result.success = true;
            return result;
        }

        private BattleCommandResult EndTurn(BattleState state, PlayerBattleState player)
        {
            var result = new BattleCommandResult();
            player.hasEndedTurn = true;
            result.events.Add(new BattleEvent
            {
                message = BattleTextHelper.Actor(player.displayName) + " ended their turn."
            });

            if (!BattleTargetResolver.HaveAllAlivePlayersEnded(state.players))
            {
                state.currentPrompt = "Waiting for teammates to end turn";
                result.success = true;
                return result;
            }

            state.phase = BattlePhase.MonsterTurn;
            RunMonsterTurn(state, result);
            result.success = true;
            return result;
        }

        private void RunMonsterTurn(BattleState state, BattleCommandResult result)
        {
            if (state.monster == null)
            {
                state.phase = BattlePhase.Defeat;
                state.currentPrompt = "Defeat";
                result.events.Add(new BattleEvent { message = "No monster present." });
                return;
            }

            if (state.monster.coreHp <= 0)
            {
                TryResolveEncounterEnd(state, result);
                return;
            }

            EnsureMonsterDefinition(state);
            BattleStatusSystem.TickPoisonOnMonsterAtTurnStart(state, result);
            if (state.monster != null && state.monster.coreHp <= 0)
            {
                TryResolveEncounterEnd(state, result);
                return;
            }

            MonsterAI.ResolveMonsterSkill(state, result);

            if (state.monster != null && state.monster.coreHp <= 0)
            {
                TryResolveEncounterEnd(state, result);
                return;
            }

            if (BattleTargetResolver.AllPlayersDead(state.players))
            {
                state.phase = BattlePhase.Defeat;
                state.currentPrompt = "Defeat";
                result.events.Add(new BattleEvent { message = "The party was defeated." });
                return;
            }

            BeginPlayerRound(state, result);
        }

        private void BeginPlayerRound(BattleState state, BattleCommandResult result)
        {
            state.phase = BattlePhase.PlayerTurn;
            state.turnIndex += 1;
            BattleTargetResolver.ResetTeamTurn(state.players);
            for (var i = 0; i < state.players.Count; i++)
            {
                if (state.players[i].hp > 0)
                {
                    state.players[i].energy = BaseEnergyPerTurn;
                    state.players[i].cardsPlayedThisTurn = 0;
                    ResetTemporaryCardModifiers(state.players[i]);
                    BattleMechanics.DrawCards(state, state.players[i], CardsDrawnPerTurn);
                    BattleMechanics.AddMoveCard(state, state.players[i]);
                }
            }

            BattleStatusSystem.TickPoisonOnPlayersAtTurnStart(state, result);

            state.currentPrompt = BuildTeamTurnPrompt(state);
            result.events.Add(new BattleEvent
            {
                message = "<color=#E6C36A>Turn " + state.turnIndex + " begins.</color>"
            });
        }

        private static void ResetTemporaryCardModifiers(PlayerBattleState player)
        {
            if (player == null)
            {
                return;
            }

            ResetCostDelta(player.hand);
            ResetCostDelta(player.drawPile);
            ResetCostDelta(player.discardPile);
            ResetCostDelta(player.exhaustPile);
        }

        private static void ResetCostDelta(System.Collections.Generic.List<BattleCardState> cards)
        {
            if (cards == null)
            {
                return;
            }

            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].costDelta = 0;
                }
            }
        }

        private void TryResolveEncounterEnd(BattleState state, BattleCommandResult result)
        {
            if (state.monster != null && state.monster.coreHp > 0)
            {
                return;
            }

            var encounterIds = state.stageEncounterIds;
            if (encounterIds == null || encounterIds.Length == 0)
            {
                state.phase = BattlePhase.Victory;
                state.currentPrompt = "Victory";
                result.events.Add(new BattleEvent
                {
                    message = "<color=#7FD67F>The monster was defeated. Victory.</color>"
                });
                return;
            }

            var clearedEncounterName = string.IsNullOrWhiteSpace(state.encounterId) ? "unknown" : state.encounterId;
            var hasNextEncounter = state.stageEncounterIndex + 1 < encounterIds.Length;
            if (!hasNextEncounter)
            {
                state.phase = BattlePhase.Victory;
                state.currentPrompt = "Stage Cleared";
                result.events.Add(new BattleEvent
                {
                    message = "<color=#7FD67F>Encounter cleared:</color> " + clearedEncounterName
                });
                result.events.Add(new BattleEvent
                {
                    message = "<color=#7FD67F>Stage cleared:</color> " + (string.IsNullOrWhiteSpace(state.stageName) ? state.stageId : state.stageName)
                });
                return;
            }

            state.stageEncounterIndex += 1;
            state.encounterId = encounterIds[state.stageEncounterIndex];
            var nextMonsterDef = _dataRepository.GetEncounterMonster(state.encounterId);
            state.monster = MonsterAI.BuildMonster(nextMonsterDef, state.randomSeed ^ state.turnIndex ^ state.stageEncounterIndex);

            state.phase = BattlePhase.PlayerTurn;
            BattleTargetResolver.ResetTeamTurn(state.players);
            for (var i = 0; i < state.players.Count; i++)
            {
                var player = state.players[i];
                player.cardsPlayedThisTurn = 0;
                player.attackChargeStage = 0;
                player.nextAttackBonus = 0;
                if (player.hp > 0)
                {
                    player.energy = BaseEnergyPerTurn;
                    BattleMechanics.DrawCards(state, player, CardsDrawnPerTurn);
                    BattleMechanics.AddMoveCard(state, player);
                }
            }

            state.currentPrompt = BuildTeamTurnPrompt(state);
            result.events.Add(new BattleEvent
            {
                message = "<color=#7FD67F>Encounter cleared:</color> " + clearedEncounterName
            });
            result.events.Add(new BattleEvent
            {
                message = "<color=#7FD67F>Next encounter:</color> " + state.encounterId + " (" + (state.stageEncounterIndex + 1) + "/" + encounterIds.Length + ")"
            });
        }

        private string BuildTeamTurnPrompt(BattleState state)
        {
            var encounterTotal = state.stageEncounterIds == null ? 0 : state.stageEncounterIds.Length;
            if (encounterTotal <= 0)
            {
                return "Team turn";
            }

            return "Team turn - Encounter " + (state.stageEncounterIndex + 1) + "/" + encounterTotal;
        }

        private void ResolveInitialStageAndEncounter(
            BattleSetupDefinition setup,
            BattleState state,
            out string openingEncounterId,
            out MonsterDefinition openingMonsterDefinition)
        {
            openingEncounterId = null;
            openingMonsterDefinition = null;

            if (!string.IsNullOrWhiteSpace(setup.monsterId))
            {
                var monster = _dataRepository.GetMonster(setup.monsterId);
                state.stageId = string.IsNullOrWhiteSpace(setup.stageId) ? "monster_debug" : setup.stageId;
                state.stageName = "Monster Debug";
                state.stageEncounterIds = new[] { "monster:" + setup.monsterId };
                state.stageEncounterIndex = 0;
                state.encounterId = state.stageEncounterIds[0];
                openingEncounterId = state.encounterId;
                openingMonsterDefinition = monster;
                return;
            }

            StageDefinition stage = null;
            if (!string.IsNullOrWhiteSpace(setup.stageId))
            {
                stage = _dataRepository.GetStage(setup.stageId);
            }
            else
            {
                var allStages = _dataRepository.GetAllStages();
                if (allStages != null && allStages.Count > 0)
                {
                    stage = allStages[0];
                }
            }

            if (stage != null)
            {
                state.stageId = stage.id;
                state.stageName = stage.name;
                state.stageEncounterIds = stage.encounterIds;
                state.stageEncounterIndex = 0;
                state.encounterId = stage.encounterIds[0];
                openingEncounterId = state.encounterId;
                openingMonsterDefinition = _dataRepository.GetEncounterMonster(state.encounterId);
                return;
            }

            if (string.IsNullOrWhiteSpace(setup.encounterId))
            {
                throw new InvalidOperationException("Battle setup requires stageId, monsterId, or encounterId.");
            }

            state.stageId = "legacy_single_encounter";
            state.stageName = "Single Encounter";
            state.stageEncounterIds = new[] { setup.encounterId };
            state.stageEncounterIndex = 0;
            state.encounterId = setup.encounterId;
            openingEncounterId = setup.encounterId;
            openingMonsterDefinition = _dataRepository.GetEncounterMonster(setup.encounterId);
        }

        private void EnsureMonsterDefinition(BattleState state)
        {
            if (state == null || state.monster == null)
            {
                return;
            }

            var needsSkills = state.monster.skills == null || state.monster.skills.Length == 0;
            var needsPoses = state.monster.poses == null || state.monster.poses.Length == 0;
            if (!needsSkills && !needsPoses)
            {
                return;
            }

            if (string.IsNullOrEmpty(state.encounterId) || state.encounterId.StartsWith("monster:", StringComparison.Ordinal))
            {
                return;
            }

            var encounterMonster = _dataRepository.GetEncounterMonster(state.encounterId);

            if (!string.IsNullOrEmpty(state.monster.monsterId)
                && !string.Equals(state.monster.monsterId, encounterMonster.monsterId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (needsSkills)
            {
                state.monster.skills = encounterMonster.skills;
            }

            if (needsPoses)
            {
                state.monster.poses = encounterMonster.poses;
            }

            if (string.IsNullOrEmpty(state.monster.currentPoseId))
            {
                MonsterAI.ApplyMonsterPose(state.monster, MonsterAI.ResolveInitialPose(encounterMonster));
            }
        }
    }
}
