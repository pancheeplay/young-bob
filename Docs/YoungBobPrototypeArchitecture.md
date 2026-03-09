# Young Bob Prototype: Architecture Notes

## 1. Project Goal

This project is a minimal co-op turn-based card battle prototype.

Current target:

- Use TapTap room-based multiplayer.
- Keep gameplay in pure C# as much as possible.
- Keep UI code-driven.
- Validate the full loop:
  - connect
  - create/join/match room
  - enter battle
  - play cards
  - end team turn
  - monster acts
  - end battle
  - return to lobby

## 2. Core Files

### Scene / lifecycle

- `Assets/Scripts/YoungBob/Prototype/Scene/YoungBobGameManager.cs`
- `Assets/Scripts/YoungBob/Prototype/Scene/YoungBobUiManager.cs`

### Application / session

- `Assets/Scripts/YoungBob/Prototype/App/PrototypeSessionController.cs`

### Battle logic

- `Assets/Scripts/YoungBob/Prototype/Battle/BattleTypes.cs`
- `Assets/Scripts/YoungBob/Prototype/Battle/BattleEngine.cs`

### Data

- `Assets/Scripts/YoungBob/Prototype/Data/GameDataRepository.cs`
- `Assets/Resources/GameData/cards.json`
- `Assets/Resources/GameData/decks.json`
- `Assets/Resources/GameData/encounters.json`

### Multiplayer abstraction / TapTap adapter

- `Assets/Scripts/YoungBob/Prototype/Multiplayer/MultiplayerContracts.cs`
- `Assets/Scripts/YoungBob/Prototype/Multiplayer/TapTapRuntimeMultiplayerService.cs`
- `Assets/Scripts/YoungBob/Prototype/Multiplayer/TapTapMultiplayerService.cs`

## 3. Layered Structure

### Layer A: Scene and lifecycle

Owner:

- `YoungBobGameManager`
- `YoungBobUiManager`

Responsibilities:

- Create runtime dependencies.
- Own startup flow.
- Connect UI to application state.
- Switch pages: lobby / room / battle.

Rule:

- No battle rules here.
- No TapTap SDK details here except service wiring.

### Layer B: Application / session

Owner:

- `PrototypeSessionController`

Responsibilities:

- Own current room state.
- Own current battle state.
- Convert UI actions into network messages.
- Receive network messages and trigger application changes.
- Broadcast state changes to UI.

Rule:

- This is the coordinator layer.
- It may know transport contracts and battle engine.
- It should not contain raw TapTap SDK calls.

### Layer C: Battle logic

Owner:

- `BattleEngine`
- `BattleTypes`

Responsibilities:

- Define battle state.
- Validate battle commands.
- Advance turn flow.
- Apply card effects.
- Apply monster behavior.
- Produce battle events.

Rule:

- Keep this pure C#.
- Do not add TapTap SDK code here.
- Do not add UI code here.

### Layer D: Data and transport adapter

Owner:

- `GameDataRepository`
- `IMultiplayerService`
- `TapTapRuntimeMultiplayerService`

Responsibilities:

- Load static content data.
- Define transport contracts.
- Adapt TapTap SDK into internal multiplayer events and operations.

Rule:

- TapTap-specific quirks belong here.
- Do not leak TapTap-specific code into battle rules.

## 4. Current Battle Rules

### Team turn model

Current player phase is a shared team phase.

Rules:

- All alive players can act during player phase.
- Playing a card applies immediately.
- Each player has `hasEndedTurn`.
- A player pressing `End Turn` only ends their own participation.
- Monster turn starts only after all alive players have ended.
- After monster turn, all alive players are reset for the next team turn.

### Current simple content

Cards:

- `Strike`: damage monster
- `Heal`: heal player

Monster:

- simple attack behavior

Win condition:

- monster HP reaches 0

Lose condition:

- all players die

## 5. UI Structure

UI is code-driven, but manager-owned.

Main manager:

- `YoungBobUiManager`

Pages:

- `LobbyPage`
- `RoomPage`
- `BattlePage`

Rules:

- Pages may be classes.
- UI elements may be created in code.
- Keep business rules out of page classes.
- UI should render state, not own game rules.

## 6. Data Rules

Current game data is loaded from:

- `Resources/GameData`

Current reason:

- more stable than direct `StreamingAssets` file IO for mobile/Tap runtime

Rule:

- Add new cards / encounters / starter decks as data first.
- Avoid inspector-driven gameplay logic.

