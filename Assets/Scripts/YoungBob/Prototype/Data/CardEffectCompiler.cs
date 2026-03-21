using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Data
{
    internal static class CardEffectCompiler
    {
        public static SExpressionNode Compile(string source)
        {
            return Normalize(SExpressionParser.Parse(source));
        }

        public static IEnumerable<SExpressionListNode> EnumerateActions(SExpressionNode node)
        {
            if (!(node is SExpressionListNode list))
            {
                yield break;
            }

            if (string.Equals(list.Head, "do", StringComparison.Ordinal))
            {
                for (var i = 0; i < list.Arguments.Length; i++)
                {
                    foreach (var child in EnumerateActions(list.Arguments[i]))
                    {
                        yield return child;
                    }
                }

                yield break;
            }

            if (string.Equals(list.Head, "if", StringComparison.Ordinal))
            {
                if (list.Arguments.Length >= 2)
                {
                    foreach (var child in EnumerateActions(list.Arguments[1]))
                    {
                        yield return child;
                    }
                }

                yield break;
            }

            if (string.Equals(list.Head, "repeat", StringComparison.Ordinal))
            {
                if (list.Arguments.Length >= 2)
                {
                    foreach (var child in EnumerateActions(list.Arguments[1]))
                    {
                        yield return child;
                    }
                }

                yield break;
            }

            yield return list;
        }

        private static SExpressionNode Normalize(SExpressionNode node)
        {
            if (!(node is SExpressionListNode list))
            {
                return node;
            }

            var normalizedArguments = new SExpressionNode[list.Arguments.Length];
            for (var i = 0; i < list.Arguments.Length; i++)
            {
                normalizedArguments[i] = Normalize(list.Arguments[i]);
            }

            var normalized = new SExpressionListNode
            {
                Head = list.Head,
                Arguments = normalizedArguments
            };

            switch (normalized.Head)
            {
                case "damage-by-armor":
                    RequireArity(normalized, 2);
                    return new SExpressionListNode
                    {
                        Head = "damage",
                        Arguments = new SExpressionNode[]
                        {
                            normalized.Arguments[0],
                            new SExpressionListNode
                            {
                                Head = "*",
                                Arguments = new SExpressionNode[]
                                {
                                    normalized.Arguments[1],
                                    new SExpressionSymbolNode { Value = "self-armor" }
                                }
                            }
                        }
                    };
                case "apply-vulnerable":
                    RequireArity(normalized, 2);
                    return new SExpressionListNode
                    {
                        Head = "apply-status",
                        Arguments = new SExpressionNode[]
                        {
                            normalized.Arguments[0],
                            new SExpressionSymbolNode { Value = "Vulnerable" },
                            normalized.Arguments[1]
                        }
                    };
                default:
                    return normalized;
            }
        }

        private static void RequireArity(SExpressionListNode node, int count)
        {
            if (node.Arguments == null || node.Arguments.Length != count)
            {
                throw new InvalidOperationException("DSL 节点 " + node.Head + " 参数数量无效。");
            }
        }
    }
}
