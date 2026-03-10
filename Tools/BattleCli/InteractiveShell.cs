using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;

namespace YoungBob.Tools.BattleCli;

internal sealed class LegalAction
{
    public string label = string.Empty;
    public BattleCommand command = new();
}

public sealed class InteractiveShell
{
    private const string DefaultPlayerId = "player1";
    private const string DefaultEncounter = "slime_intro";
    private const string DefaultDeck = "co_op_starter";

    private readonly GameDataRepository _repo;
    private readonly BattleEngine _engine;

    private string _playerId = DefaultPlayerId;
    private string _displayName = "Hero";
    private BattleState? _state;
    private List<LegalAction>? _legal;
    private readonly List<string> _eventLog = new();

    public InteractiveShell(GameDataRepository repo)
    {
        _repo = repo;
        _engine = new BattleEngine(repo);
    }

    public void Run()
    {
        Console.WriteLine("Young Bob Battle CLI (interactive)");
        Console.WriteLine("Type 'help' for commands, 'new' to start a battle.");
        Console.WriteLine();

        while (true)
        {
            Console.Write(FormatPrompt());
            var line = Console.ReadLine();
            if (line == null) break;
            line = line.Trim();
            if (line.Length == 0) continue;

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cmd = tokens[0].ToLowerInvariant();

            try
            {
                if (cmd is "quit" or "q" or "exit") break;
                Dispatch(cmd, tokens);
            }
            catch (Exception ex)
            {
                Console.WriteLine("error: " + ex.Message);
            }
        }
    }

    // ================================================================
    // Prompt
    // ================================================================

    private string FormatPrompt()
    {
        if (_state == null) return "> ";
        var p = _state.GetPlayer(_playerId);
        var energy = p != null ? p.energy.ToString() : "?";
        var phase = _state.phase switch
        {
            BattlePhase.PlayerTurn => "▶",
            BattlePhase.MonsterTurn => "🐉",
            BattlePhase.Victory => "✓",
            BattlePhase.Defeat => "✗",
            _ => "…"
        };
        return $"[T{_state.turnIndex} ⚡{energy} {phase}]> ";
    }

    // ================================================================
    // Command dispatch
    // ================================================================

    private void Dispatch(string cmd, string[] tokens)
    {
        switch (cmd)
        {
            case "help": PrintHelp(); break;
            case "new": case "n": CmdNew(tokens); break;
            case "state": case "s": CmdState(); break;
            case "hand": case "h": CmdHand(); break;
            case "monster": case "m": CmdMonster(); break;
            case "legal": case "l": CmdLegal(); break;
            case "do": case "d": CmdDo(tokens); break;
            case "play": case "p": CmdPlay(tokens); break;
            case "end": case "e": CmdEndTurn(); break;
            case "log": CmdLog(tokens); break;
            default:
                Console.WriteLine("Unknown command: " + cmd + ". Type 'help'.");
                break;
        }
    }

