# BattleCli

`BattleCli` is a local command line harness for Young Bob battle simulation.
It uses file-based room state so multiple CLI processes can collaborate on the same room.

## Build

```bash
dotnet build Tools/BattleCli/BattleCli.csproj -nologo
```

## Key Paths

- Default room/log root: `/tmp/young-bob-cli`
- Default data root: `Assets/Resources/GameData`

Override with:

- `--root /custom/path`
- `--data-root /custom/game-data-path`

## Two-CLI Flow

Terminal A:

```bash
dotnet run --project Tools/BattleCli/BattleCli.csproj -- room create --room demo --player p1 --name Alpha
dotnet run --project Tools/BattleCli/BattleCli.csproj -- battle start --room demo --player p1
```

Terminal B:

```bash
dotnet run --project Tools/BattleCli/BattleCli.csproj -- room join --room demo --player p2 --name Beta
```

Either terminal can inspect and act:

```bash
dotnet run --project Tools/BattleCli/BattleCli.csproj -- battle legal --room demo --player p1
dotnet run --project Tools/BattleCli/BattleCli.csproj -- battle play --room demo --player p1 --card <cardInstanceId> --target-faction Enemies --target <monsterPartInstanceId>
dotnet run --project Tools/BattleCli/BattleCli.csproj -- battle end-turn --room demo --player p1
dotnet run --project Tools/BattleCli/BattleCli.csproj -- battle state --room demo
dotnet run --project Tools/BattleCli/BattleCli.csproj -- log tail --room demo --lines 50
```

## Commands

- `room create --room <id> --player <id> [--name <name>]`
- `room join --room <id> --player <id> [--name <name>]`
- `room list`
- `room show --room <id>`
- `battle start --room <id> --player <hostId> [--encounter slime_intro] [--deck co_op_starter] [--seed 24681357]`
- `battle state --room <id>`
- `battle legal --room <id> --player <id>`
- `battle play --room <id> --player <id> --card <instanceId> [--target-faction Allies|Enemies] [--target <unitId>] [--target-area West|East]`
- `battle end-turn --room <id> --player <id>`
- `log tail --room <id> [--lines 30]`

## Notes

- State updates are protected by a per-room file lock.
- Event logs are JSONL and can be parsed by an LLM/tooling pipeline.
- Host-only rule is enforced for `battle start`.
