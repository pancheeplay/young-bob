using System;
using System.IO;
using UnityEngine;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.Data
{
    internal static class UnityGameDataRepositoryLoader
    {
        private const string CardsResourcePath = "GameData/cards";
        private const string EncountersResourcePath = "GameData/encounters";
        private const string DecksResourcePath = "GameData/decks";
        private const string MonstersResourcePath = "GameData/monsters";
        private const string StagesResourcePath = "GameData/stages";

        public static GameDataRepository LoadFromResources()
        {
            var cards = LoadCatalog<CardCatalog>(CardsResourcePath);
            var encounters = LoadCatalog<EncounterCatalog>(EncountersResourcePath);
            var decks = LoadCatalog<DeckCatalog>(DecksResourcePath);
            var monsters = LoadCatalog<MonsterCatalog>(MonstersResourcePath);
            var stages = LoadCatalog<StageCatalog>(StagesResourcePath);

            return GameDataRepository.Create(
                cards == null ? null : cards.cards,
                encounters == null ? null : encounters.encounters,
                decks == null ? null : decks.decks,
                monsters == null ? null : monsters.monsters,
                stages == null ? null : stages.stages);
        }

        private static TCatalog LoadCatalog<TCatalog>(string resourcePath)
        {
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null)
            {
                throw new FileNotFoundException("Missing game data file in Resources/" + resourcePath, resourcePath);
            }

            var catalog = JsonUtility.FromJson<TCatalog>(textAsset.text);
            if (catalog == null)
            {
                throw new InvalidOperationException("Failed to parse catalog: " + resourcePath);
            }

            return catalog;
        }
    }
}