## 7. Multiplayer Abstraction

Internal multiplayer entry point:

- `IMultiplayerService`

Current events:

- `Connected`
- `RoomJoined`
- `RoomListUpdated`
- `MessageReceived`

Current operations:

- `Connect`
- `Disconnect`
- `CreateRoom`
- `MatchOrCreateRoom`
- `RefreshRoomList`
- `JoinRoom`
- `LeaveRoom`
- `Send`

Rule:

- Game/session code depends on `IMultiplayerService`, not directly on TapTap SDK.

## 8. TapTap Multiplayer Notes

These are the important TapTap rules for this project.

### Rule 1: Use room + custom message

For this prototype:

- use TapTap room APIs
- use `SendCustomMessage`
- do not use frame sync for core gameplay

Reason:

- this game is turn-based
- message frequency is low
- state-based sync is simpler

### Rule 2: `SendCustomMessage(type=0)` does not echo to sender

Meaning:

- sender does not receive its own `OnCustomMessage`
- only other players receive it

Project consequence:

- sender must apply local success behavior manually
- otherwise sender and receivers diverge

Current project already handles this in Tap transport.

### Rule 3: Keep request shape close to official Tap demo

For room APIs, keep these structures explicit:

- `roomCfg`
- `playerCfg`
- `customProperties`
- `matchParams`

Do not aggressively simplify request objects.

Reason:

- Tap bridge/runtime is strict about structure

### Rule 4: Avoid overlapping `GetRoomList`

Tap room list fetch can fail if a previous request is still in progress.

Project consequence:

- room list refresh must be throttled
- auto refresh must not issue overlapping requests

### Rule 5: Keep custom message envelope explicit

Preferred shape:

- `messageId`
- `type`
- `senderPlayerId`
- `roomId`
- `seq`
- `payload`

Avoid:

- unstable implicit object serialization
- deep ad hoc nesting
- inconsistent message shapes

### Rule 6: Let room exit complete through service/event flow

Do not clear local room state too early.

Bad:

- click leave
- clear local state immediately
- later receive Tap room event
- UI flickers or re-enters room

Good:

- request leave
- finalize state when service/event confirms it

### Rule 7: Keep TapTap-specific logic in adapter layer

TapTap-specific concerns belong in:

- `TapTapRuntimeMultiplayerService`

They should not spread into:

- `BattleEngine`
- battle state definitions
- UI pages

## 9. Current Battle Network Model

The project now uses a host-authoritative model.

### Message types

- `battle.start`
- `battle.command`
- `battle.commit`
- `battle.finish`

### `battle.start`

Sender:

- host only

Purpose:

- initialize battle for all players

### `battle.command`

Sender:

- any client

Purpose:

- send player intent only
- examples:
  - play card
  - end turn

Rule:

- do not treat this as final battle state

### `battle.commit`

Sender:

- host only

Purpose:

- publish authoritative result of a command

Current payload contains:

- source command id
- battle state snapshot
- battle events

Rule:

- all clients apply `battle.commit`
- only host runs authoritative state mutation

### `battle.finish`

Sender:

- host only

Purpose:

- end the battle
- drive return-to-lobby flow

## 10. Important Invariants

These should stay true.

### Architecture invariants

- Battle rules live in pure C# battle layer.
- UI does not own gameplay state machine.
- TapTap SDK code stays in transport adapter layer.
- Session layer coordinates but does not become a giant god-object.

### Sync invariants

- Clients send intent.
- Host validates and applies.
- Host broadcasts authoritative commit.
- Clients render committed state.

### Data invariants

- Content changes should start from data definitions.
- New cards/encounters should not require inspector business logic.

## 11. Recommended Next Steps

Best next work:

1. Refine battle UX under the team-turn model.
2. Tighten invalid-action UI states for host and non-host.
3. Normalize commit payload structure further if battle complexity grows.
4. Add a non-Tap debug relay transport for multi-developer testing outside Tap runtime.

## 12. Short Summary

If you need a very short mental model:

- `GameManager`: boot and wire things
- `UIManager`: show pages and forward user input
- `SessionController`: room + battle coordination
- `BattleEngine`: real gameplay rules
- `TapTapRuntimeMultiplayerService`: Tap room/message adapter

And the most important multiplayer rule is:

- sender sends `battle.command`
- host executes
- host broadcasts `battle.commit`
- everyone applies the commit

