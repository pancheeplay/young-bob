using System;
using System.IO;
using UnityEngine;

namespace YoungBob.Prototype.Testing
{
    public static class BattleScenarioCommandEntry
    {
        public static void Run()
        {
            RunFromArgs();
        }

        public static int RunFromArgs()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var scenario = GetArgValue(args, "--scenario") ?? "TestScenarios/curse_single";
                var reportPath = GetArgValue(args, "--report") ?? "/tmp/young-bob-scenario-report.json";

                var report = BattleScenarioExecutionService.RunScenarioFromResources(scenario);
                var json = JsonUtility.ToJson(report, true);
                var reportDirectory = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrWhiteSpace(reportDirectory))
                {
                    Directory.CreateDirectory(reportDirectory);
                }

                File.WriteAllText(reportPath, json);
                Debug.Log("[BattleScenarioCommandEntry] scenario=" + scenario + " report=" + reportPath + " success=" + report.success);
                return report.success ? 0 : 2;
            }
            catch (Exception ex)
            {
                Debug.LogError("[BattleScenarioCommandEntry] failed: " + ex);
                return 1;
            }
        }

        private static string GetArgValue(string[] args, string key)
        {
            if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

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
}
