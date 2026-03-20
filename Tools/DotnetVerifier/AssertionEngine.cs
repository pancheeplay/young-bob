using System.Text.Json;
using YoungBob.Prototype.Battle;

internal static class AssertionEngine
{
    public static void EvaluateAssertions(ScenarioReport report, List<ScenarioAssertion> assertions)
    {
        for (var i = 0; i < assertions.Count; i++)
        {
            var assertion = assertions[i];
            switch ((assertion.type ?? string.Empty).Trim())
            {
                case "snapshot_contains":
                    EvaluateSnapshotContains(report, assertion, i, shouldContain: true);
                    break;

                case "snapshot_not_contains":
                    EvaluateSnapshotContains(report, assertion, i, shouldContain: false);
                    break;

                case "snapshot_error_contains":
                    EvaluateSnapshotErrorContains(report, assertion, i);
                    break;

                case "snapshot_hash_equals":
                    EvaluateSnapshotHashEquals(report, assertion, i);
                    break;

                case "event_contains":
                    EvaluateEventContains(report, assertion, i);
                    break;

                case "event_sequence":
                    EvaluateEventSequence(report, assertion, i);
                    break;

                case "player_field_equals":
                    EvaluatePlayerFieldEquals(report, assertion, i);
                    break;

                case "player_status_equals":
                    EvaluatePlayerStatusEquals(report, assertion, i);
                    break;

                case "player_hand_has_card":
                    EvaluatePlayerPileHasCard(report, assertion, i, "hand");
                    break;

                case "player_discard_has_card":
                    EvaluatePlayerPileHasCard(report, assertion, i, "discard");
                    break;

                case "monster_core_hp_equals":
                    EvaluateMonsterCoreHpEquals(report, assertion, i);
                    break;

                case "monster_status_equals":
                    EvaluateMonsterStatusEquals(report, assertion, i);
                    break;

                case "part_field_equals":
                    EvaluatePartFieldEquals(report, assertion, i);
                    break;

                case "part_status_equals":
                    EvaluatePartStatusEquals(report, assertion, i);
                    break;

                case "phase_equals":
                    EvaluatePhaseEquals(report, assertion, i);
                    break;

                case "todo":
                    break;

                default:
                    report.failures.Add($"assertion[{i}] unknown type: {assertion.type}");
                    break;
            }
        }
    }

    private static void EvaluateSnapshotContains(ScenarioReport report, ScenarioAssertion assertion, int index, bool shouldContain)
    {
        var snap = VerifierInfra.FindSnapshot(report, assertion.actualPath);
        if (snap == null)
        {
            report.failures.Add($"assertion[{index}] snapshot not found: {assertion.actualPath}");
            return;
        }

        var contains = (snap.stateJson ?? string.Empty).Contains(assertion.expected ?? string.Empty, StringComparison.Ordinal);
        if (contains != shouldContain)
        {
            var verb = shouldContain ? "contain" : "not contain";
            report.failures.Add($"assertion[{index}] expected snapshot '{snap.tag}' to {verb}: {assertion.expected}");
        }
    }

    private static void EvaluateSnapshotErrorContains(ScenarioReport report, ScenarioAssertion assertion, int index)
    {
        var snap = VerifierInfra.FindSnapshot(report, assertion.actualPath);
        if (snap == null)
        {
            report.failures.Add($"assertion[{index}] snapshot not found: {assertion.actualPath}");
            return;
        }

        var expected = assertion.expected ?? string.Empty;
        if ((snap.error ?? string.Empty).IndexOf(expected, StringComparison.Ordinal) < 0)
        {
            report.failures.Add($"assertion[{index}] expected error to contain '{expected}' in snapshot '{snap.tag}'");
        }
    }

    private static void EvaluateSnapshotHashEquals(ScenarioReport report, ScenarioAssertion assertion, int index)
    {
        var split = (assertion.actualPath ?? string.Empty).Split('|');
        if (split.Length != 2)
        {
            report.failures.Add($"assertion[{index}] invalid hash compare path");
            return;
        }

        var left = VerifierInfra.FindSnapshot(report, split[0].Trim());
        var right = VerifierInfra.FindSnapshot(report, split[1].Trim());
        if (left == null || right == null)
        {
            report.failures.Add($"assertion[{index}] missing snapshots for hash compare");
            return;
        }

        if (!string.Equals(left.stateHash, right.stateHash, StringComparison.Ordinal))
        {
            report.failures.Add($"assertion[{index}] snapshot hash mismatch: {left.tag} vs {right.tag}");
        }
    }

