using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class VerifierInfra
{
    public static JsonSerializerOptions JsonOptions(bool indented = false)
    {
        return new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            WriteIndented = indented
        };
    }

    public static string Sha256(string text)
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

    public static SnapshotRecord Snapshot(YoungBob.Prototype.Battle.BattleState state, string tag, string? error)
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

    public static SnapshotRecord? FindSnapshot(ScenarioReport report, string? tag)
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
}

internal static class CliParser
{
    public static CliOptions Parse(string[] args)
    {
        var repoRoot = Directory.GetCurrentDirectory();
        return new CliOptions
        {
            ScenarioArg = GetOption(args, "--scenario") ?? "all",
            ReportPath = GetOption(args, "--report") ?? "/tmp/young-bob-dotnet-verifier-report.json",
            DataRoot = Path.Combine(repoRoot, "Assets/Resources/GameData"),
            ScenarioRoot = Path.Combine(repoRoot, "Assets/Resources/TestScenarios")
        };
    }

    private static string? GetOption(string[] args, string key)
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
}

internal static class ScenarioLoader
{
    public static List<string> ResolveScenarioFiles(string scenarioRoot, string scenarioArg)
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

    public static ScenarioDefinition LoadScenario(string file)
    {
        var json = File.ReadAllText(file);
        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, VerifierInfra.JsonOptions())
            ?? throw new InvalidOperationException("Failed to parse scenario: " + file);

        ValidateScenario(scenario, file);
        return scenario;
    }

    private static void ValidateScenario(ScenarioDefinition scenario, string file)
    {
        if (scenario.setup == null)
        {
            throw new InvalidOperationException("Scenario setup missing: " + file);
        }

        if (scenario.setup.players == null || scenario.setup.players.Count == 0)
        {
            throw new InvalidOperationException("Scenario players missing: " + file);
        }

        if (string.IsNullOrWhiteSpace(scenario.setup.starterDeckId))
        {
            throw new InvalidOperationException("starterDeckId missing: " + file);
        }

        scenario.steps ??= new List<ScenarioStep>();
        scenario.assertions ??= new List<ScenarioAssertion>();
    }
}
