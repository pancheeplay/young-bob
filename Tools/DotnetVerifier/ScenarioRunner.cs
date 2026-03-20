using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;

internal sealed class ScenarioRunner
{
    private readonly StepExecutor _stepExecutor = new();

    public ScenarioReport Execute(GameDataRepository repo, ScenarioDefinition scenario)
    {
        var report = new ScenarioReport
        {
            scenarioId = scenario.id,
            success = true
        };

        try
        {
            var engine = new BattleEngine(repo);
            var setup = new BattleSetupDefinition
            {
                roomId = scenario.setup.roomId,
                stageId = scenario.setup.stageId,
                encounterId = scenario.setup.encounterId,
                monsterId = scenario.setup.monsterId,
                starterDeckId = scenario.setup.starterDeckId,
                randomSeed = scenario.setup.randomSeed
            };

            for (var i = 0; i < scenario.setup.players.Count; i++)
            {
                setup.players.Add(new BattleParticipantDefinition
                {
                    playerId = scenario.setup.players[i].playerId,
                    displayName = scenario.setup.players[i].displayName
                });
            }

            var state = engine.CreateInitialState(setup);
            report.snapshots.Add(VerifierInfra.Snapshot(state, "setup", null));

            for (var i = 0; i < scenario.steps.Count; i++)
            {
                var step = scenario.steps[i];
                var tag = string.IsNullOrWhiteSpace(step.snapshotTag) ? $"step_{i}_{step.action}" : step.snapshotTag;
                var result = _stepExecutor.Execute(engine, state, step);
                report.snapshots.Add(VerifierInfra.Snapshot(state, tag, result.error));
                report.steps.Add(new StepTrace
                {
                    index = i,
                    action = step.action,
                    tag = tag,
                    success = result.success,
                    error = result.error ?? string.Empty,
                    events = result.events ?? new List<BattleEvent>()
                });

                var expectedFailure = !result.success && step.allowFailure
                    && (string.IsNullOrWhiteSpace(step.expectedErrorContains)
                        || (result.error ?? string.Empty).Contains(step.expectedErrorContains, StringComparison.Ordinal));

                if (!result.success && !expectedFailure)
                {
                    report.failures.Add($"step[{i}] {step.action} failed unexpectedly: {result.error}");
                    report.success = false;
                    return report;
                }

                if (result.success && step.allowFailure)
                {
                    report.failures.Add($"step[{i}] {step.action} expected failure but succeeded");
                    report.success = false;
                    return report;
                }
            }

            AssertionEngine.EvaluateAssertions(report, scenario.assertions);
            report.success = report.failures.Count == 0;
            return report;
        }
        catch (Exception ex)
        {
            report.success = false;
            report.failures.Add("exception: " + ex.Message);
            return report;
        }
    }
}
