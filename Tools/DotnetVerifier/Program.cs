using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;

var exitCode = Run(args);
return exitCode;

static int Run(string[] args)
{
    var repoRoot = Directory.GetCurrentDirectory();
    var dataRoot = Path.Combine(repoRoot, "Assets/Resources/GameData");
    var scenarioRoot = Path.Combine(repoRoot, "Assets/Resources/TestScenarios");

    var scenarioArg = GetOption(args, "--scenario") ?? "all";
    var reportPath = GetOption(args, "--report") ?? "/tmp/young-bob-dotnet-verifier-report.json";

    var scenarioFiles = ResolveScenarioFiles(scenarioRoot, scenarioArg);
    if (scenarioFiles.Count == 0)
    {
        Console.Error.WriteLine("No scenarios found for: " + scenarioArg);
        return 1;
    }

    var repo = GameDataRepository.LoadFromDirectory(dataRoot);
    var aggregate = new AggregateReport();

    foreach (var scenarioFile in scenarioFiles)
    {
        var scenarioJson = File.ReadAllText(scenarioFile);
        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(scenarioJson, JsonOptions())
            ?? throw new InvalidOperationException("Failed to parse scenario: " + scenarioFile);

        var report = ExecuteScenario(repo, scenario);
        aggregate.scenarioReports.Add(report);
        aggregate.success = aggregate.success && report.success;

        Console.WriteLine($"[{(report.success ? "PASS" : "FAIL")}] {report.scenarioId}");
        if (!report.success)
        {
            for (var i = 0; i < report.failures.Count; i++)
            {
                Console.WriteLine("  - " + report.failures[i]);
            }
        }
    }

    var aggregateJson = JsonSerializer.Serialize(aggregate, JsonOptionsIndented());
    Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? "/tmp");
    File.WriteAllText(reportPath, aggregateJson);
    Console.WriteLine("Report: " + reportPath);

    return aggregate.success ? 0 : 2;
}

static ScenarioReport ExecuteScenario(GameDataRepository repo, ScenarioDefinition scenario)
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
        report.snapshots.Add(Snapshot(state, "setup", null));

        for (var i = 0; i < scenario.steps.Count; i++)
        {
            var step = scenario.steps[i];
            var tag = string.IsNullOrWhiteSpace(step.snapshotTag) ? $"step_{i}_{step.action}" : step.snapshotTag;
            var result = ExecuteStep(engine, state, step);
            var snap = Snapshot(state, tag, result.error);
            report.snapshots.Add(snap);

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

        EvaluateAssertions(report, scenario.assertions);
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

static StepResult ExecuteStep(BattleEngine engine, BattleState state, ScenarioStep step)
{
    switch (step.action)
    {
        case "end_turn":
            return Apply(engine, state, new BattleCommand
            {
                commandId = Guid.NewGuid().ToString("N"),
                actorPlayerId = step.actorPlayerId,
                action = "end_turn"
            });

        case "play_card":
            return Apply(engine, state, new BattleCommand
            {
                commandId = Guid.NewGuid().ToString("N"),
                actorPlayerId = step.actorPlayerId,
                action = "play_card",
                cardInstanceId = ResolveCardInstanceId(state, step.actorPlayerId, step.cardInstanceId),
                targetFaction = step.targetFaction,
                targetUnitId = step.targetUnitId,
                targetArea = step.targetArea
            });

        case "snapshot":
            return new StepResult { success = true };

        case "debug_damage_monster":
            if (state.monster == null)
            {
                return new StepResult { success = false, error = "Monster not found." };
            }

            var damage = Math.Max(0, step.debugValue);
            state.monster.coreHp = Math.Max(0, state.monster.coreHp - damage);
            for (var i = 0; i < state.monster.parts.Count; i++)
            {
                var part = state.monster.parts[i];
                part.hp = Math.Max(0, part.hp - damage);
                if (part.hp == 0)
                {
                    part.isBroken = true;
                }
            }

            return new StepResult { success = true };

        case "debug_set_player_hp":
            {
                var player = state.GetPlayer(step.targetUnitId);
                if (player == null)
                {
                    return new StepResult { success = false, error = "Player not found: " + step.targetUnitId };
                }

                player.hp = Math.Max(0, Math.Min(player.maxHp, step.debugValue));
                return new StepResult { success = true };
            }

        default:
            return new StepResult { success = false, error = "unknown action: " + step.action };
    }
}

static StepResult Apply(BattleEngine engine, BattleState state, BattleCommand command)
{
    var result = engine.Apply(state, command);
    return new StepResult
    {
        success = result.success,
        error = result.error
    };
}

static void EvaluateAssertions(ScenarioReport report, List<ScenarioAssertion> assertions)
{
    for (var i = 0; i < assertions.Count; i++)
    {
        var assertion = assertions[i];
        switch (assertion.type)
        {
            case "snapshot_contains":
                {
                    var snap = FindSnapshot(report, assertion.actualPath);
                    if (snap == null || !snap.stateJson.Contains(assertion.expected ?? string.Empty, StringComparison.Ordinal))
                    {
                        report.failures.Add($"assertion[{i}] snapshot_contains failed: {assertion.message}");
                    }
                    break;
                }
            case "snapshot_not_contains":
                {
                    var snap = FindSnapshot(report, assertion.actualPath);
                    if (snap == null || snap.stateJson.Contains(assertion.expected ?? string.Empty, StringComparison.Ordinal))
                    {
                        report.failures.Add($"assertion[{i}] snapshot_not_contains failed: {assertion.message}");
                    }
                    break;
                }
            case "snapshot_error_contains":
                {
                    var snap = FindSnapshot(report, assertion.actualPath);
                    if (snap == null || !(snap.error ?? string.Empty).Contains(assertion.expected ?? string.Empty, StringComparison.Ordinal))
                    {
                        report.failures.Add($"assertion[{i}] snapshot_error_contains failed: {assertion.message}");
                    }
                    break;
                }
            case "snapshot_hash_equals":
                {
                    var split = (assertion.actualPath ?? string.Empty).Split('|');
                    if (split.Length != 2)
                    {
                        report.failures.Add($"assertion[{i}] invalid hash compare path");
                        break;
                    }

                    var left = FindSnapshot(report, split[0].Trim());
                    var right = FindSnapshot(report, split[1].Trim());
                    if (left == null || right == null || !string.Equals(left.stateHash, right.stateHash, StringComparison.Ordinal))
                    {
                        report.failures.Add($"assertion[{i}] snapshot_hash_equals failed: {assertion.message}");
                    }
                    break;
                }
            case "todo":
                break;
            default:
                report.failures.Add($"assertion[{i}] unknown type: {assertion.type}");
                break;
        }
    }
}

static SnapshotRecord? FindSnapshot(ScenarioReport report, string? tag)
{
    if (report.snapshots.Count == 0)
    {
        return null;
    }

    if (string.IsNullOrWhiteSpace(tag))
    {
        return report.snapshots[report.snapshots.Count - 1];
    }

    for (var i = report.snapshots.Count - 1; i >= 0; i--)
    {
        if (string.Equals(report.snapshots[i].tag, tag, StringComparison.Ordinal))
        {
            return report.snapshots[i];
        }
    }

    return null;
}

static SnapshotRecord Snapshot(BattleState state, string tag, string? error)
{
    var json = JsonSerializer.Serialize(state, JsonOptions());
    return new SnapshotRecord
    {
        tag = tag,
        stateJson = json,
        stateHash = Sha256(json),
        error = error ?? string.Empty
    };
}

static string ResolveCardInstanceId(BattleState state, string actorPlayerId, string raw)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return raw;
    }

    if (!raw.StartsWith("card:", StringComparison.Ordinal))
    {
        return raw;
    }

    var player = state.GetPlayer(actorPlayerId);
    if (player == null)
    {
        return raw;
    }

    var cardId = raw.Substring("card:".Length);
    var match = player.hand.FirstOrDefault(item => item.cardId == cardId);
    return match == null ? raw : match.instanceId;
}