    private void PrintHelp()
    {
        Console.WriteLine(@"
=== Start ===
  new [encounter] [deck] [name]  Start a new battle
                                 Defaults: slime_intro, co_op_starter, Hero

=== View ===
  state / s                      Full battle state summary
  hand / h                       Your hand with card details
  monster / m                    Monster details (parts, skills, pose)

=== Act ===
  legal / l                      List all legal actions (numbered)
  do <index>                     Execute a legal action by index
  play <handIdx> [target]        Play a card (target: part name, area, or player)
  end / e                        End your turn

=== Other ===
  log [n]                        Show last n events (default: 20)
  help                           This message
  quit / q                       Exit
");
    }

    // ================================================================
    // Commands
    // ================================================================

    private void CmdNew(string[] tokens)
    {
        var encounter = tokens.Length > 1 ? tokens[1] : DefaultEncounter;
        var deck = tokens.Length > 2 ? tokens[2] : DefaultDeck;
        _displayName = tokens.Length > 3 ? string.Join(" ", tokens, 3, tokens.Length - 3) : "Hero";
        _playerId = DefaultPlayerId;

        var setup = new BattleSetupDefinition
        {
            roomId = "cli",
            randomSeed = Environment.TickCount,
            encounterId = encounter,
            starterDeckId = deck
        };
        setup.players.Add(new BattleParticipantDefinition
        {
            playerId = _playerId,
            displayName = _displayName
        });

        _state = _engine.CreateInitialState(setup);
        _legal = null;
        _eventLog.Clear();
        _eventLog.Add("Battle started: encounter=" + encounter + " deck=" + deck);

        Console.WriteLine("Battle started!  encounter=" + encounter + "  deck=" + deck);
        Console.WriteLine("You are " + _displayName + " (" + _playerId + ")");
        Console.WriteLine();
        PrintFullState();
    }

    private void CmdState()
    {
        RequireBattle();
        PrintFullState();
    }

    private void CmdHand()
    {
        RequireBattle();
        var p = RequirePlayer();
        PrintHand(p);
    }

    private void CmdMonster()
    {
        RequireBattle();
        if (_state!.monster == null) { Console.WriteLine("No monster."); return; }
        PrintMonster(_state.monster);
    }

    private void CmdLegal()
    {
        RequireBattle();
        _legal = BuildLegalActions();
        Console.WriteLine("Legal actions:");
        if (_legal.Count == 0)
        {
            Console.WriteLine("  (none — battle may be over or not your turn)");
            return;
        }
        for (var i = 0; i < _legal.Count; i++)
        {
            Console.WriteLine($"  [{i}] {_legal[i].label}");
        }
    }

    private void CmdDo(string[] tokens)
    {
        RequireBattle();
        if (_legal == null || _legal.Count == 0)
        {
            Console.WriteLine("Run 'legal' first to see available actions.");
            return;
        }
        if (tokens.Length < 2)
        {
            Console.WriteLine("Usage: do <index>  (run 'legal' to see options)");
            return;
        }
        if (!int.TryParse(tokens[1], out var idx) || idx < 0 || idx >= _legal.Count)
        {
            Console.WriteLine("Invalid index. Range: 0.." + (_legal.Count - 1));
            return;
        }

        var action = _legal[idx];
        // Generate a fresh commandId for this execution
        action.command.commandId = Guid.NewGuid().ToString("N");
        ApplyAndShow(action.command);
    }

    private void CmdPlay(string[] tokens)
    {
        RequireBattle();
        var p = RequirePlayer();

        if (tokens.Length < 2)
        {
            Console.WriteLine("Usage: play <handIndex> [target]");
            Console.WriteLine("  target can be: part name (head, body...), area (west, east), or player name");
            return;
        }
        if (!int.TryParse(tokens[1], out var idx) || idx < 0 || idx >= p.hand.Count)
        {
            Console.WriteLine("Invalid hand index. You have " + p.hand.Count + " cards (0.." + (p.hand.Count - 1) + ").");
            return;
        }

        var card = p.hand[idx];
        var def = _repo.GetCard(card.cardId);
        var targetType = BattleTargetResolver.ParseTargetType(def.targetType);
        var targetHint = tokens.Length >= 3
            ? string.Join(" ", tokens, 2, tokens.Length - 2).ToLowerInvariant()
            : null;

        var command = new BattleCommand
        {
            commandId = Guid.NewGuid().ToString("N"),
            actorPlayerId = _playerId,
            action = "play_card",
            cardInstanceId = card.instanceId
        };

        ResolveTarget(command, targetType, targetHint);
        ApplyAndShow(command);
    }

    private void CmdEndTurn()
    {
        RequireBattle();
        var command = new BattleCommand
        {
            commandId = Guid.NewGuid().ToString("N"),
            actorPlayerId = _playerId,
            action = "end_turn"
        };
        ApplyAndShow(command);
    }

    private void CmdLog(string[] tokens)
    {
        var count = tokens.Length > 1 && int.TryParse(tokens[1], out var n) ? n : 20;
        var start = Math.Max(0, _eventLog.Count - count);
        if (_eventLog.Count == 0)
        {
            Console.WriteLine("(no events yet)");
            return;
        }
        for (var i = start; i < _eventLog.Count; i++)
        {
            Console.WriteLine("  " + _eventLog[i]);
        }
    }

    // ================================================================
    // Core: apply command and show results
    // ================================================================

    private void ApplyAndShow(BattleCommand command)
    {
        var result = _engine.Apply(_state!, command);
        _legal = null; // invalidate cached legal actions

        if (!result.success)
        {
            Console.WriteLine("✗ Rejected: " + (result.error ?? "unknown"));
            return;
        }

        // Print events
        for (var i = 0; i < result.events.Count; i++)
        {
            var msg = StripTags(result.events[i].message);
            Console.WriteLine("  " + msg);
            _eventLog.Add(msg);
        }

        Console.WriteLine();

        // Show end-of-battle or updated state
        if (_state!.phase == BattlePhase.Victory)
        {
            Console.WriteLine("═══════════════════════════");
            Console.WriteLine("  ✓  VICTORY");
            Console.WriteLine("═══════════════════════════");
            if (_state.loot.Count > 0)
            {
                Console.WriteLine("Loot: " + string.Join(", ", _state.loot));
            }
        }
        else if (_state.phase == BattlePhase.Defeat)
        {
            Console.WriteLine("═══════════════════════════");
            Console.WriteLine("  ✗  DEFEAT");
            Console.WriteLine("═══════════════════════════");
        }
        else
        {
            PrintFullState();
        }
    }

    // ================================================================
    // Formatting
    // ================================================================

    private void PrintFullState()
    {
        var s = _state!;
        var p = s.GetPlayer(_playerId);

        Console.WriteLine($"══ Turn {s.turnIndex} ({s.phase}) ══");

        // Player info
        if (p != null)
        {
            var chargeInfo = p.attackChargeStage > 0
                ? $" | Charge:{p.attackChargeStage}(+{p.nextAttackBonus})"
                : "";
            Console.WriteLine(
                $"{p.displayName} | HP {p.hp}/{p.maxHp} | Armor {p.armor} | ⚡{p.energy} | {p.area}/{p.height}{chargeInfo}");
        }

        // Monster info
        if (s.monster != null)
        {
            Console.WriteLine();
            PrintMonster(s.monster);
        }

        // Hand
        if (p != null && p.hand.Count > 0)
        {
            Console.WriteLine();
            PrintHand(p);
        }
    }

    private void PrintHand(PlayerBattleState p)
    {
        Console.WriteLine("Hand:");
        for (var i = 0; i < p.hand.Count; i++)
        {
            var card = p.hand[i];
            var def = _repo.GetCard(card.cardId);
            var name = string.IsNullOrEmpty(def.name) ? def.id : def.name;
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(def.effectType)) parts.Add(def.effectType);
            if (def.value != 0) parts.Add(def.value.ToString());
            parts.Add(def.energyCost + "⚡");

            if (!string.IsNullOrEmpty(def.rangeHeights) &&
                !string.Equals(def.rangeHeights, "Both", StringComparison.OrdinalIgnoreCase))
                parts.Add("H:" + def.rangeHeights);
            if (!string.IsNullOrEmpty(def.rangeDistance) &&
                !string.Equals(def.rangeDistance, "Both", StringComparison.OrdinalIgnoreCase))
                parts.Add("R:" + def.rangeDistance);

            var classTag = string.IsNullOrEmpty(def.classTag) ? "" : $"[{def.classTag}] ";

            Console.WriteLine($"  [{i}] {classTag}{name,-12} {string.Join(", ", parts)}");
        }
    }

