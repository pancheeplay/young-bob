using System;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Battle
{
    public sealed class BattleEngine
    {
        internal const int BaseEnergyPerTurn = 3;
        internal const int CardsDrawnPerTurn = 2;
        internal const int MaxHandSize = 10;
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
            var encounter = _dataRepository.GetEncounter(setup.encounterId);
            var deck = _dataRepository.GetDeck(setup.starterDeckId);
            var state = new BattleState
            {
                roomId = setup.roomId,
                encounterId = setup.encounterId,
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
                        cardId = deck.cards[i]
                    });
                }

                BattleMechanics.Shuffle(player.drawPile, setup.randomSeed ^ participant.playerId.GetHashCode());
                BattleMechanics.DrawCards(state, player, CardsDrawnPerTurn);
                BattleMechanics.AddMoveCard(state, player);
                state.players.Add(player);
            }

            state.monster = MonsterAI.BuildMonster(encounter, setup.randomSeed);
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
            if (actingPlayer.energy < definition.energyCost)
            {
                result.error = "Not enough energy.";
                return result;
            }

            var targetType = BattleTargetResolver.ParseTargetType(definition.targetType);
            CardEffectResolver.ResolveCardEffect(state, actingPlayer, command, definition, targetType, result);
            if (!string.IsNullOrEmpty(result.error))
            {
                return result;
            }

            actingPlayer.energy -= definition.energyCost;
            actingPlayer.hand.Remove(card);
            actingPlayer.discardPile.Add(card);
            actingPlayer.cardsPlayedThisTurn += 1;
            BattleMechanics.TryResolveBattleEnd(state, result);
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
                state.phase = BattlePhase.Victory;
                state.currentPrompt = "Victory";
                result.events.Add(new BattleEvent { message = "The monster has fallen." });
                return;
            }

            EnsureMonsterDefinition(state);
            MonsterAI.ResolveMonsterSkill(state, result);

            if (BattleTargetResolver.AllPlayersDead(state.players))
            {
                state.phase = BattlePhase.Defeat;
                state.currentPrompt = "Defeat";
                result.events.Add(new BattleEvent { message = "The party was defeated." });
                return;
            }

            state.phase = BattlePhase.PlayerTurn;
            state.turnIndex += 1;
            BattleTargetResolver.ResetTeamTurn(state.players);
            for (var i = 0; i < state.players.Count; i++)
            {
                if (state.players[i].hp > 0)
                {
                    state.players[i].energy = BaseEnergyPerTurn;
                    state.players[i].cardsPlayedThisTurn = 0;
                    BattleMechanics.DrawCards(state, state.players[i], CardsDrawnPerTurn);
                    BattleMechanics.AddMoveCard(state, state.players[i]);
                }
            }

            state.currentPrompt = "Team turn";
            result.events.Add(new BattleEvent
            {
                message = "<color=#E6C36A>Turn " + state.turnIndex + " begins.</color>"
            });
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

            if (string.IsNullOrEmpty(state.encounterId))
            {
                return;
            }

            var encounter = _dataRepository.GetEncounter(state.encounterId);
            if (encounter == null || encounter.monster == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(state.monster.monsterId)
                && !string.Equals(state.monster.monsterId, encounter.monster.monsterId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (needsSkills)
            {
                state.monster.skills = encounter.monster.skills;
            }

            if (needsPoses)
            {
                state.monster.poses = encounter.monster.poses;
            }

            if (string.IsNullOrEmpty(state.monster.currentPoseId))
            {
                MonsterAI.ApplyMonsterPose(state.monster, MonsterAI.ResolveInitialPose(encounter.monster));
            }
        }
    }
}