static string Sha256(string text)
{
    using var sha = SHA256.Create();
    var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
    var sb = new StringBuilder(hash.Length * 2);
    for (var i = 0; i < hash.Length; i++)
    {
        sb.Append(hash[i].ToString("x2"));
    }

    return sb.ToString();
}

static List<string> ResolveScenarioFiles(string scenarioRoot, string scenarioArg)
{
    var result = new List<string>();
    if (string.Equals(scenarioArg, "all", StringComparison.OrdinalIgnoreCase))
    {
        result.AddRange(Directory.GetFiles(scenarioRoot, "*.json").OrderBy(x => x));
        return result;
    }

    var byId = Path.Combine(scenarioRoot, scenarioArg + ".json");
    if (File.Exists(byId))
    {
        result.Add(byId);
        return result;
    }

    if (File.Exists(scenarioArg))
    {
        result.Add(Path.GetFullPath(scenarioArg));
    }

    return result;
}

static string? GetOption(string[] args, string key)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static JsonSerializerOptions JsonOptions()
{
    return new JsonSerializerOptions
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true
    };
}

static JsonSerializerOptions JsonOptionsIndented()
{
    return new JsonSerializerOptions
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
}

public sealed class AggregateReport
{
    public bool success = true;
    public List<ScenarioReport> scenarioReports = new();
}

public sealed class ScenarioReport
{
    public string scenarioId = string.Empty;
    public bool success;
    public List<string> failures = new();
    public List<SnapshotRecord> snapshots = new();
}

public sealed class SnapshotRecord
{
    public string tag = string.Empty;
    public string stateJson = string.Empty;
    public string stateHash = string.Empty;
    public string error = string.Empty;
}

public sealed class StepResult
{
    public bool success;
    public string? error;
}

public sealed class ScenarioDefinition
{
    public string id = string.Empty;
    public string name = string.Empty;
    public ScenarioSetup setup = new();
    public List<ScenarioStep> steps = new();
    public List<ScenarioAssertion> assertions = new();
}

public sealed class ScenarioSetup
{
    public string roomId = string.Empty;
    public string stageId = string.Empty;
    public string encounterId = string.Empty;
    public string monsterId = string.Empty;
    public string starterDeckId = string.Empty;
    public int randomSeed;
    public int topology;
    public List<ScenarioPlayer> players = new();
}

public sealed class ScenarioPlayer
{
    public string playerId = string.Empty;
    public string displayName = string.Empty;
}

public sealed class ScenarioStep
{
    public string action = string.Empty;
    public string actorPlayerId = string.Empty;
    public string cardInstanceId = string.Empty;
    public int debugValue;
    public BattleTargetFaction targetFaction;
    public string targetUnitId = string.Empty;
    public BattleArea targetArea;
    public string snapshotTag = string.Empty;
    public bool allowFailure;
    public string expectedErrorContains = string.Empty;
}

public sealed class ScenarioAssertion
{
    public string type = string.Empty;
    public string message = string.Empty;
    public string expected = string.Empty;
    public string actualPath = string.Empty;
    public bool shouldMatch;
}
