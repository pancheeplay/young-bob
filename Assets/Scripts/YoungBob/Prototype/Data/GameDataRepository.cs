using System;
using System.Collections.Generic;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.Data
{
    [Serializable]
    public sealed class CardEffectDefinition
    {
        public string op;
        public string target;
        public string destinationId;
        public string statusId;
        public BattleStatusDurationKind durationKind;
        public int durationTurns;
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
        public string effectsSExpr;
        [NonSerialized] public SExpressionNode parsedEffects;
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
        public CardDefinition[] cards = Array.Empty<CardDefinition>();
    }

    [Serializable]
    internal sealed class EncounterCatalog
    {
        public EncounterDefinition[] encounters = Array.Empty<EncounterDefinition>();
    }

    [Serializable]
    internal sealed class DeckCatalog
    {
        public DeckDefinition[] decks = Array.Empty<DeckDefinition>();
    }

    [Serializable]
    internal sealed class MonsterCatalog
    {
        public MonsterDefinition[] monsters = Array.Empty<MonsterDefinition>();
    }

    [Serializable]
    internal sealed class StageCatalog
    {
        public StageDefinition[] stages = Array.Empty<StageDefinition>();
    }

    public sealed class GameDataRepository
    {
        private readonly Dictionary<string, CardDefinition> _cards;
        private readonly Dictionary<string, EncounterDefinition> _encounters;
        private readonly Dictionary<string, DeckDefinition> _decks;
        private readonly List<DeckDefinition> _deckList;
        private readonly Dictionary<string, MonsterDefinition> _monsters;
        private readonly List<StageDefinition> _stages;
        private readonly Dictionary<string, StageDefinition> _stagesById;

        private GameDataRepository(
            Dictionary<string, CardDefinition> cards,
            Dictionary<string, EncounterDefinition> encounters,
            Dictionary<string, DeckDefinition> decks,
            List<DeckDefinition> deckList,
            Dictionary<string, MonsterDefinition> monsters,
            List<StageDefinition> stages,
            Dictionary<string, StageDefinition> stagesById)
        {
            _cards = cards;
            _encounters = encounters;
            _decks = decks;
            _deckList = deckList;
            _monsters = monsters;
            _stages = stages;
            _stagesById = stagesById;
        }

        internal static GameDataRepository Create(
            CardDefinition[] cards,
            EncounterDefinition[] encounters,
            DeckDefinition[] decks,
            MonsterDefinition[] monsters,
            StageDefinition[] stages)
        {
            var cardLookup = BuildLookup(cards, item => item.id);
            InitializeCards(cardLookup);

            var encounterLookup = BuildLookup(encounters, item => item.id);
            var deckLookup = BuildLookup(decks, item => item.id);
            var deckList = new List<DeckDefinition>(decks ?? Array.Empty<DeckDefinition>());
            var monsterLookup = BuildLookup(monsters, item => item.monsterId);
            var stageList = new List<StageDefinition>(stages ?? Array.Empty<StageDefinition>());

            ValidateEncounterMonsterReferences(encounterLookup, monsterLookup);
            var stagesById = BuildStageLookup(stageList);
            ValidateStageEncounters(stageList, encounterLookup);

            return new GameDataRepository(
                cardLookup,
                encounterLookup,
                deckLookup,
                deckList,
                monsterLookup,
                stageList,
                stagesById);
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

        public IReadOnlyList<DeckDefinition> GetAllDecks()
        {
            return _deckList;
        }

        private static Dictionary<string, TItem> BuildLookup<TItem>(TItem[] items, Func<TItem, string> keySelector)
        {
            var result = new Dictionary<string, TItem>();
            foreach (var item in items ?? Array.Empty<TItem>())
            {
                if (item == null)
                {
                    continue;
                }

                var key = keySelector(item);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                result[key] = item;
            }

            return result;
        }

        private static void InitializeCards(Dictionary<string, CardDefinition> cards)
        {
            foreach (var pair in cards)
            {
                var card = pair.Value;
                if (card == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(card.effectsSExpr))
                {
                    throw new InvalidOperationException("Card missing effectsSExpr: " + pair.Key);
                }

                card.parsedEffects = CardEffectCompiler.Compile(card.effectsSExpr);
                CardEffectDslValidator.Validate(card.parsedEffects);
            }
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
    }
}
