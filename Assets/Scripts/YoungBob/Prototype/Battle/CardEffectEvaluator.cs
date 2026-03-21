using System;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.Battle
{
    internal static class CardEffectEvaluator
    {
        public static double EvaluateNumber(CardEffectExecutionContext context, SExpressionNode node, CardEffectTargetRef target)
        {
            if (node is SExpressionNumberNode number)
            {
                return number.Value;
            }

            if (node is SExpressionSymbolNode symbol)
            {
                return ResolveNumericSymbol(context, symbol.Value, target);
            }

            var list = node as SExpressionListNode;
            if (list == null)
            {
                throw new InvalidOperationException("数值表达式无效。");
            }

            switch (list.Head)
            {
                case "+":
                {
                    var sum = 0d;
                    for (var i = 0; i < list.Arguments.Length; i++)
                    {
                        sum += EvaluateNumber(context, list.Arguments[i], target);
                    }

                    return sum;
                }
                case "-":
                    if (list.Arguments.Length == 1)
                    {
                        return -EvaluateNumber(context, list.Arguments[0], target);
                    }

                    if (list.Arguments.Length >= 2)
                    {
                        var value = EvaluateNumber(context, list.Arguments[0], target);
                        for (var i = 1; i < list.Arguments.Length; i++)
                        {
                            value -= EvaluateNumber(context, list.Arguments[i], target);
                        }

                        return value;
                    }

                    break;
                case "*":
                {
                    var product = 1d;
                    for (var i = 0; i < list.Arguments.Length; i++)
                    {
                        product *= EvaluateNumber(context, list.Arguments[i], target);
                    }

                    return product;
                }
                case "/":
                    if (list.Arguments.Length == 2)
                    {
                        var denominator = EvaluateNumber(context, list.Arguments[1], target);
                        return Math.Abs(denominator) < 0.0001d ? 0d : EvaluateNumber(context, list.Arguments[0], target) / denominator;
                    }

                    break;
                case "min":
                {
                    var value = EvaluateNumber(context, list.Arguments[0], target);
                    for (var i = 1; i < list.Arguments.Length; i++)
                    {
                        value = Math.Min(value, EvaluateNumber(context, list.Arguments[i], target));
                    }

                    return value;
                }
                case "max":
                {
                    var value = EvaluateNumber(context, list.Arguments[0], target);
                    for (var i = 1; i < list.Arguments.Length; i++)
                    {
                        value = Math.Max(value, EvaluateNumber(context, list.Arguments[i], target));
                    }

                    return value;
                }
            }

            throw new InvalidOperationException("不支持的数值表达式：" + list.Head);
        }

        public static bool EvaluateBoolean(CardEffectExecutionContext context, SExpressionNode node, CardEffectTargetRef target)
        {
            if (node is SExpressionSymbolNode symbol)
            {
                return ResolveBooleanSymbol(context, symbol.Value, target);
            }

            var list = node as SExpressionListNode;
            if (list == null || list.Arguments.Length != 2)
            {
                throw new InvalidOperationException("布尔表达式无效。");
            }

            var left = EvaluateNumber(context, list.Arguments[0], target);
            var right = EvaluateNumber(context, list.Arguments[1], target);
            switch (list.Head)
            {
                case ">":
                    return left > right;
                case "<":
                    return left < right;
                case ">=":
                    return left >= right;
                case "<=":
                    return left <= right;
                case "=":
                    return Math.Abs(left - right) < 0.0001d;
                default:
                    throw new InvalidOperationException("不支持的布尔表达式：" + list.Head);
            }
        }

        private static double ResolveNumericSymbol(CardEffectExecutionContext context, string symbol, CardEffectTargetRef target)
        {
            switch (symbol)
            {
                case "self-armor":
                    return context.actingPlayer.armor;
                case "cards-played-this-turn":
                    return context.actingPlayer.cardsPlayedThisTurn;
                case "target-poison":
                    if (target == null)
                    {
                        return 0;
                    }

                    if (target.IsPlayer)
                    {
                        return BattleStatusSystem.GetStacks(target.Player.statuses, BattleStatusSystem.PoisonStatusId);
                    }

                    return BattleStatusSystem.GetStacks(context.state.monster == null ? null : context.state.monster.statuses, BattleStatusSystem.PoisonStatusId);
                case "target-hp":
                    if (target == null)
                    {
                        return 0;
                    }

                    return target.IsPlayer ? target.Player.hp : target.Part.hp;
                case "last-damage-dealt":
                    return context.lastResult.damageDealt;
                case "energy":
                    return context.actingPlayer.energy;
                default:
                    throw new InvalidOperationException("未知数值变量：" + symbol);
            }
        }

        private static bool ResolveBooleanSymbol(CardEffectExecutionContext context, string symbol, CardEffectTargetRef target)
        {
            switch (symbol)
            {
                case "last-killed-target?":
                    return context.lastResult.killedTarget;
                case "target-is-player?":
                    return target != null && target.IsPlayer;
                case "target-is-monster-part?":
                    return target != null && !target.IsPlayer;
                case "has-target?":
                    return target != null;
                default:
                    throw new InvalidOperationException("未知布尔变量：" + symbol);
            }
        }
    }
}
