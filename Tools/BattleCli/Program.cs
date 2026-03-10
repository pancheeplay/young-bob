using System.Text.Json;
using YoungBob.Prototype.Battle;
using YoungBob.Prototype.Data;
using YoungBob.Tools.BattleCli;

var exitCode = Run(args);
return exitCode;

static int Run(string[] args)
{
    var dataRoot = GetOption(args, "--data-root") ?? ResolveDefaultDataRoot();

    // Default to interactive mode when no args or explicit "interactive" command
    if (args.Length == 0 || string.Equals(args[0], "interactive", StringComparison.OrdinalIgnoreCase))
    {
        var repo = GameDataRepository.LoadFromDirectory(dataRoot);
        var shell = new YoungBob.Tools.BattleCli.InteractiveShell(repo);
        shell.Run();
        return 0;
    }

    var root = GetOption(args, "--root") ?? "/tmp/young-bob-cli";
    RoomStore.EnsureRoot(root);

    var domain = args[0].ToLowerInvariant();
    var command = args.Length > 1 ? args[1].ToLowerInvariant() : string.Empty;

    try
    {
        return domain switch
        {
            "room" => HandleRoom(root, command, args),
            "battle" => HandleBattle(root, dataRoot, command, args),
            "log" => HandleLog(root, command, args),
            "help" => PrintHelpAndReturn(),
            _ => PrintUnknownAndReturn(domain)
        };
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("error: " + ex.Message);
        return 1;
    }
}

static int HandleRoom(string root, string command, string[] args)
{
    return command switch
    {
        "create" => RoomCreate(root, args),
        "join" => RoomJoin(root, args),
        "list" => RoomList(root),
        "show" => RoomShow(root, args),
        _ => PrintUnknownAndReturn("room " + command)
    };
}

static int RoomCreate(string root, string[] args)
{
    var roomId = RequireOption(args, "--room");
    var playerId = RequireOption(args, "--player");
    var displayName = GetOption(args, "--name") ?? playerId;

    return RoomStore.WithRoomLock(root, roomId, () =>
    {
        var existing = RoomStore.LoadRoom(root, roomId);
        if (existing != null)
        {
            Console.Error.WriteLine("room already exists: " + roomId);
            return 1;
        }

        var room = new CliRoom
        {
            roomId = roomId,
            hostPlayerId = playerId,
            players =
            {
                new CliPlayer
                {
                    playerId = playerId,
                    displayName = displayName
                }
            }
        };
        RoomStore.SaveRoom(root, room);
        RoomStore.AppendLog(root, new CliLogEntry
        {
            timestamp = DateTimeOffset.UtcNow,
            roomId = roomId,
            type = "room.create",
            actorPlayerId = playerId,
            message = displayName + " created room"
        });
        Console.WriteLine("created room " + roomId + " host=" + playerId);
        return 0;
    });
}

static int RoomJoin(string root, string[] args)
{
    var roomId = RequireOption(args, "--room");
    var playerId = RequireOption(args, "--player");
    var displayName = GetOption(args, "--name") ?? playerId;

    return RoomStore.WithRoomLock(root, roomId, () =>
    {
        var room = RoomStore.LoadRoom(root, roomId);
        if (room == null)
        {
            Console.Error.WriteLine("room not found: " + roomId);
            return 1;
        }

        var existed = room.players.Exists(p => p.playerId == playerId);
        if (!existed)
        {
            room.players.Add(new CliPlayer
            {
                playerId = playerId,
                displayName = displayName
            });
            RoomStore.SaveRoom(root, room);
            RoomStore.AppendLog(root, new CliLogEntry
            {
                timestamp = DateTimeOffset.UtcNow,
                roomId = roomId,
                type = "room.join",
                actorPlayerId = playerId,
                message = displayName + " joined room"
            });
        }

        Console.WriteLine("joined room " + roomId + " as " + playerId + (existed ? " (already in room)" : string.Empty));
        return 0;
    });
}