    private void PrintMonster(MonsterBattleState m)
    {
        Console.WriteLine(
            $"Monster: {m.displayName} | HP {m.coreHp}/{m.coreMaxHp} | Facing:{m.facing} | Stance:{m.stance} | Pose:{m.currentPoseId ?? "?"}");

        // Parts
        for (var i = 0; i < m.parts.Count; i++)
        {
            var part = m.parts[i];
            var broken = part.isBroken ? " [BROKEN]" : "";
            Console.WriteLine(
                $"  {part.displayName,-10} {part.hp,3}/{part.maxHp} hp  {part.relativeHeight}/{part.relativeZone}{broken}");
        }

        // Active skill warning
        if (m.hasActiveSkill && m.activeSkill != null)
        {
            var sk = m.activeSkill;
            var timing = sk.remainingWindup > 0
                ? $"windup {sk.remainingWindup} turn(s)"
                : "⚡ EXECUTES THIS TURN";
            Console.WriteLine(
                $"  ⚠ Charging: {sk.displayName} → {sk.targetArea}/{sk.targetHeight} ({sk.damage} dmg) [{timing}]");
        }

        // Skill cooldowns
        if (m.skills != null && m.skillCooldowns != null)
        {
            var parts = new List<string>();
            for (var i = 0; i < m.skills.Length && i < m.skillCooldowns.Length; i++)
            {
                var cd = m.skillCooldowns[i];
                parts.Add(m.skills[i].name + (cd > 0 ? $"(cd:{cd})" : "(ready)"));
            }
            if (parts.Count > 0)
            {
                Console.WriteLine("  Skills: " + string.Join(", ", parts));
            }
        }
    }

