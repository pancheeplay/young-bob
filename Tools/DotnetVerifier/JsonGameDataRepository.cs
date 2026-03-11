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
    public string monsterId = string.Empty;
}

public sealed class StageDefinition
{
    public string id = string.Empty;
    public string name = string.Empty;
    public string[] encounterIds = Array.Empty<string>();
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

internal sealed class MonsterCatalog
{
    public YoungBob.Prototype.Battle.MonsterDefinition[] monsters = Array.Empty<YoungBob.Prototype.Battle.MonsterDefinition>();
}

internal sealed class StageCatalog
{
    public StageDefinition[] stages = Array.Empty<StageDefinition>();
}

public sealed class GameDataRepository
{
    private readonly Dictionary<string, CardDefinition> _cards;
    private readonly Dictionary<string, EncounterDefinition> _encounters;
    private readonly Dictionary<string, DeckDefinition> _decks;
    private readonly Dictionary<string, YoungBob.Prototype.Battle.MonsterDefinition> _monsters;
    private readonly List<StageDefinition> _stages;
    private readonly Dictionary<string, StageDefinition> _stagesById;

    private GameDataRepository(
        Dictionary<string, CardDefinition> cards,
        Dictionary<string, EncounterDefinition> encounters,
        Dictionary<string, DeckDefinition> decks,
        Dictionary<string, YoungBob.Prototype.Battle.MonsterDefinition> monsters,
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
        var monsters = LoadCatalog<MonsterCatalog, YoungBob.Prototype.Battle.MonsterDefinition>(
            Path.Combine(rootDirectory, "monsters.json"),
            catalog => catalog.monsters,
            item => item.monsterId);
        var stages = LoadArrayCatalog<StageCatalog, StageDefinition>(
            Path.Combine(rootDirectory, "stages.json"),
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

    public DeckDefinition GetDeck(string id)
    {
        if (!_decks.TryGetValue(id, out var result))
        {
            throw new InvalidOperationException("Unknown deck: " + id);
        }

        return result;
    }

    public YoungBob.Prototype.Battle.MonsterDefinition GetMonster(string id)
    {
        if (!_monsters.TryGetValue(id, out var result))
        {
            throw new InvalidOperationException("Unknown monster: " + id);
        }

        return result;
    }

    public StageDefinition GetStage(string id)
    {
        if (!_stagesById.TryGetValue(id, out var result))
        {
            throw new InvalidOperationException("Unknown stage: " + id);
        }

        return result;
    }

    public YoungBob.Prototype.Battle.MonsterDefinition GetEncounterMonster(string encounterId)
    {
        var encounter = GetEncounter(encounterId);
        if (string.IsNullOrWhiteSpace(encounter.monsterId))
        {
            throw new InvalidOperationException("Encounter missing monsterId: " + encounterId);
        }

        return GetMonster(encounter.monsterId);
    }

    public IReadOnlyList<StageDefinition> GetAllStages()
    {
        return _stages;
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

    private static List<TItem> LoadArrayCatalog<TCatalog, TItem>(
        string filePath,
        Func<TCatalog, TItem[]> selector)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Missing game data file", filePath);
        }

        var json = File.ReadAllText(filePath);
        var catalog = JsonSerializer.Deserialize<TCatalog>(json, JsonOptions())
            ?? throw new InvalidOperationException("Failed to parse catalog: " + filePath);
        var items = selector(catalog) ?? Array.Empty<TItem>();
        return new List<TItem>(items);
    }

    private static void ValidateEncounterMonsterReferences(
        Dictionary<string, EncounterDefinition> encounters,
        Dictionary<string, YoungBob.Prototype.Battle.MonsterDefinition> monsters)
    {
        foreach (var pair in encounters)
        {
            var encounter = pair.Value;
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
        var lookup = new Dictionary<string, StageDefinition>(StringComparer.Ordinal);
        for (var i = 0; i < stages.Count; i++)
        {
            var stage = stages[i];
            if (string.IsNullOrWhiteSpace(stage.id))
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
            var encounterIds = stage.encounterIds ?? Array.Empty<string>();
            if (encounterIds.Length == 0)
            {
                throw new InvalidOperationException("Stage has no encounters: " + stage.id);
            }

            for (var encounterIndex = 0; encounterIndex < encounterIds.Length; encounterIndex++)
            {
                if (!encounters.ContainsKey(encounterIds[encounterIndex]))
                {
                    throw new InvalidOperationException("Stage references unknown encounterId: " + encounterIds[encounterIndex] + " (stage=" + stage.id + ")");
                }
            }
        }
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
