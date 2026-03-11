using System;
using System.Linq;

namespace YoungBob.Prototype.Testing
{
    public sealed class BattleScenarioRunner
    {
        private readonly IBattleTestDriver _driver;

        public BattleScenarioRunner(IBattleTestDriver driver)
        {
            _driver = driver;
        }

        public ScenarioExecutionReport Execute(BattleScenarioDefinition scenario)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            var report = new ScenarioExecutionReport
            {
                scenarioId = scenario.id,
                success = true
            };

            var setupResult = _driver.Setup(scenario.setup);
            RecordResult(report, "setup", setupResult);
            if (!setupResult.success)
            {
                report.success = false;
                return report;
            }

            var hostPlayerId = scenario.setup != null
                && scenario.setup.players != null
                && scenario.setup.players.Count > 0
                ? scenario.setup.players[0].playerId
                : string.Empty;

            var startResult = _driver.StartBattle(hostPlayerId);
            RecordResult(report, "start_battle", startResult);
            if (!startResult.success)
            {
                report.success = false;
                return report;
            }

            if (scenario.steps != null)
            {
                for (var i = 0; i < scenario.steps.Count; i++)
                {
                    var step = scenario.steps[i];
                    var stepResult = ExecuteStep(step);
                    RecordResult(report, "step_" + i + "_" + step.action, stepResult);
                    if (!stepResult.success && !IsExpectedFailure(step, stepResult))
                    {
                        report.failures.Add("step_" + i + "_" + step.action + " failed unexpectedly: " + (stepResult.error ?? "unknown"));
                        report.success = false;
                        return report;
                    }
                }
            }

            EvaluateAssertions(report, scenario);
            report.success = report.failures.Count == 0;
            return report;
        }

        private DriverActionResult ExecuteStep(ScenarioStep step)
        {
            if (step == null)
            {
                return new DriverActionResult { success = false, error = "Scenario step is null." };
            }

            switch (step.action)
            {
                case "play_card":
                    return ApplySnapshotTagOverride(
                        _driver.PlayCard(step.actorPlayerId, step.cardInstanceId, step.targetFaction, step.targetUnitId, step.targetArea),
                        step.snapshotTag);

                case "end_turn":
                    return ApplySnapshotTagOverride(_driver.EndTurn(step.actorPlayerId), step.snapshotTag);

                case "snapshot":
                    return _driver.Snapshot(string.IsNullOrWhiteSpace(step.snapshotTag) ? "snapshot" : step.snapshotTag);

                default:
                    return new DriverActionResult { success = false, error = "Unknown step action: " + step.action };
            }
        }

        private static DriverActionResult ApplySnapshotTagOverride(DriverActionResult result, string snapshotTag)
        {
            if (result == null || result.snapshot == null || string.IsNullOrWhiteSpace(snapshotTag))
            {
                return result;
            }

            result.snapshot.tag = snapshotTag;
            return result;
        }

        private static void RecordResult(ScenarioExecutionReport report, string stage, DriverActionResult result)
        {
            if (report == null || result == null)
            {
                return;
            }

            report.logs.Add(stage + ": " + (result.success ? "ok" : "failed"));
            if (!string.IsNullOrEmpty(result.error))
            {
                report.logs.Add(stage + " error: " + result.error);
            }

            if (result.snapshot != null)
            {
                report.snapshots.Add(result.snapshot);
            }
        }

        private static void EvaluateAssertions(ScenarioExecutionReport report, BattleScenarioDefinition scenario)
        {
            if (report == null || scenario == null || scenario.assertions == null)
            {
                return;
            }

            for (var i = 0; i < scenario.assertions.Count; i++)
            {
                var assertion = scenario.assertions[i];
                if (assertion == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(assertion.type))
                {
                    report.failures.Add("assertion[" + i + "] has empty type");
                    continue;
                }

                switch (assertion.type)
                {
                    case "todo":
                        report.logs.Add("assertion[" + i + "] TODO: " + assertion.message);
                        break;

                    case "snapshot_contains":
                        EvaluateSnapshotContains(report, assertion, i, shouldContain: true);
                        break;

                    case "snapshot_not_contains":
                        EvaluateSnapshotContains(report, assertion, i, shouldContain: false);
                        break;

                    case "snapshot_hash_equals":
                        EvaluateSnapshotHashEquals(report, assertion, i);
                        break;

                    case "snapshot_error_contains":
                        EvaluateSnapshotErrorContains(report, assertion, i);
                        break;

                    default:
                        report.failures.Add("assertion[" + i + "] unknown type: " + assertion.type);
                        break;
                }
            }
        }