    // ================================================================
    // Legal actions builder
    // ================================================================

    private List<LegalAction> BuildLegalActions()
    {
        var list = new List<LegalAction>();
        var p = _state!.GetPlayer(_playerId);
        if (p == null || p.hp <= 0 || p.hasEndedTurn || _state.phase != BattlePhase.PlayerTurn)
        {
            return list;
        }

        for (var i = 0; i < p.hand.Count; i++)
        {
            var card = p.hand[i];
            var def = _repo.GetCard(card.cardId);
            if (p.energy < def.energyCost) continue;

            var cardName = string.IsNullOrEmpty(def.name) ? def.id : def.name;
            var targetType = BattleTargetResolver.ParseTargetType(def.targetType);
            var costTag = def.energyCost + "⚡";
            var effectTag = FormatEffectBrief(def);

            switch (targetType)
            {
                case BattleTargetType.Self:
                    list.Add(MakeLegalPlay(
                        $"{cardName} → Self            ({effectTag}, {costTag})",
                        card.instanceId, BattleTargetFaction.Allies, _playerId, BattleArea.West));
                    break;

                case BattleTargetType.SingleAlly:
                case BattleTargetType.OtherAlly:
                    foreach (var ally in _state.players)
                    {
                        if (ally.hp <= 0) continue;
                        if (targetType == BattleTargetType.OtherAlly && ally.playerId == _playerId) continue;
                        list.Add(MakeLegalPlay(
                            $"{cardName} → {ally.displayName} ({ally.hp}hp)  ({effectTag}, {costTag})",
                            card.instanceId, BattleTargetFaction.Allies, ally.playerId, BattleArea.West));
                    }
                    break;

                case BattleTargetType.AllAllies:
                    list.Add(MakeLegalPlay(
                        $"{cardName} → All Allies       ({effectTag}, {costTag})",
                        card.instanceId, BattleTargetFaction.Allies, string.Empty, BattleArea.West));
                    break;

                case BattleTargetType.MonsterPart:
                    if (_state.monster != null)
                    {
                        foreach (var part in _state.monster.parts)
                        {
                            if (part.hp <= 0) continue;
                            var inRange = BattleTargetResolver.IsPartInRange(_state.monster, part, def, p.area);
                            var rangeTag = inRange ? "" : " ⛔OUT OF RANGE";
                            list.Add(MakeLegalPlay(
                                $"{cardName} → {part.displayName} ({part.hp}hp){rangeTag}  ({effectTag}, {costTag})",
                                card.instanceId, BattleTargetFaction.Enemies, part.instanceId, BattleArea.West));
                        }
                    }
                    break;

                case BattleTargetType.AllMonsterParts:
                    list.Add(MakeLegalPlay(
                        $"{cardName} → All Parts        ({effectTag}, {costTag})",
                        card.instanceId, BattleTargetFaction.Enemies, string.Empty, BattleArea.West));
                    break;

                case BattleTargetType.SingleUnit:
                    foreach (var ally in _state.players)
                    {
                        if (ally.hp > 0)
                        {
                            list.Add(MakeLegalPlay(
                                $"{cardName} → {ally.displayName} ({ally.hp}hp)  ({effectTag}, {costTag})",
                                card.instanceId, BattleTargetFaction.Allies, ally.playerId, BattleArea.West));
                        }
                    }
                    if (_state.monster != null)
                    {
                        foreach (var part in _state.monster.parts)
                        {
                            if (part.hp > 0)
                            {
                                list.Add(MakeLegalPlay(
                                    $"{cardName} → {part.displayName} ({part.hp}hp)  ({effectTag}, {costTag})",
                                    card.instanceId, BattleTargetFaction.Enemies, part.instanceId, BattleArea.West));
                            }
                        }
                    }
                    break;

                case BattleTargetType.Area:
                    list.Add(MakeLegalPlay(
                        $"{cardName} → West             ({effectTag}, {costTag})",
                        card.instanceId, BattleTargetFaction.None, string.Empty, BattleArea.West));
                    list.Add(MakeLegalPlay(
                        $"{cardName} → East             ({effectTag}, {costTag})",
                        card.instanceId, BattleTargetFaction.None, string.Empty, BattleArea.East));
                    break;

                default:
                    list.Add(MakeLegalPlay(
                        $"{cardName}                    ({effectTag}, {costTag})",
                        card.instanceId, BattleTargetFaction.None, string.Empty, BattleArea.West));
                    break;
            }
        }

        // End turn is always available
        list.Add(new LegalAction
        {
            label = "End Turn",
            command = new BattleCommand
            {
                commandId = Guid.NewGuid().ToString("N"),
                actorPlayerId = _playerId,
                action = "end_turn"
            }
        });

        return list;
    }