static int RoomList(string root)
{
    var rooms = RoomStore.ListRooms(root).OrderBy(x => x).ToArray();
    if (rooms.Length == 0)
    {
        Console.WriteLine("no rooms");
        return 0;
    }

    for (var i = 0; i < rooms.Length; i++)
    {
        var roomId = rooms[i];
        var room = RoomStore.LoadRoom(root, roomId);
        if (room == null)
        {
            continue;
        }

        var battle = room.battleState == null ? "idle" : room.battleState.phase.ToString();
        Console.WriteLine(room.roomId + " players=" + room.players.Count + " host=" + room.hostPlayerId + " battle=" + battle);
    }

    return 0;
}

static int RoomShow(string root, string[] args)
{
    var roomId = RequireOption(args, "--room");
    var room = RoomStore.LoadRoom(root, roomId);
    if (room == null)
    {
        Console.Error.WriteLine("room not found: " + roomId);
        return 1;
    }

    Console.WriteLine(JsonSerializer.Serialize(room, RoomStore.JsonOptions));
    return 0;
}

static int HandleBattle(string root, string dataRoot, string command, string[] args)
{
    var repo = GameDataRepository.LoadFromDirectory(dataRoot);
    var engine = new BattleEngine(repo);

    return command switch
    {
        "start" => BattleStart(root, engine, args),
        "state" => BattleStateCmd(root, args),
        "legal" => BattleLegal(root, repo, args),
        "play" => BattlePlay(root, engine, args),
        "end-turn" => BattleEndTurn(root, engine, args),
        _ => PrintUnknownAndReturn("battle " + command)
    };
}

static int BattleStart(string root, BattleEngine engine, string[] args)
{
    var roomId = RequireOption(args, "--room");
    var playerId = RequireOption(args, "--player");
    var encounterId = GetOption(args, "--encounter") ?? "slime_intro";
    var deckId = GetOption(args, "--deck") ?? "co_op_starter";
    var seedRaw = GetOption(args, "--seed") ?? "24681357";
    var seed = int.Parse(seedRaw);

    return RoomStore.WithRoomLock(root, roomId, () =>
    {
        var room = RoomStore.LoadRoom(root, roomId);
        if (room == null)
        {
            Console.Error.WriteLine("room not found: " + roomId);
            return 1;
        }

        if (room.hostPlayerId != playerId)
        {
            Console.Error.WriteLine("only host can start battle");
            return 1;
        }

        var setup = new BattleSetupDefinition
        {
            roomId = roomId,
            randomSeed = seed,
            encounterId = encounterId,
            starterDeckId = deckId
        };

        for (var i = 0; i < room.players.Count; i++)
        {
            setup.players.Add(new BattleParticipantDefinition
            {
                playerId = room.players[i].playerId,
                displayName = room.players[i].displayName
            });
        }

        room.battleState = engine.CreateInitialState(setup);
        RoomStore.SaveRoom(root, room);
        RoomStore.AppendLog(root, new CliLogEntry
        {
            timestamp = DateTimeOffset.UtcNow,
            roomId = roomId,
            type = "battle.start",
            actorPlayerId = playerId,
            message = "battle started encounter=" + encounterId + " seed=" + seed
        });

        Console.WriteLine("battle started room=" + roomId + " phase=" + room.battleState.phase);
        return 0;
    });
}

static int BattleStateCmd(string root, string[] args)
{
    var roomId = RequireOption(args, "--room");
    var room = RoomStore.LoadRoom(root, roomId);
    if (room?.battleState == null)
    {
        Console.Error.WriteLine("battle not started");
        return 1;
    }

    Console.WriteLine(JsonSerializer.Serialize(room.battleState, RoomStore.JsonOptions));
    return 0;
}

