using System;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Battle
{
    internal static class CardEffectActionCompiler
    {
        public static bool TryBuildActionPrototype(
            SExpressionListNode action,
            out CardEffectDefinition effect,
            out string error)
        {
            effect = null;
            error = null;
            if (!CardEffectActionRegistry.TryGet(action.Head, out var metadata))
            {
                error = "未知 DSL 动作：" + action.Head;
                return false;
            }

            var acceptsDurationSuffix = metadata.SupportsDurationSuffix;
            if (action.Arguments.Length != metadata.Arity
                && !(acceptsDurationSuffix && action.Arguments.Length == metadata.Arity + 1))
            {
                error = action.Head + " 需要 " + metadata.Arity + " 个参数。";
                return false;
            }

            effect = new CardEffectDefinition
            {
                op = metadata.RuntimeOp,
                durationKind = metadata.DefaultDurationKind,
                durationTurns = metadata.DefaultDurationTurns
            };

            for (var i = 0; i < metadata.ArgumentKinds.Length; i++)
            {
                switch (metadata.ArgumentKinds[i])
                {
                    case CardEffectValueKind.Target:
                        effect.target = ConvertTarget(action.Arguments[i], out error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            return false;
                        }

                        break;
                    case CardEffectValueKind.Symbol:
                    {
                        var symbol = RequireSymbol(action.Arguments[i], out error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            error = metadata.SymbolArgumentError ?? error;
                            return false;
                        }

                        if (string.Equals(action.Head, "move-area", StringComparison.Ordinal))
                        {
                            effect.destinationId = symbol;
                        }
                        else
                        {
                            effect.statusId = symbol;
                        }

                        break;
                    }
                }
            }

            return true;
        }

        public static CardEffectDefinition MaterializeEffectForTarget(
            CardEffectExecutionContext context,
            SExpressionListNode action,
            CardEffectDefinition prototype,
            CardEffectTargetRef target)
        {
            var effect = new CardEffectDefinition
            {
                op = prototype.op,
                target = prototype.target,
                destinationId = prototype.destinationId,
                statusId = prototype.statusId,
                durationKind = prototype.durationKind,
                durationTurns = prototype.durationTurns
            };

            if (!CardEffectActionRegistry.TryGet(action.Head, out var metadata))
            {
                return effect;
            }

            switch (action.Head)
            {
                case "recycle-discard-to-hand":
                    effect.amount = (int)Math.Round(CardEffectEvaluator.EvaluateNumber(context, action.Arguments[1], target));
                    effect.amount2 = (int)Math.Round(CardEffectEvaluator.EvaluateNumber(context, action.Arguments[2], target));
                    return effect;
                default:
                    if (metadata.NumericArgumentStartIndex > 0)
                    {
                        effect.amount = (int)Math.Round(CardEffectEvaluator.EvaluateNumber(
                            context,
                            action.Arguments[metadata.NumericArgumentStartIndex],
                            target));
                    }

                    if (metadata.SupportsDurationSuffix && action.Arguments.Length > metadata.Arity)
                    {
                        if (!TryMaterializeDuration(context, action.Arguments[metadata.Arity], target, out var durationKind, out var durationTurns, out var durationError))
                        {
                            return effect;
                        }

                        effect.durationKind = durationKind;
                        effect.durationTurns = durationTurns;
                    }

                    return effect;
            }
        }

        private static bool TryMaterializeDuration(
            CardEffectExecutionContext context,
            SExpressionNode node,
            CardEffectTargetRef target,
            out BattleStatusDurationKind durationKind,
            out int durationTurns,
            out string error)
        {
            durationKind = BattleStatusDurationKind.Permanent;
            durationTurns = 0;
            error = null;

            if (node is SExpressionNumberNode number)
            {
                durationKind = BattleStatusDurationKind.TurnCount;
                durationTurns = Math.Max(1, (int)Math.Round(number.Value));
                return true;
            }

            if (node is SExpressionSymbolNode symbol)
            {
                return TryParseDurationSymbol(symbol.Value, out durationKind, out durationTurns, out error);
            }

            if (node is SExpressionListNode list)
            {
                if (string.Equals(list.Head, "until-turn-start", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(list.Head, "until-next-turn-start", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(list.Head, "until-owner-turn-start", StringComparison.OrdinalIgnoreCase))
                {
                    durationKind = BattleStatusDurationKind.UntilTurnStart;
                    durationTurns = 1;
                    return true;
                }

                if (string.Equals(list.Head, "permanent", StringComparison.OrdinalIgnoreCase))
                {
                    durationKind = BattleStatusDurationKind.Permanent;
                    durationTurns = 0;
                    return true;
                }

                if (string.Equals(list.Head, "turns", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(list.Head, "duration", StringComparison.OrdinalIgnoreCase))
                {
                    if (list.Arguments == null || list.Arguments.Length != 1)
                    {
                        error = "duration 需要 1 个参数。";
                        return false;
                    }

                    durationKind = BattleStatusDurationKind.TurnCount;
                    durationTurns = Math.Max(1, (int)Math.Round(CardEffectEvaluator.EvaluateNumber(context, list.Arguments[0], target)));
                    return true;
                }
            }

            error = "无法识别的持续时间表达式。";
            return false;
        }

        private static bool TryParseDurationSymbol(string value, out BattleStatusDurationKind durationKind, out int durationTurns, out string error)
        {
            durationKind = BattleStatusDurationKind.Permanent;
            durationTurns = 0;
            error = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                error = "持续时间参数不能为空。";
                return false;
            }

            if (string.Equals(value, "permanent", StringComparison.OrdinalIgnoreCase))
            {
                durationKind = BattleStatusDurationKind.Permanent;
                durationTurns = 0;
                return true;
            }

            if (string.Equals(value, "until-turn-start", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "until-next-turn-start", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "until-owner-turn-start", StringComparison.OrdinalIgnoreCase))
            {
                durationKind = BattleStatusDurationKind.UntilTurnStart;
                durationTurns = 1;
                return true;
            }

            error = "未知持续时间符号：" + value;
            return false;
        }

        private static string ConvertTarget(SExpressionNode node, out string error)
        {
            error = null;
            var targetSymbol = RequireSymbol(node, out error);
            if (!string.IsNullOrEmpty(error))
            {
                return null;
            }

            switch (targetSymbol)
            {
                case "self":
                    return "Self";
                case "target":
                    return "CardTarget";
                case "all-allies":
                    return "AllAllies";
                case "all-enemies":
                    return "AllEnemies";
                default:
                    error = "未知目标：" + targetSymbol;
                    return null;
            }
        }

        private static string RequireSymbol(SExpressionNode node, out string error)
        {
            error = null;
            if (node is SExpressionSymbolNode symbol && !string.IsNullOrWhiteSpace(symbol.Value))
            {
                return symbol.Value;
            }

            error = "需要 symbol 参数。";
            return null;
        }
    }
}
