using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.Data
{
    [Serializable]
    public sealed class CardDefinition
    {
        public string id;
        public string name;
        public string classTag;
        public string effectType;
        public string targetType;
        public string rangeHeights;
        public string rangeDistance;
        public string rangeZones;
        public int energyCost;
        public int value;
    }

    [Serializable]
    public sealed class EncounterDefinition
    {
        public string id;
        public MonsterDefinition monster;
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

    public sealed class GameDataRepository
    {
        private const string CardsResourcePath = "GameData/cards";
        private const string EncountersResourcePath = "GameData/encounters";
        private const string DecksResourcePath = "GameData/decks";

        private readonly Dictionary<string, CardDefinition> _cards;
        private readonly Dictionary<string, EncounterDefinition> _encounters;
        private readonly Dictionary<string, DeckDefinition> _decks;

        private GameDataRepository(
            Dictionary<string, CardDefinition> cards,
            Dictionary<string, EncounterDefinition> encounters,
            Dictionary<string, DeckDefinition> decks)
        {
            _cards = cards;
            _encounters = encounters;
            _decks = decks;
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

            return new GameDataRepository(cards, encounters, decks);
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
