using System;
using UnityEngine;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Testing
{
    public static class BattleScenarioExecutionService
    {
        public static ScenarioExecutionReport RunScenarioFromResources(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                throw new ArgumentException("Scenario resource path is empty.", nameof(resourcePath));
            }

            var scenario = BattleScenarioLoader.LoadSingleFromResources(resourcePath);
            var dataRepository = UnityGameDataRepositoryLoader.LoadFromResources();
            var driver = new InProcessBattleTestDriver(dataRepository);
            var runner = new BattleScenarioRunner(driver);
            return runner.Execute(scenario);
        }

        public static string RunScenarioFromResourcesAsJson(string resourcePath)
        {
            var report = RunScenarioFromResources(resourcePath);
            return JsonUtility.ToJson(report, true);
        }
    }
}