    private static string FormatEffectBrief(CardDefinition def)
    {
        var effect = def.effectType ?? "?";
        if (def.value != 0)
            return effect + " " + def.value;
        return effect;
    }

    private static LegalAction MakeLegalPlay(string label, string cardInstanceId,
        BattleTargetFaction faction, string targetUnitId, BattleArea area)
    {
        return new LegalAction
        {
            label = label,
            command = new BattleCommand
            {
                commandId = Guid.NewGuid().ToString("N"),
                actorPlayerId = DefaultPlayerId,
                action = "play_card",
                cardInstanceId = cardInstanceId,
                targetFaction = faction,
                targetUnitId = targetUnitId,
                targetArea = area
            }
        };
    }

    // ================================================================
    // Target resolution for 'play' command (fuzzy matching)
    // ================================================================

    private void ResolveTarget(BattleCommand command, BattleTargetType targetType, string? hint)
    {
        switch (targetType)
        {
            case BattleTargetType.Self:
                command.targetFaction = BattleTargetFaction.Allies;
                command.targetUnitId = _playerId;
                break;

            case BattleTargetType.SingleAlly:
            case BattleTargetType.OtherAlly:
                command.targetFaction = BattleTargetFaction.Allies;
                command.targetUnitId = FuzzyMatchPlayer(hint) ?? _playerId;
                break;

            case BattleTargetType.MonsterPart:
                command.targetFaction = BattleTargetFaction.Enemies;
                command.targetUnitId = FuzzyMatchPart(hint) ?? string.Empty;
                break;

            case BattleTargetType.Area:
                command.targetArea = ParseAreaHint(hint);
                break;

            case BattleTargetType.SingleUnit:
                // Try monster part first, then player
                var partMatch = FuzzyMatchPart(hint);
                if (partMatch != null)
                {
                    command.targetFaction = BattleTargetFaction.Enemies;
                    command.targetUnitId = partMatch;
                }
                else
                {
                    command.targetFaction = BattleTargetFaction.Allies;
                    command.targetUnitId = FuzzyMatchPlayer(hint) ?? _playerId;
                }
                break;

            default:
                // No target needed (AllAllies, AllMonsterParts, None)
                break;
        }
    }

    private string? FuzzyMatchPlayer(string? hint)
    {
        if (_state == null) return null;
        if (string.IsNullOrEmpty(hint))
        {
            // Default to first alive player
            foreach (var p in _state.players)
                if (p.hp > 0) return p.playerId;
            return null;
        }
        foreach (var p in _state.players)
        {
            if (p.hp > 0 &&
                (p.displayName.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
                 p.playerId.Contains(hint, StringComparison.OrdinalIgnoreCase)))
                return p.playerId;
        }
        return null;
    }

    private string? FuzzyMatchPart(string? hint)
    {
        if (_state?.monster == null) return null;
        if (string.IsNullOrEmpty(hint))
        {
            // Default to first alive part
            foreach (var part in _state.monster.parts)
                if (part.hp > 0) return part.instanceId;
            return null;
        }
        foreach (var part in _state.monster.parts)
        {
            if (part.hp > 0 &&
                (part.displayName.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
                 part.partId.Contains(hint, StringComparison.OrdinalIgnoreCase)))
                return part.instanceId;
        }
        return null;
    }

    private static BattleArea ParseAreaHint(string? hint)
    {
        if (string.IsNullOrEmpty(hint)) return BattleArea.West;
        if (hint.StartsWith("e", StringComparison.OrdinalIgnoreCase)) return BattleArea.East;
        if (hint.StartsWith("m", StringComparison.OrdinalIgnoreCase)) return BattleArea.Middle;
        return BattleArea.West;
    }

    // ================================================================
    // Helpers
    // ================================================================

    private void RequireBattle()
    {
        if (_state == null)
            throw new InvalidOperationException("No active battle. Use 'new' to start one.");
    }

    private PlayerBattleState RequirePlayer()
    {
        return _state!.GetPlayer(_playerId)
               ?? throw new InvalidOperationException("Player not found in battle.");
    }

    private static string StripTags(string text)
    {
        return Regex.Replace(text, @"</?color[^>]*>", "");
    }
}
