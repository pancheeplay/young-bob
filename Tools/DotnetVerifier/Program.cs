using System.Text.Json;
using YoungBob.Prototype.Data;

return Run(args);

static int Run(string[] args)
{
    var options = CliParser.Parse(args);
    var scenarioFiles = ScenarioLoader.ResolveScenarioFiles(options.ScenarioRoot, options.ScenarioArg);
    if (scenarioFiles.Count == 0)
    {
        Console.Error.WriteLine("No scenarios found for: " + options.ScenarioArg);
        return 1;
    }

    var repo = JsonGameDataRepositoryLoader.LoadFromDirectory(options.DataRoot);
    var runner = new ScenarioRunner();
    var aggregate = new AggregateReport();

    foreach (var scenarioFile in scenarioFiles)
    {
        var scenario = ScenarioLoader.LoadScenario(scenarioFile);
        var report = runner.Execute(repo, scenario);

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

    var aggregateJson = JsonSerializer.Serialize(aggregate, VerifierInfra.JsonOptions(indented: true));
    Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath) ?? "/tmp");
    File.WriteAllText(options.ReportPath, aggregateJson);
    Console.WriteLine("Report: " + options.ReportPath);

    return aggregate.success ? 0 : 2;
}
