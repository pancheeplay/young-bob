using YoungBob.Prototype.Battle;

internal sealed class StepExecutor
{
    private readonly Dictionary<string, Func<BattleEngine, BattleState, ScenarioStep, StepResult>> _handlers;

    public StepExecutor()
    {
        _handlers = new Dictionary<string, Func<BattleEngine, BattleState, ScenarioStep, StepResult>>(StringComparer.OrdinalIgnoreCase)
        {
            { "end_turn", ExecuteEndTurn },
            { "play_card", ExecutePlayCard },
            { "snapshot", ExecuteSnapshot },
            { "debug_damage_monster", ExecuteDebugDamageMonster },
            { "debug_set_player_hp", ExecuteDebugSetPlayerHp },
            { "debug_set_player_armor", ExecuteDebugSetPlayerArmor },
            { "debug_clear_hand", ExecuteDebugClearHand },
            { "debug_add_card_to_hand", ExecuteDebugAddCardToHand }
        };
    }

    public StepResult Execute(BattleEngine engine, BattleState state, ScenarioStep step)
    {
        if (!_handlers.TryGetValue(step.action, out var handler))
        {
            return new StepResult { success = false, error = "unknown action: " + step.action };
        }

        return handler(engine, state, step);
    }

    private static StepResult ExecuteEndTurn(BattleEngine engine, BattleState state, ScenarioStep step)
    {
        return Apply(engine, state, new BattleCommand
        {
            commandId = Guid.NewGuid().ToString("N"),
            actorPlayerId = step.actorPlayerId,
            action = "end_turn"
        });
    }

    private static StepResult ExecutePlayCard(BattleEngine engine, BattleState state, ScenarioStep step)
    {
        return Apply(engine, state, new BattleCommand
        {
            commandId = Guid.NewGuid().ToString("N"),
            actorPlayerId = step.actorPlayerId,
            action = "play_card",
            cardInstanceId = ResolveCardInstanceId(state, step.actorPlayerId, step.cardInstanceId),
            targetFaction = step.targetFaction,
            targetUnitId = ResolveTargetUnitId(state, step.targetUnitId),
            targetArea = step.targetArea
        });
    }

    private static StepResult ExecuteSnapshot(BattleEngine engine, BattleState state, ScenarioStep step)
    {
        return new StepResult { success = true };
    }

    private static StepResult ExecuteDebugDamageMonster(BattleEngine engine, BattleState state, ScenarioStep step)
    {
        if (state.monster == null)
        {
            return new StepResult { success = false, error = "Monster not found." };
        }

        var damage = Math.Max(0, step.debugValue);
        state.monster.coreHp = Math.Max(0, state.monster.coreHp - damage);
        for (var i = 0; i < state.monster.parts.Count; i++)
        {
            var part = state.monster.parts[i];
            part.hp = Math.Max(0, part.hp - damage);
            if (part.hp == 0)
            {
                part.isBroken = true;
            }
        }

        return new StepResult { success = true };
    }

    private static StepResult ExecuteDebugSetPlayerHp(BattleEngine engine, BattleState state, ScenarioStep step)
    {
        var player = state.GetPlayer(step.targetUnitId);
        if (player == null)
        {
            return new StepResult { success = false, error = "Player not found: " + step.targetUnitId };
        }

        player.hp = Math.Max(0, Math.Min(player.maxHp, step.debugValue));
        return new StepResult { success = true };
    }

    private static StepResult ExecuteDebugSetPlayerArmor(BattleEngine engine, BattleState state, ScenarioStep step)
    {
        var player = state.GetPlayer(step.targetUnitId);
        if (player == null)
        {
            return new StepResult { success = false, error = "Player not found: " + step.targetUnitId };
        }

        player.armor = Math.Max(0, step.debugValue);
        return new StepResult { success = true };
    }

    private static StepResult ExecuteDebugClearHand(BattleEngine engine, BattleState state, ScenarioStep step)
    {
        var player = state.GetPlayer(step.actorPlayerId);
        if (player == null)
        {
            return new StepResult { success = false, error = "Player not found: " + step.actorPlayerId };
        }

        while (player.hand.Count > 0)
        {
            var card = player.hand[0];
            player.hand.RemoveAt(0);
            player.discardPile.Add(card);
        }

        return new StepResult { success = true };
    }

    private static StepResult ExecuteDebugAddCardToHand(BattleEngine engine, BattleState state, ScenarioStep step)
    {
        var player = state.GetPlayer(step.actorPlayerId);
        if (player == null)
        {
            return new StepResult { success = false, error = "Player not found: " + step.actorPlayerId };
        }

        var cardId = string.IsNullOrWhiteSpace(step.cardId) ? step.cardInstanceId : step.cardId;
        if (string.IsNullOrWhiteSpace(cardId))
        {
            return new StepResult { success = false, error = "Missing cardId for debug_add_card_to_hand." };
        }

        BattleMechanics.AddCardToHandOrDiscard(state, player, cardId, forceIntoHand: true);
        return new StepResult { success = true };
    }

    private static StepResult Apply(BattleEngine engine, BattleState state, BattleCommand command)
    {
        var result = engine.Apply(state, command);

        return new StepResult
        {
            success = result.success,
            error = result.error,
            events = result.events == null ? new List<BattleEvent>() : new List<BattleEvent>(result.events)
        };
    }

    private static string ResolveCardInstanceId(BattleState state, string actorPlayerId, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        if (!raw.StartsWith("card:", StringComparison.Ordinal))
        {
            return raw;
        }

        var player = state.GetPlayer(actorPlayerId);
        if (player == null)
        {
            return raw;
        }

        var cardId = raw.Substring("card:".Length);
        var match = player.hand.FirstOrDefault(item => item.cardId == cardId);
        return match == null ? raw : match.instanceId;
    }

    private static string ResolveTargetUnitId(BattleState state, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return raw;
        }

        if (string.Equals(raw, "$first_part", StringComparison.Ordinal))
        {
            if (state.monster == null || state.monster.parts == null)
            {
                return raw;
            }

            for (var i = 0; i < state.monster.parts.Count; i++)
            {
                var part = state.monster.parts[i];
                if (part != null)
                {
                    return part.instanceId;
                }
            }

            return raw;
        }

        if (string.Equals(raw, "$first_ground_part", StringComparison.Ordinal))
        {
            if (state.monster == null || state.monster.parts == null)
            {
                return raw;
            }

            for (var i = 0; i < state.monster.parts.Count; i++)
            {
                var part = state.monster.parts[i];
                if (part != null && part.relativeHeight == BattleHeight.Ground)
                {
                    return part.instanceId;
                }
            }

            return raw;
        }

        if (raw.StartsWith("$part:", StringComparison.Ordinal))
        {
            if (state.monster == null || state.monster.parts == null)
            {
                return raw;
            }

            var partId = raw.Substring("$part:".Length);
            for (var i = 0; i < state.monster.parts.Count; i++)
            {
                var part = state.monster.parts[i];
                if (part != null && string.Equals(part.partId, partId, StringComparison.OrdinalIgnoreCase))
                {
                    return part.instanceId;
                }
            }

            return raw;
        }

        return raw;
    }
}
