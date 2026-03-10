using System.Text.Json;
using YoungBob.Prototype.Battle;

namespace YoungBob.Tools.BattleCli;

public sealed class CliPlayer
{
    public string playerId = string.Empty;
    public string displayName = string.Empty;
}

public sealed class CliRoom
{
    public string roomId = string.Empty;
    public string hostPlayerId = string.Empty;
    public List<CliPlayer> players = new();
    public int messageSeq;
    public BattleState? battleState;
}

public sealed class CliLogEntry
{
    public DateTimeOffset timestamp;
    public string roomId = string.Empty;
    public string type = string.Empty;
    public string actorPlayerId = string.Empty;
    public string message = string.Empty;
}

public static class RoomStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static void EnsureRoot(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "rooms"));
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        Directory.CreateDirectory(Path.Combine(root, "locks"));
    }

    public static string RoomPath(string root, string roomId) => Path.Combine(root, "rooms", roomId + ".json");
    public static string LogPath(string root, string roomId) => Path.Combine(root, "logs", roomId + ".log");
    public static string LockPath(string root, string roomId) => Path.Combine(root, "locks", roomId + ".lock");

    public static T WithRoomLock<T>(string root, string roomId, Func<T> action)
    {
        var lockPath = LockPath(root, roomId);
        using var lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        return action();
    }

    public static CliRoom? LoadRoom(string root, string roomId)
    {
        var file = RoomPath(root, roomId);
        if (!File.Exists(file))
        {
            return null;
        }

        var json = File.ReadAllText(file);
        return JsonSerializer.Deserialize<CliRoom>(json, JsonOptions);
    }

    public static void SaveRoom(string root, CliRoom room)
    {
        var file = RoomPath(root, room.roomId);
        var json = JsonSerializer.Serialize(room, JsonOptions);
        File.WriteAllText(file, json);
    }

    public static void AppendLog(string root, CliLogEntry entry)
    {
        var file = LogPath(root, entry.roomId);
        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true
        });
        File.AppendAllText(file, json + Environment.NewLine);
    }

    public static IEnumerable<string> ListRooms(string root)
    {
        var roomDir = Path.Combine(root, "rooms");
        if (!Directory.Exists(roomDir))
        {
            yield break;
        }

        var files = Directory.GetFiles(roomDir, "*.json");
        for (var i = 0; i < files.Length; i++)
        {
            yield return Path.GetFileNameWithoutExtension(files[i]);
        }
    }
}