    private static void EvaluateEventContains(ScenarioReport report, ScenarioAssertion assertion, int index)
    {
        if (!TryGetStep(report, assertion.actualPath, index, out var step))
        {
            return;
        }

        var events = step.events ?? new List<BattleEvent>();
        if (events.Count == 0)
        {
            report.failures.Add($"assertion[{index}] step '{step.tag}' has no events");
            return;
        }

        for (var i = 0; i < events.Count; i++)
        {
            var battleEvent = events[i];
            if (battleEvent == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(assertion.eventId)
                && !string.Equals(assertion.eventId, battleEvent.eventId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(assertion.actor)
                && !string.Equals(assertion.actor, battleEvent.actor, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(assertion.target)
                && !string.Equals(assertion.target, battleEvent.target, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(assertion.cardId)
                && !string.Equals(assertion.cardId, battleEvent.cardId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(assertion.statusId)
                && !string.Equals(assertion.statusId, battleEvent.statusId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(assertion.context)
                && !string.Equals(assertion.context, battleEvent.context, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(assertion.field))
            {
                var numberMatches = assertion.field switch
                {
                    "amount" => battleEvent.amount == assertion.expectedInt,
                    "amount2" => battleEvent.amount2 == assertion.expectedInt,
                    "turn" => battleEvent.turn == assertion.expectedInt,
                    "area" => (int)battleEvent.area == assertion.expectedInt,
                    _ => false
                };
                if (!numberMatches)
                {
                    continue;
                }
            }

            return;
        }

        report.failures.Add($"assertion[{index}] event not found in step '{step.tag}'");
    }

    private static void EvaluateEventSequence(ScenarioReport report, ScenarioAssertion assertion, int index)
    {
        if (!TryGetStep(report, assertion.actualPath, index, out var step))
        {
            return;
        }

        var expected = (assertion.expected ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(expected))
        {
            report.failures.Add($"assertion[{index}] event_sequence expected is empty");
            return;
        }

        var tokens = expected.Split(new[] { ">>" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            report.failures.Add($"assertion[{index}] event_sequence expected is empty");
            return;
        }

        var events = step.events ?? new List<BattleEvent>();
        var searchIndex = 0;
        for (var tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
        {
            var token = tokens[tokenIndex];
            var found = false;
            for (var eventIndex = searchIndex; eventIndex < events.Count; eventIndex++)
            {
                var battleEvent = events[eventIndex];
                if (battleEvent != null && string.Equals(token, battleEvent.eventId, StringComparison.Ordinal))
                {
                    found = true;
                    searchIndex = eventIndex + 1;
                    break;
                }
            }

            if (!found)
            {
                report.failures.Add($"assertion[{index}] expected event sequence token not found in order: {token}");
                return;
            }
        }
    }

    private static bool TryGetStep(ScenarioReport report, string tag, int index, out StepTrace step)
    {
        step = null;
        if (report.steps == null || report.steps.Count == 0)
        {
            report.failures.Add($"assertion[{index}] no step logs available for event assertion");
            return false;
        }

        if (string.IsNullOrWhiteSpace(tag))
        {
            step = report.steps[report.steps.Count - 1];
            return true;
        }

        for (var i = report.steps.Count - 1; i >= 0; i--)
        {
            if (string.Equals(report.steps[i].tag, tag, StringComparison.Ordinal))
            {
                step = report.steps[i];
                return true;
            }
        }

        report.failures.Add($"assertion[{index}] step tag not found for event log: {tag}");
        return false;
    }

    private static void EvaluatePlayerFieldEquals(ScenarioReport report, ScenarioAssertion assertion, int index)
    {
        if (!TryGetState(report, assertion.actualPath, index, out var state))
        {
            return;
        }

        var player = state.GetPlayer(assertion.playerId);
        if (player == null)
        {
            report.failures.Add($"assertion[{index}] player not found: {assertion.playerId}");
            return;
        }

        var actual = assertion.field switch
        {
            "hp" => player.hp,
            "armor" => player.armor,
            "energy" => player.energy,
            "vulnerableStacks" => player.vulnerableStacks,
            "cardsPlayedThisTurn" => player.cardsPlayedThisTurn,
            "threatValue" => player.threatValue,
            "threatTier" => player.threatTier,
            "area" => (int)player.area,
            _ => int.MinValue
        };

        if (actual == int.MinValue)
        {
            report.failures.Add($"assertion[{index}] unsupported player field: {assertion.field}");
            return;
        }

        if (actual != assertion.expectedInt)
        {
            report.failures.Add($"assertion[{index}] player {assertion.playerId} field {assertion.field} expected {assertion.expectedInt}, got {actual}");
        }
    }

    private static void EvaluatePlayerStatusEquals(ScenarioReport report, ScenarioAssertion assertion, int index)
    {
        if (!TryGetState(report, assertion.actualPath, index, out var state))
        {
            return;
        }

        var player = state.GetPlayer(assertion.playerId);
        if (player == null)
        {
            report.failures.Add($"assertion[{index}] player not found: {assertion.playerId}");
            return;
        }

        var actual = GetStatusStacks(player.statuses, assertion.statusId);
        if (actual != assertion.expectedInt)
        {
            report.failures.Add($"assertion[{index}] player {assertion.playerId} status {assertion.statusId} expected {assertion.expectedInt}, got {actual}");
        }
    }

    private static void EvaluatePlayerPileHasCard(ScenarioReport report, ScenarioAssertion assertion, int index, string pile)
    {
        if (!TryGetState(report, assertion.actualPath, index, out var state))
        {
            return;
        }

        var player = state.GetPlayer(assertion.playerId);
        if (player == null)
        {
            report.failures.Add($"assertion[{index}] player not found: {assertion.playerId}");
            return;
        }

        var found = pile switch
        {
            "hand" => player.hand.Any(c => c.cardId == assertion.cardId),
            "discard" => player.discardPile.Any(c => c.cardId == assertion.cardId),
            _ => false
        };

        if (!found)
        {
            report.failures.Add($"assertion[{index}] expected player {assertion.playerId} {pile} to contain {assertion.cardId}");
        }
    }

    private static void EvaluateMonsterCoreHpEquals(ScenarioReport report, ScenarioAssertion assertion, int index)
    {
        if (!TryGetState(report, assertion.actualPath, index, out var state))
        {
            return;
        }

        var actual = state.monster == null ? -1 : state.monster.coreHp;
        if (actual != assertion.expectedInt)
        {
            report.failures.Add($"assertion[{index}] monster core hp expected {assertion.expectedInt}, got {actual}");
        }
    }

    private static void EvaluateMonsterStatusEquals(ScenarioReport report, ScenarioAssertion assertion, int index)
    {
        if (!TryGetState(report, assertion.actualPath, index, out var state))
        {
            return;
        }

        var actual = state.monster == null ? 0 : GetStatusStacks(state.monster.statuses, assertion.statusId);
        if (actual != assertion.expectedInt)
        {
            report.failures.Add($"assertion[{index}] monster status {assertion.statusId} expected {assertion.expectedInt}, got {actual}");
        }
    }

    private static void EvaluatePartFieldEquals(ScenarioReport report, ScenarioAssertion assertion, int index)
    {
        if (!TryGetState(report, assertion.actualPath, index, out var state))
        {
            return;
        }

        var part = ResolvePart(state, assertion.partId);
        if (part == null)
        {
            report.failures.Add($"assertion[{index}] part not found: {assertion.partId}");
            return;
        }

        var actual = assertion.field switch
        {
            "hp" => part.hp,
            "isBroken" => part.isBroken ? 1 : 0,
            _ => int.MinValue
        };

        if (actual == int.MinValue)
        {
            report.failures.Add($"assertion[{index}] unsupported part field: {assertion.field}");
            return;
        }

        if (actual != assertion.expectedInt)
        {
            report.failures.Add($"assertion[{index}] part {assertion.partId} field {assertion.field} expected {assertion.expectedInt}, got {actual}");
        }
    }

    private static void EvaluatePartStatusEquals(ScenarioReport report, ScenarioAssertion assertion, int index)
    {
        report.failures.Add($"assertion[{index}] part_status_equals is deprecated. Use monster_status_equals.");
    }

    private static void EvaluatePhaseEquals(ScenarioReport report, ScenarioAssertion assertion, int index)
    {
        if (!TryGetState(report, assertion.actualPath, index, out var state))
        {
            return;
        }

        if (!Enum.TryParse(assertion.expected, true, out BattlePhase expected))
        {
            report.failures.Add($"assertion[{index}] invalid phase expected: {assertion.expected}");
            return;
        }

        if (state.phase != expected)
        {
            report.failures.Add($"assertion[{index}] phase expected {expected}, got {state.phase}");
        }
    }

    private static bool TryGetState(ScenarioReport report, string snapshotTag, int index, out BattleState state)
    {
        state = null!;
        var snap = VerifierInfra.FindSnapshot(report, snapshotTag);
        if (snap == null)
        {
            report.failures.Add($"assertion[{index}] snapshot not found: {snapshotTag}");
            return false;
        }

        state = JsonSerializer.Deserialize<BattleState>(snap.stateJson, VerifierInfra.JsonOptions())!;
        if (state == null)
        {
            report.failures.Add($"assertion[{index}] failed to deserialize snapshot: {snap.tag}");
            return false;
        }

        return true;
    }

    private static MonsterPartState? ResolvePart(BattleState state, string partId)
    {
        if (state?.monster?.parts == null)
        {
            return null;
        }

        if (string.Equals(partId, "$first_part", StringComparison.Ordinal))
        {
            return state.monster.parts.FirstOrDefault(p => p != null);
        }

        return state.monster.parts.FirstOrDefault(p => p != null && (p.instanceId == partId || p.partId == partId || p.displayName == partId));
    }

    private static int GetStatusStacks(List<BattleStatusState> statuses, string statusId)
    {
        if (statuses == null || string.IsNullOrWhiteSpace(statusId))
        {
            return 0;
        }

        for (var i = 0; i < statuses.Count; i++)
        {
            if (string.Equals(statuses[i].id, statusId, StringComparison.OrdinalIgnoreCase))
            {
                return statuses[i].stacks;
            }
        }

        return 0;
    }
}