static int BattleLegal(string root, GameDataRepository repo, string[] args)
{
    var roomId = RequireOption(args, "--room");
    var playerId = RequireOption(args, "--player");
    var room = RoomStore.LoadRoom(root, roomId);
    if (room?.battleState == null)
    {
        Console.Error.WriteLine("battle not started");
        return 1;
    }

    var state = room.battleState;
    var player = state.GetPlayer(playerId);
    if (player == null)
    {
        Console.Error.WriteLine("player not in battle: " + playerId);
        return 1;
    }

    if (state.phase != BattlePhase.PlayerTurn || player.hp <= 0 || player.hasEndedTurn)
    {
        Console.WriteLine("[]");
        return 0;
    }

    var actions = new List<object>
    {
        new { action = "end_turn" }
    };

    for (var i = 0; i < player.hand.Count; i++)
    {
        var card = player.hand[i];
        var def = repo.GetCard(card.cardId);
        if (player.energy < def.energyCost)
        {
            continue;
        }

        var targetType = BattleTargetResolver.ParseTargetType(def.targetType);
        switch (targetType)
        {
            case BattleTargetType.Self:
                actions.Add(new { action = "play_card", cardInstanceId = card.instanceId, targetFaction = "Allies", targetUnitId = playerId });
                break;
            case BattleTargetType.SingleAlly:
            case BattleTargetType.OtherAlly:
                for (var p = 0; p < state.players.Count; p++)
                {
                    var ally = state.players[p];
                    if (ally.hp <= 0)
                    {
                        continue;
                    }

                    if (targetType == BattleTargetType.OtherAlly && ally.playerId == playerId)
                    {
                        continue;
                    }

                    actions.Add(new { action = "play_card", cardInstanceId = card.instanceId, targetFaction = "Allies", targetUnitId = ally.playerId });
                }
                break;
            case BattleTargetType.MonsterPart:
                if (state.monster != null)
                {
                    for (var m = 0; m < state.monster.parts.Count; m++)
                    {
                        if (state.monster.parts[m].hp <= 0)
                        {
                            continue;
                        }

                        actions.Add(new { action = "play_card", cardInstanceId = card.instanceId, targetFaction = "Enemies", targetUnitId = state.monster.parts[m].instanceId });
                    }
                }
                break;
            case BattleTargetType.SingleUnit:
                for (var p = 0; p < state.players.Count; p++)
                {
                    var ally = state.players[p];
                    if (ally.hp > 0)
                    {
                        actions.Add(new { action = "play_card", cardInstanceId = card.instanceId, targetFaction = "Allies", targetUnitId = ally.playerId });
                    }
                }

                if (state.monster != null)
                {
                    for (var m = 0; m < state.monster.parts.Count; m++)
                    {
                        if (state.monster.parts[m].hp > 0)
                        {
                            actions.Add(new { action = "play_card", cardInstanceId = card.instanceId, targetFaction = "Enemies", targetUnitId = state.monster.parts[m].instanceId });
                        }
                    }
                }
                break;
            case BattleTargetType.Area:
                actions.Add(new { action = "play_card", cardInstanceId = card.instanceId, targetArea = "West" });
                actions.Add(new { action = "play_card", cardInstanceId = card.instanceId, targetArea = "East" });
                break;
            default:
                actions.Add(new { action = "play_card", cardInstanceId = card.instanceId });
                break;
        }
    }

    Console.WriteLine(JsonSerializer.Serialize(actions, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

static int BattlePlay(string root, BattleEngine engine, string[] args)
{
    var roomId = RequireOption(args, "--room");
    var playerId = RequireOption(args, "--player");
    var cardInstanceId = RequireOption(args, "--card");
    var targetFactionRaw = GetOption(args, "--target-faction") ?? "None";
    var targetUnit = GetOption(args, "--target");
    var targetAreaRaw = GetOption(args, "--target-area") ?? "West";

    var targetFaction = Enum.Parse<BattleTargetFaction>(targetFactionRaw, true);
    var targetArea = Enum.Parse<BattleArea>(targetAreaRaw, true);

    var command = new BattleCommand
    {
        commandId = Guid.NewGuid().ToString("N"),
        actorPlayerId = playerId,
        action = "play_card",
        cardInstanceId = cardInstanceId,
        targetFaction = targetFaction,
        targetUnitId = targetUnit,
        targetArea = targetArea
    };

    return ApplyCommand(root, roomId, playerId, engine, command);
}

static int BattleEndTurn(string root, BattleEngine engine, string[] args)
{
    var roomId = RequireOption(args, "--room");
    var playerId = RequireOption(args, "--player");

    var command = new BattleCommand
    {
        commandId = Guid.NewGuid().ToString("N"),
        actorPlayerId = playerId,
        action = "end_turn"
    };

    return ApplyCommand(root, roomId, playerId, engine, command);
}

static int ApplyCommand(string root, string roomId, string actorPlayerId, BattleEngine engine, BattleCommand command)
{
    return RoomStore.WithRoomLock(root, roomId, () =>
    {
        var room = RoomStore.LoadRoom(root, roomId);
        if (room?.battleState == null)
        {
            Console.Error.WriteLine("battle not started");
            return 1;
        }

        var result = engine.Apply(room.battleState, command);
        if (!result.success)
        {
            Console.Error.WriteLine("rejected: " + (string.IsNullOrEmpty(result.error) ? "unknown" : result.error));
            RoomStore.AppendLog(root, new CliLogEntry
            {
                timestamp = DateTimeOffset.UtcNow,
                roomId = roomId,
                type = "battle.reject",
                actorPlayerId = actorPlayerId,
                message = result.error ?? "unknown"
            });
            return 1;
        }

        room.messageSeq += 1;
        RoomStore.SaveRoom(root, room);
        for (var i = 0; i < result.events.Count; i++)
        {
            RoomStore.AppendLog(root, new CliLogEntry
            {
                timestamp = DateTimeOffset.UtcNow,
                roomId = roomId,
                type = "battle.event",
                actorPlayerId = actorPlayerId,
                message = result.events[i].message
            });
        }

        Console.WriteLine("ok phase=" + room.battleState.phase + " turn=" + room.battleState.turnIndex + " events=" + result.events.Count);
        return 0;
    });
}

static int HandleLog(string root, string command, string[] args)
{
    if (command != "tail")
    {
        return PrintUnknownAndReturn("log " + command);
    }

    var roomId = RequireOption(args, "--room");
    var linesRaw = GetOption(args, "--lines") ?? "30";
    var lineCount = int.Parse(linesRaw);
    var path = RoomStore.LogPath(root, roomId);
    if (!File.Exists(path))
    {
        Console.WriteLine("no log");
        return 0;
    }

    var all = File.ReadAllLines(path);
    var start = Math.Max(0, all.Length - lineCount);
    for (var i = start; i < all.Length; i++)
    {
        Console.WriteLine(all[i]);
    }

    return 0;
}

static string RequireOption(string[] args, string name)
{
    return GetOption(args, name) ?? throw new InvalidOperationException("missing option " + name);
}

static string? GetOption(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static string ResolveDefaultDataRoot()
{
    var direct = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Assets/Resources/GameData"));
    if (Directory.Exists(direct))
    {
        return direct;
    }

    var fromExe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Assets/Resources/GameData"));
    return fromExe;
}

static int PrintUnknownAndReturn(string command)
{
    Console.Error.WriteLine("unknown command: " + command);
    PrintHelp();
    return 1;
}

static int PrintHelpAndReturn()
{
    PrintHelp();
    return 0;
}

static void PrintHelp()
{
    Console.WriteLine("Young Bob Battle CLI");
    Console.WriteLine("root defaults to /tmp/young-bob-cli");
    Console.WriteLine();
    Console.WriteLine("room create --room <id> --player <id> [--name <name>] [--root <path>]");
    Console.WriteLine("room join --room <id> --player <id> [--name <name>] [--root <path>]");
    Console.WriteLine("room list [--root <path>]");
    Console.WriteLine("room show --room <id> [--root <path>]");
    Console.WriteLine();
    Console.WriteLine("battle start --room <id> --player <hostId> [--encounter slime_intro] [--deck co_op_starter] [--seed 24681357]");
    Console.WriteLine("battle state --room <id>");
    Console.WriteLine("battle legal --room <id> --player <id>");
    Console.WriteLine("battle play --room <id> --player <id> --card <instanceId> [--target-faction Allies|Enemies] [--target <unitId>] [--target-area West|East]");
    Console.WriteLine("battle end-turn --room <id> --player <id>");
    Console.WriteLine();
    Console.WriteLine("log tail --room <id> [--lines 30]");
    Console.WriteLine();
    Console.WriteLine("optional override: --data-root <path to Assets/Resources/GameData>");
}
