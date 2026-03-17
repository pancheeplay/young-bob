using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.Data
{
    [Serializable]
    public sealed class CardEffectDefinition
    {
        public string op;
        public string target;
        public string statusId;
        public string scaleBy;
        public string pileFrom;
        public int amount;
        public int amount2;
        public float ratio = 1f;
    }

    [Serializable]
    public sealed class CardDefinition
    {
        public string id;
        public string name;
        public string classTag;
        public string targetType;
        public string rangeHeights;
        public string rangeDistance;
        public string rangeZones;
        public int energyCost;
        public string[] tags;
        public CardEffectDefinition[] effects;
    }

    [Serializable]
    public sealed class EncounterDefinition
    {
        public string id;
        public string monsterId;
    }

    [Serializable]
    public sealed class StageDefinition
    {
        public string id;
        public string name;
        public string[] encounterIds;
    }

    [Serializable]
    public sealed class DeckDefinition
    {
        public string id;
        public string[] cards;
    }

    [Serializable]
    internal sealed class CardCatalog
    {
        public CardDefinition[] cards;
    }

    [Serializable]
    internal sealed class EncounterCatalog
    {
        public EncounterDefinition[] encounters;
    }

    [Serializable]
    internal sealed class DeckCatalog
    {
        public DeckDefinition[] decks;
    }

    [Serializable]
    internal sealed class MonsterCatalog
    {
        public MonsterDefinition[] monsters;
    }

    [Serializable]
    internal sealed class StageCatalog
    {
        public StageDefinition[] stages;
    }

    public sealed class GameDataRepository
    {
        private const string CardsResourcePath = "GameData/cards";
        private const string EncountersResourcePath = "GameData/encounters";
        private const string DecksResourcePath = "GameData/decks";
        private const string MonstersResourcePath = "GameData/monsters";
        private const string StagesResourcePath = "GameData/stages";

        private readonly Dictionary<string, CardDefinition> _cards;
        private readonly Dictionary<string, EncounterDefinition> _encounters;
        private readonly Dictionary<string, DeckDefinition> _decks;
        private readonly Dictionary<string, MonsterDefinition> _monsters;
        private readonly List<StageDefinition> _stages;
        private readonly Dictionary<string, StageDefinition> _stagesById;

        private GameDataRepository(
            Dictionary<string, CardDefinition> cards,
            Dictionary<string, EncounterDefinition> encounters,
            Dictionary<string, DeckDefinition> decks,
            Dictionary<string, MonsterDefinition> monsters,
            List<StageDefinition> stages,
            Dictionary<string, StageDefinition> stagesById)
        {
            _cards = cards;
            _encounters = encounters;
            _decks = decks;
            _monsters = monsters;
            _stages = stages;
            _stagesById = stagesById;
        }

        public static GameDataRepository LoadFromResources()
        {
            var cards = LoadCatalog<CardCatalog, CardDefinition>(
                CardsResourcePath,
                catalog => catalog.cards,
                item => item.id);
            var encounters = LoadCatalog<EncounterCatalog, EncounterDefinition>(
                EncountersResourcePath,
                catalog => catalog.encounters,
                item => item.id);
            var decks = LoadCatalog<DeckCatalog, DeckDefinition>(
                DecksResourcePath,
                catalog => catalog.decks,
                item => item.id);
            var monsters = LoadCatalog<MonsterCatalog, MonsterDefinition>(
                MonstersResourcePath,
                catalog => catalog.monsters,
                item => item.monsterId);
            var stages = LoadArrayCatalog<StageCatalog, StageDefinition>(
                StagesResourcePath,
                catalog => catalog.stages);

            ValidateEncounterMonsterReferences(encounters, monsters);
            var stagesById = BuildStageLookup(stages);
            ValidateStageEncounters(stages, encounters);

            return new GameDataRepository(cards, encounters, decks, monsters, stages, stagesById);
        }

        public CardDefinition GetCard(string id)
        {
            if (!_cards.TryGetValue(id, out var result))
            {
                throw new InvalidOperationException("Unknown card: " + id);
            }

            return result;
        }

        public EncounterDefinition GetEncounter(string id)
        {
            if (!_encounters.TryGetValue(id, out var result))
            {
                throw new InvalidOperationException("Unknown encounter: " + id);
            }

            return result;
        }

        public MonsterDefinition GetMonster(string id)
        {
            if (!_monsters.TryGetValue(id, out var result))
            {
                throw new InvalidOperationException("Unknown monster: " + id);
            }

            return result;
        }

        public MonsterDefinition GetEncounterMonster(string encounterId)
        {
            var encounter = GetEncounter(encounterId);
            if (string.IsNullOrWhiteSpace(encounter.monsterId))
            {
                throw new InvalidOperationException("Encounter missing monsterId: " + encounterId);
            }

            return GetMonster(encounter.monsterId);
        }

        public StageDefinition GetStage(string id)
        {
            if (!_stagesById.TryGetValue(id, out var result))
            {
                throw new InvalidOperationException("Unknown stage: " + id);
            }

            return result;
        }

        public IReadOnlyList<StageDefinition> GetAllStages()
        {
            return _stages;
        }

        public DeckDefinition GetDeck(string id)
        {
            if (!_decks.TryGetValue(id, out var result))
            {
                throw new InvalidOperationException("Unknown deck: " + id);
            }

            return result;
        }

        private static Dictionary<string, TItem> LoadCatalog<TCatalog, TItem>(
            string resourcePath,
            Func<TCatalog, TItem[]> selector,
            Func<TItem, string> keySelector)
        {
            var json = LoadJson(resourcePath);
            var catalog = JsonUtility.FromJson<TCatalog>(json);
            var items = selector(catalog) ?? Array.Empty<TItem>();
            var result = new Dictionary<string, TItem>(items.Length);
            foreach (var item in items)
            {
                var key = keySelector(item);
                result[key] = item;
            }

            return result;
        }

        private static List<TItem> LoadArrayCatalog<TCatalog, TItem>(
            string resourcePath,
            Func<TCatalog, TItem[]> selector)
        {
            var json = LoadJson(resourcePath);
            var catalog = JsonUtility.FromJson<TCatalog>(json);
            var items = selector(catalog) ?? Array.Empty<TItem>();
            return new List<TItem>(items);
        }

        private static void ValidateEncounterMonsterReferences(
            Dictionary<string, EncounterDefinition> encounters,
            Dictionary<string, MonsterDefinition> monsters)
        {
            foreach (var pair in encounters)
            {
                var encounter = pair.Value;
                if (encounter == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(encounter.monsterId))
                {
                    throw new InvalidOperationException("Encounter missing monsterId: " + pair.Key);
                }

                if (!monsters.ContainsKey(encounter.monsterId))
                {
                    throw new InvalidOperationException("Encounter references unknown monsterId: " + encounter.monsterId);
                }
            }
        }

        private static Dictionary<string, StageDefinition> BuildStageLookup(List<StageDefinition> stages)
        {
            var lookup = new Dictionary<string, StageDefinition>();
            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (stage == null || string.IsNullOrWhiteSpace(stage.id))
                {
                    continue;
                }

                lookup[stage.id] = stage;
            }

            return lookup;
        }

        private static void ValidateStageEncounters(
            List<StageDefinition> stages,
            Dictionary<string, EncounterDefinition> encounters)
        {
            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (stage == null)
                {
                    continue;
                }

                var encounterIds = stage.encounterIds ?? Array.Empty<string>();
                if (encounterIds.Length == 0)
                {
                    throw new InvalidOperationException("Stage has no encounters: " + stage.id);
                }

                for (var encounterIndex = 0; encounterIndex < encounterIds.Length; encounterIndex++)
                {
                    var encounterId = encounterIds[encounterIndex];
                    if (!encounters.ContainsKey(encounterId))
                    {
                        throw new InvalidOperationException("Stage references unknown encounterId: " + encounterId + " (stage=" + stage.id + ")");
                    }
                }
            }
        }

        private static string LoadJson(string resourcePath)
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset != null)
            {
                return textAsset.text;
            }

            throw new FileNotFoundException(
                "Missing game data file in Resources/" + resourcePath,
                resourcePath);
        }
    }
}
