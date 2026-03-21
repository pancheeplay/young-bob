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

            if (action.Arguments.Length != metadata.Arity)
            {
                error = action.Head + " 需要 " + metadata.Arity + " 个参数。";
                return false;
            }

            effect = new CardEffectDefinition
            {
                op = metadata.RuntimeOp
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
                statusId = prototype.statusId
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

                    return effect;
            }
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
