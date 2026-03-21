using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Testing
{
    public sealed class InProcessBattleTestDriver : IBattleTestDriver
    {
        private readonly GameDataRepository _dataRepository;
        private readonly BattleEngine _battleEngine;

        private DriverSetupOptions _setup;
        private BattleState _state;

        public InProcessBattleTestDriver(GameDataRepository dataRepository)
        {
            _dataRepository = dataRepository;
            _battleEngine = new BattleEngine(_dataRepository);
        }

        public DriverActionResult Setup(DriverSetupOptions options)
        {
            if (options == null)
            {
                return Fail("Setup options is null.");
            }

            if (options.players == null || options.players.Count == 0)
            {
                return Fail("Setup players is empty.");
            }

            _setup = options;
            _state = null;
            return Success("setup");
        }

        public DriverActionResult StartBattle(string hostPlayerId)
        {
            if (_setup == null)
            {
                return Fail("Call Setup before StartBattle.");
            }

            if (string.IsNullOrWhiteSpace(hostPlayerId))
            {
                return Fail("Host player id is empty.");
            }

            if (!_setup.players.Any(player => player != null && player.playerId == hostPlayerId))
            {
                return Fail("Host player not in setup players.");
            }

            try
            {
                var definition = new BattleSetupDefinition
                {
                    roomId = _setup.roomId,
                    stageId = _setup.stageId,
                    encounterId = _setup.encounterId,
                    monsterId = _setup.monsterId,
                    starterDeckId = _setup.starterDeckId,
                    randomSeed = _setup.randomSeed
                };

                for (var i = 0; i < _setup.players.Count; i++)
                {
                    var player = _setup.players[i];
                    definition.players.Add(new BattleParticipantDefinition
                    {
                        playerId = player.playerId,
                        displayName = player.displayName
                    });
                }

                _state = _battleEngine.CreateInitialState(definition);
                return Success("start_battle");
            }
            catch (Exception ex)
            {
                return Fail("StartBattle exception: " + ex.Message);
            }
        }

        public DriverActionResult PlayCard(string actorPlayerId, string cardInstanceId, BattleTargetFaction targetFaction, string targetUnitId, BattleArea targetArea)
        {
            if (_state == null)
            {
                return Fail("Battle not started.");
            }

            var resolvedCardInstanceId = ResolveCardInstanceId(actorPlayerId, cardInstanceId);
            if (string.IsNullOrWhiteSpace(resolvedCardInstanceId))
            {
                return Fail("Cannot resolve card instance id for actor " + actorPlayerId + ".");
            }

            var result = _battleEngine.Apply(_state, new BattleCommand
            {
                commandId = Guid.NewGuid().ToString("N"),
                actorPlayerId = actorPlayerId,
                action = "play_card",
                cardInstanceId = resolvedCardInstanceId,
                targetFaction = targetFaction,
                targetUnitId = targetUnitId,
                targetArea = targetArea
            });

            return FromCommandResult(result, "play_card");
        }

        public DriverActionResult EndTurn(string actorPlayerId)
        {
            if (_state == null)
            {
                return Fail("Battle not started.");
            }

            var result = _battleEngine.Apply(_state, new BattleCommand
            {
                commandId = Guid.NewGuid().ToString("N"),
                actorPlayerId = actorPlayerId,
                action = "end_turn"
            });

            return FromCommandResult(result, "end_turn");
        }

        public DriverActionResult DebugDamageMonster(int amount)
        {
            if (_state == null)
            {
                return Fail("Battle not started.");
            }

            if (_state.monster == null)
            {
                return Fail("Monster not found.");
            }

            var damage = Math.Max(0, amount);
            _state.monster.coreHp = Math.Max(0, _state.monster.coreHp - damage);
            if (_state.monster.parts != null)
            {
                for (var i = 0; i < _state.monster.parts.Count; i++)
                {
                    var part = _state.monster.parts[i];
                    part.hp = Math.Max(0, part.hp - damage);
                    if (part.hp == 0)
                    {
                        part.isBroken = true;
                    }
                }
            }

            return Success("debug_damage_monster");
        }

        public DriverActionResult DebugSetPlayerHp(string playerId, int hp)
        {
            if (_state == null)
            {
                return Fail("Battle not started.");
            }

            var player = _state.GetPlayer(playerId);
            if (player == null)
            {
                return Fail("Player not found: " + playerId);
            }

            player.hp = Math.Max(0, Math.Min(player.maxHp, hp));
            return Success("debug_set_player_hp");
        }

        public DriverActionResult Snapshot(string tag)
        {
            if (_state == null)
            {
                return Fail("Battle not started.");
            }

            return new DriverActionResult
            {
                success = true,
                snapshot = BuildSnapshot(tag, null)
            };
        }

        private DriverActionResult FromCommandResult(BattleCommandResult commandResult, string tag)
        {
            if (commandResult == null)
            {
                return Fail("Battle command returned null.");
            }

            return new DriverActionResult
            {
                success = commandResult.success,
                error = commandResult.error,
                snapshot = BuildSnapshot(tag, commandResult)
            };
        }

        private DriverActionResult Success(string tag)
        {
            return new DriverActionResult
            {
                success = true,
                snapshot = _state == null ? null : BuildSnapshot(tag, null)
            };
        }

        private static DriverActionResult Fail(string error)
        {
            return new DriverActionResult
            {
                success = false,
                error = error
            };
        }

        private DriverSnapshot BuildSnapshot(string tag, BattleCommandResult commandResult)
        {
            var stateJson = JsonUtility.ToJson(_state);
            return new DriverSnapshot
            {
                tag = string.IsNullOrWhiteSpace(tag) ? "snapshot" : tag,
                stateJson = stateJson,
                stateHash = ComputeSha256(stateJson),
                eventMessages = commandResult == null
                    ? Array.Empty<string>()
                    : BattleEventFormatter.FormatAll(commandResult.events, richText: false),
                error = commandResult == null ? null : commandResult.error
            };
        }

        private string ResolveCardInstanceId(string actorPlayerId, string rawCardInstanceId)
        {
            if (string.IsNullOrWhiteSpace(rawCardInstanceId))
            {
                return null;
            }

            if (!rawCardInstanceId.StartsWith("card:", StringComparison.Ordinal))
            {
                return rawCardInstanceId;
            }

            var player = _state.GetPlayer(actorPlayerId);
            if (player == null)
            {
                return null;
            }

            var cardId = rawCardInstanceId.Substring("card:".Length);
            var match = player.hand.FirstOrDefault(item => item.cardId == cardId);
            return match == null ? null : match.instanceId;
        }

        private static string ComputeSha256(string text)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }

            return sb.ToString();
        }
    }
}
