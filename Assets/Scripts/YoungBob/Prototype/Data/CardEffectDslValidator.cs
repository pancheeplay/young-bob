using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Data
{
    internal static class CardEffectDslValidator
    {
        private static readonly HashSet<string> TargetSymbols = new HashSet<string>(StringComparer.Ordinal)
        {
            "self",
            "target",
            "all-allies",
            "all-enemies"
        };

        private static readonly HashSet<string> NumericSymbols = new HashSet<string>(StringComparer.Ordinal)
        {
            "self-armor",
            "cards-played-this-turn",
            "target-poison",
            "target-hp",
            "last-damage-dealt",
            "energy"
        };

        private static readonly HashSet<string> DestinationSymbols = new HashSet<string>(StringComparer.Ordinal)
        {
            "selected-area",
            "another-side-area"
        };

        private static readonly HashSet<string> BooleanSymbols = new HashSet<string>(StringComparer.Ordinal)
        {
            "last-killed-target?",
            "target-is-player?",
            "target-is-monster-part?",
            "has-target?"
        };

        public static void Validate(SExpressionNode node)
        {
            ValidateEffectNode(node);
        }

        private static void ValidateEffectNode(SExpressionNode node)
        {
            var list = RequireList(node, "效果节点必须是列表。");
            switch (list.Head)
            {
                case "do":
                    if (list.Arguments.Length == 0)
                    {
                        throw new InvalidOperationException("do 至少需要 1 个子节点。");
                    }

                    for (var i = 0; i < list.Arguments.Length; i++)
                    {
                        ValidateEffectNode(list.Arguments[i]);
                    }

                    return;
                case "if":
                    RequireArity(list, 2);
                    ValidateBooleanExpr(list.Arguments[0]);
                    ValidateEffectNode(list.Arguments[1]);
                    return;
                case "repeat":
                    RequireArity(list, 2);
                    ValidateNumericExpr(list.Arguments[0]);
                    ValidateEffectNode(list.Arguments[1]);
                    return;
                default:
                    ValidateAction(list);
                    return;
            }
        }

        private static void ValidateAction(SExpressionListNode list)
        {
            if (!CardEffectActionRegistry.TryGet(list.Head, out var metadata))
            {
                throw new InvalidOperationException("未知 DSL 动作：" + list.Head);
            }

            if (metadata.SupportsDurationSuffix)
            {
                if (list.Arguments.Length != metadata.Arity && list.Arguments.Length != metadata.Arity + 1)
                {
                    throw new InvalidOperationException(list.Head + " 需要 " + metadata.Arity + " 个参数。");
                }
            }
            else
            {
                RequireArity(list, metadata.Arity);
            }

            for (var i = 0; i < metadata.ArgumentKinds.Length; i++)
            {
                switch (metadata.ArgumentKinds[i])
                {
                    case CardEffectValueKind.Target:
                        ValidateTarget(list.Arguments[i]);
                        break;
                    case CardEffectValueKind.Number:
                        ValidateNumericExpr(list.Arguments[i]);
                        break;
                    case CardEffectValueKind.Symbol:
                    {
                        var symbol = RequirePlainSymbol(list.Arguments[i], metadata.SymbolArgumentError ?? (list.Head + " 的 symbol 参数无效。"));
                        if (string.Equals(list.Head, "move-area", StringComparison.Ordinal))
                        {
                            if (!DestinationSymbols.Contains(symbol))
                            {
                                throw new InvalidOperationException("未知位移目的地：" + symbol);
                            }
                    }

                        break;
                    }
                    default:
                        throw new InvalidOperationException("未知 DSL 参数类型。");
                }
            }

            if (metadata.SupportsDurationSuffix && list.Arguments.Length > metadata.Arity)
            {
                ValidateDurationSuffix(list.Arguments[metadata.Arity]);
            }
        }

        private static void ValidateDurationSuffix(SExpressionNode node)
        {
            if (node is SExpressionNumberNode)
            {
                return;
            }

            if (node is SExpressionSymbolNode symbol)
            {
                if (string.IsNullOrWhiteSpace(symbol.Value))
                {
                    throw new InvalidOperationException("持续时间参数不能为空。");
                }

                switch (symbol.Value)
                {
                    case "until-turn-start":
                    case "until-next-turn-start":
                    case "until-owner-turn-start":
                    case "permanent":
                        return;
                }

                throw new InvalidOperationException("未知持续时间符号：" + symbol.Value);
            }

            var list = RequireList(node, "持续时间表达式无效。");
            switch (list.Head)
            {
                case "turns":
                case "duration":
                    RequireArity(list, 1);
                    ValidateNumericExpr(list.Arguments[0]);
                    return;
                case "until-turn-start":
                case "until-next-turn-start":
                case "until-owner-turn-start":
                case "permanent":
                    RequireArity(list, 0);
                    return;
                default:
                    throw new InvalidOperationException("不支持的持续时间表达式：" + list.Head);
            }
        }

        private static void ValidateTarget(SExpressionNode node)
        {
            var value = RequirePlainSymbol(node, "target 必须是 symbol。");
            if (!TargetSymbols.Contains(value))
            {
                throw new InvalidOperationException("未知 target：" + value);
            }
        }

        private static void ValidateNumericExpr(SExpressionNode node)
        {
            if (node is SExpressionNumberNode)
            {
                return;
            }

            if (node is SExpressionSymbolNode symbol)
            {
                if (symbol.Value.EndsWith("?", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("布尔变量不能用在数值表达式里：" + symbol.Value);
                }

                if (!NumericSymbols.Contains(symbol.Value))
                {
                    throw new InvalidOperationException("未知数值变量：" + symbol.Value);
                }

                return;
            }

            var list = RequireList(node, "数值表达式无效。");
            switch (list.Head)
            {
                case "+":
                case "*":
                case "min":
                case "max":
                    if (list.Arguments.Length == 0)
                    {
                        throw new InvalidOperationException(list.Head + " 至少需要 1 个参数。");
                    }

                    for (var i = 0; i < list.Arguments.Length; i++)
                    {
                        ValidateNumericExpr(list.Arguments[i]);
                    }

                    return;
                case "-":
                    if (list.Arguments.Length == 0)
                    {
                        throw new InvalidOperationException("- 至少需要 1 个参数。");
                    }

                    for (var i = 0; i < list.Arguments.Length; i++)
                    {
                        ValidateNumericExpr(list.Arguments[i]);
                    }

                    return;
                case "/":
                    RequireArity(list, 2);
                    ValidateNumericExpr(list.Arguments[0]);
                    ValidateNumericExpr(list.Arguments[1]);
                    return;
                default:
                    throw new InvalidOperationException("不支持的数值表达式：" + list.Head);
            }
        }

        private static void ValidateBooleanExpr(SExpressionNode node)
        {
            if (node is SExpressionSymbolNode symbol)
            {
                if (!symbol.Value.EndsWith("?", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("布尔变量必须以 ? 结尾：" + symbol.Value);
                }

                if (!BooleanSymbols.Contains(symbol.Value))
                {
                    throw new InvalidOperationException("未知布尔变量：" + symbol.Value);
                }

                return;
            }

            var list = RequireList(node, "布尔表达式无效。");
            switch (list.Head)
            {
                case ">":
                case "<":
                case ">=":
                case "<=":
                case "=":
                    RequireArity(list, 2);
                    ValidateNumericExpr(list.Arguments[0]);
                    ValidateNumericExpr(list.Arguments[1]);
                    return;
                default:
                    throw new InvalidOperationException("不支持的布尔表达式：" + list.Head);
            }
        }

        private static string RequirePlainSymbol(SExpressionNode node, string error)
        {
            if (node is SExpressionSymbolNode symbol && !string.IsNullOrWhiteSpace(symbol.Value))
            {
                return symbol.Value;
            }

            throw new InvalidOperationException(error);
        }

        private static SExpressionListNode RequireList(SExpressionNode node, string error)
        {
            if (node is SExpressionListNode list)
            {
                return list;
            }

            throw new InvalidOperationException(error);
        }

        private static void RequireArity(SExpressionListNode list, int expected)
        {
            if (list.Arguments == null || list.Arguments.Length != expected)
            {
                throw new InvalidOperationException(list.Head + " 需要 " + expected + " 个参数。");
            }
        }
    }
}