        private static void EvaluateSnapshotContains(ScenarioExecutionReport report, ScenarioAssertion assertion, int index, bool shouldContain)
        {
            var snapshot = FindSnapshotByTag(report, assertion.actualPath);
            if (snapshot == null)
            {
                report.failures.Add("assertion[" + index + "] snapshot not found: " + assertion.actualPath);
                return;
            }

            var contains = !string.IsNullOrEmpty(assertion.expected) && snapshot.stateJson.IndexOf(assertion.expected, StringComparison.Ordinal) >= 0;
            if (contains != shouldContain)
            {
                var verb = shouldContain ? "contain" : "not contain";
                report.failures.Add("assertion[" + index + "] expected snapshot '" + snapshot.tag + "' to " + verb + " text: " + assertion.expected);
            }
        }

        private static void EvaluateSnapshotHashEquals(ScenarioExecutionReport report, ScenarioAssertion assertion, int index)
        {
            if (string.IsNullOrWhiteSpace(assertion.actualPath))
            {
                report.failures.Add("assertion[" + index + "] snapshot_hash_equals requires actualPath 'tagA|tagB'.");
                return;
            }

            var split = assertion.actualPath.Split('|');
            if (split.Length != 2)
            {
                report.failures.Add("assertion[" + index + "] snapshot_hash_equals requires actualPath format 'tagA|tagB'.");
                return;
            }

            var left = FindSnapshotByTag(report, split[0].Trim());
            var right = FindSnapshotByTag(report, split[1].Trim());
            if (left == null || right == null)
            {
                report.failures.Add("assertion[" + index + "] missing snapshot tag in hash compare.");
                return;
            }

            if (!string.Equals(left.stateHash, right.stateHash, StringComparison.Ordinal))
            {
                report.failures.Add("assertion[" + index + "] snapshot hash mismatch: '" + left.tag + "' vs '" + right.tag + "'.");
            }
        }

        private static DriverSnapshot FindSnapshotByTag(ScenarioExecutionReport report, string tag)
        {
            if (report == null || report.snapshots == null || report.snapshots.Count == 0)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(tag))
            {
                return report.snapshots[report.snapshots.Count - 1];
            }

            return report.snapshots.LastOrDefault(item => string.Equals(item.tag, tag, StringComparison.Ordinal));
        }

        private static void EvaluateSnapshotErrorContains(ScenarioExecutionReport report, ScenarioAssertion assertion, int index)
        {
            var snapshot = FindSnapshotByTag(report, assertion.actualPath);
            if (snapshot == null)
            {
                report.failures.Add("assertion[" + index + "] snapshot not found: " + assertion.actualPath);
                return;
            }

            var expected = assertion.expected ?? string.Empty;
            var actual = snapshot.error ?? string.Empty;
            if (actual.IndexOf(expected, StringComparison.Ordinal) < 0)
            {
                report.failures.Add("assertion[" + index + "] expected snapshot '" + snapshot.tag + "' error to contain: " + expected);
            }
        }

        private static bool IsExpectedFailure(ScenarioStep step, DriverActionResult result)
        {
            if (step == null || result == null || result.success || !step.allowFailure)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(step.expectedErrorContains))
            {
                return true;
            }

            return !string.IsNullOrEmpty(result.error)
                && result.error.IndexOf(step.expectedErrorContains, StringComparison.Ordinal) >= 0;
        }
    }
}
