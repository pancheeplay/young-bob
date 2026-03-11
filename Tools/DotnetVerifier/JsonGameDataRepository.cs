using System.Text.Json;

namespace YoungBob.Prototype.Data;

public sealed class CardDefinition
{
    public string id = string.Empty;
    public string name = string.Empty;
    public string classTag = string.Empty;
    public string effectType = string.Empty;
    public string targetType = string.Empty;
    public string rangeHeights = string.Empty;
    public string rangeDistance = string.Empty;
    public string rangeZones = string.Empty;
    public int energyCost;
    public int value;
}

public sealed class EncounterDefinition
{
    public string id = string.Empty;
    public YoungBob.Prototype.Battle.MonsterDefinition? monster;
}

public sealed class DeckDefinition
{
    public string id = string.Empty;
    public string[] cards = Array.Empty<string>();
}

internal sealed class CardCatalog
{
    public CardDefinition[] cards = Array.Empty<CardDefinition>();
}

internal sealed class EncounterCatalog
{
    public EncounterDefinition[] encounters = Array.Empty<EncounterDefinition>();
}

internal sealed class DeckCatalog
{
    public DeckDefinition[] decks = Array.Empty<DeckDefinition>();
}

public sealed class GameDataRepository
{
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

    public static GameDataRepository LoadFromDirectory(string rootDirectory)
    {
        var cards = LoadCatalog<CardCatalog, CardDefinition>(
            Path.Combine(rootDirectory, "cards.json"),
            catalog => catalog.cards,
            item => item.id);
        var encounters = LoadCatalog<EncounterCatalog, EncounterDefinition>(
            Path.Combine(rootDirectory, "encounters.json"),
            catalog => catalog.encounters,
            item => item.id);
        var decks = LoadCatalog<DeckCatalog, DeckDefinition>(
            Path.Combine(rootDirectory, "decks.json"),
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
        string filePath,
        Func<TCatalog, TItem[]> selector,
        Func<TItem, string> keySelector)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Missing game data file", filePath);
        }

        var json = File.ReadAllText(filePath);
        var catalog = JsonSerializer.Deserialize<TCatalog>(json, JsonOptions())
            ?? throw new InvalidOperationException("Failed to parse catalog: " + filePath);

        var items = selector(catalog) ?? Array.Empty<TItem>();
        var result = new Dictionary<string, TItem>(items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            var key = keySelector(items[i]);
            result[key] = items[i];
        }

        return result;
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
