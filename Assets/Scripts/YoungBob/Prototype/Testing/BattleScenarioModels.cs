using System;
using System.Collections.Generic;
using UnityEngine;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.Testing
{
    [Serializable]
    public sealed class BattleScenarioCollection
    {
        public BattleScenarioDefinition[] scenarios;
    }

    [Serializable]
    public sealed class BattleScenarioDefinition
    {
        public string id;
        public string name;
        public DriverSetupOptions setup;
        public List<ScenarioStep> steps = new List<ScenarioStep>();
        public List<ScenarioAssertion> assertions = new List<ScenarioAssertion>();
    }

    [Serializable]
    public sealed class ScenarioStep
    {
        public string action;
        public string actorPlayerId;
        public string cardInstanceId;
        public int debugValue;
        public BattleTargetFaction targetFaction;
        public string targetUnitId;
        public BattleArea targetArea;
        public string snapshotTag;
        public bool allowFailure;
        public string expectedErrorContains;
    }

    [Serializable]
    public sealed class ScenarioAssertion
    {
        public string type;
        public string message;
        public string expected;
        public string actualPath;
        public bool shouldMatch;
    }

    [Serializable]
    public sealed class ScenarioExecutionReport
    {
        public string scenarioId;
        public bool success;
        public List<string> logs = new List<string>();
        public List<DriverSnapshot> snapshots = new List<DriverSnapshot>();
        public List<string> failures = new List<string>();
    }

    public static class BattleScenarioLoader
    {
        public static BattleScenarioDefinition LoadSingleFromResources(string resourcePath)
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                throw new InvalidOperationException("Scenario resource not found: " + resourcePath);
            }

            var scenario = JsonUtility.FromJson<BattleScenarioDefinition>(textAsset.text);
            if (scenario == null)
            {
                throw new InvalidOperationException("Failed to parse scenario: " + resourcePath);
            }

            return scenario;
        }
    }
}
