using System.Text.Json;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;

internal static class JsonGameDataRepositoryLoader
{
    public static GameDataRepository LoadFromDirectory(string rootDirectory)
    {
        var cards = LoadCatalog<CardCatalog>(Path.Combine(rootDirectory, "cards.json"));
        var encounters = LoadCatalog<EncounterCatalog>(Path.Combine(rootDirectory, "encounters.json"));
        var decks = LoadCatalog<DeckCatalog>(Path.Combine(rootDirectory, "decks.json"));
        var monsters = LoadCatalog<MonsterCatalog>(Path.Combine(rootDirectory, "monsters.json"));
        var stages = LoadCatalog<StageCatalog>(Path.Combine(rootDirectory, "stages.json"));

        return GameDataRepository.Create(
            cards == null ? null : cards.cards,
            encounters == null ? null : encounters.encounters,
            decks == null ? null : decks.decks,
            monsters == null ? null : monsters.monsters,
            stages == null ? null : stages.stages);
    }

    private static TCatalog LoadCatalog<TCatalog>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Missing game data file", filePath);
        }

        var json = File.ReadAllText(filePath);
        var catalog = JsonSerializer.Deserialize<TCatalog>(json, JsonOptions());
        if (catalog == null)
        {
            throw new InvalidOperationException("Failed to parse catalog: " + filePath);
        }

        return catalog;
    }

    private static JsonSerializerOptions JsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true
        };
    }
}
