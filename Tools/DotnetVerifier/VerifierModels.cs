using YoungBob.Prototype.Battle;

public sealed class CliOptions
{
    public string ScenarioArg { get; init; } = "all";
    public string ReportPath { get; init; } = "/tmp/young-bob-dotnet-verifier-report.json";
    public string DataRoot { get; init; } = string.Empty;
    public string ScenarioRoot { get; init; } = string.Empty;
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
    public List<StepTrace> steps = new();
}

public sealed class StepTrace
{
    public int index;
    public string action = string.Empty;
    public string tag = string.Empty;
    public bool success;
    public string error = string.Empty;
    public List<string> events = new();
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
    public List<string> events = new();
}

public sealed class ScenarioDefinition
{
    public int scenarioVersion = 2;
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

    // generic extensions
    public string cardId = string.Empty;
    public int amount;
    public string statusId = string.Empty;
}

public sealed class ScenarioAssertion
{
    public string type = string.Empty;
    public string message = string.Empty;
    public string expected = string.Empty;
    public string actualPath = string.Empty;
    public bool shouldMatch;

    // structured fields
    public string playerId = string.Empty;
    public string partId = string.Empty;
    public string cardId = string.Empty;
    public string statusId = string.Empty;
    public string field = string.Empty;
    public int expectedInt;
    public bool expectedBool;
}
