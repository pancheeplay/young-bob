# TapTap Co-op Prototype Plan

## Goal

Build a minimal two-player co-op turn-based battle prototype for TapTap:

- Match two players into one room.
- Start one shared battle.
- Each player has simple `Strike` and `Heal` cards.
- Players and monster take turns.
- Defeat the monster to win.

## Current code structure

- `Assets/Scripts/YoungBob/Prototype/Battle`
  - Pure C# battle state and rules.
- `Assets/Scripts/YoungBob/Prototype/Data`
  - JSON-driven card, deck and encounter loading.
- `Assets/Scripts/YoungBob/Prototype/Multiplayer`
  - Transport abstraction.
  - `LoopbackMultiplayerService` for local single-client validation.
  - `TapTapMultiplayerService` placeholder for real SDK binding.
- `Assets/Scripts/YoungBob/Prototype/App`
  - Session orchestration between battle, transport and UI.
- `Assets/Scripts/YoungBob/Prototype/UI`
  - Runtime-generated debug UI.

## Why this shape

- Rules stay in pure C# and can later be unit-tested.
- Transport is isolated, so loopback and TapTap use the same battle/session layer.
- UI remains disposable; prototype iteration does not force scene prefab work.
- Data changes mostly live in JSON, not in inspector state.

## TapTap mapping

The intended TapTap flow is:

1. `TapTapSDK.Init(...)`
2. `TapBattleClient.Initialize(eventHandler)`
3. `TapBattleClient.Connect(...)`
4. `TapBattleClient.MatchRoom(...)`
5. `TapBattleClient.SendCustomMessage(...)`

Map those calls into `TapTapMultiplayerService`:

- `Connect(playerId, displayName)`
  - Call TapTap SDK init if not already done.
  - Call `TapBattleClient.Initialize`.
  - Call `TapBattleClient.Connect`.
  - On success, raise `Connected(playerId)`.

- `MatchOrCreateRoom()`
  - Call `TapBattleClient.MatchRoom`.
  - Convert room info into `RoomJoinedEvent`.
  - Fill `roomId`, `localPlayerId`, `hostPlayerId`, `players`.
  - Raise `RoomJoined(...)`.

- `Send(message)`
  - Serialize `MultiplayerMessage` to JSON.
  - Call `TapBattleClient.SendCustomMessage`.

- TapTap event handler callbacks
  - `OnRoomJoin` / `OnPlayerJoin` / `OnPlayerLeave`
  - `OnCustomMessage`
  - `OnDisconnected`
  - Translate them into `RoomJoined`, `MessageReceived`, and future disconnect events.

## Message protocol

Use custom messages only. This prototype does not need frame sync.

- `battle.start`
  - Payload:
    - `randomSeed`
    - `encounterId`
    - `starterDeckId`
- `battle.command`
  - Payload:
    - `actorPlayerId`
    - `action`
    - `cardInstanceId`
    - `targetPlayerId`

Envelope fields already exist in `MultiplayerMessage`:

- `messageId`
- `type`
- `senderPlayerId`
- `roomId`
- `seq`
- `payloadJson`

## Authority model

Recommended for the first real TapTap version:

- Host is authoritative.
- All clients send `battle.command`.
- Host validates and applies command.
- Host broadcasts resulting state transition or command commit.

The current local loopback version applies received commands directly because it is only for fast local validation.

## Suggested next implementation step

Replace `LoopbackMultiplayerService` in bootstrap with a small factory:

- If TapTap SDK is available and platform is correct, use `TapTapMultiplayerService`.
- Otherwise use `LoopbackMultiplayerService`.

Then implement only these first:

1. Connect
2. Match room
3. Receive room member list
4. Send and receive one `battle.start` message
5. Send and receive one `battle.command` message

Do not implement reconnection, ready-check, or room properties before the above works end-to-end.
