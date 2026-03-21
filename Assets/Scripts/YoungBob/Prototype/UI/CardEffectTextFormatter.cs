using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using YoungBob.Prototype.Data;

namespace YoungBob.Prototype.UI
{
    internal static class CardEffectTextFormatter
    {
        public static string BuildEffectSummary(CardDefinition cardDef)
        {
            if (cardDef == null || cardDef.parsedEffects == null)
            {
                return "无效果";
            }

            var summary = FormatEffectClause(cardDef.parsedEffects);
            if (string.IsNullOrWhiteSpace(summary))
            {
                return "无效果";
            }

            return summary;
        }

        public static string BuildEffectsTargetHint(CardDefinition cardDef)
        {
            if (cardDef == null || cardDef.parsedEffects == null)
            {
                return string.Empty;
            }

            bool hasCardTarget = false;
            bool hasSelf = false;
            bool hasAllEnemies = false;
            bool hasAllAllies = false;
            bool hasOther = false;

            foreach (var action in CardEffectCompiler.EnumerateActions(cardDef.parsedEffects))
            {
                if (action == null || action.Arguments == null || action.Arguments.Length == 0)
                {
                    continue;
                }

                if (!(action.Arguments[0] is SExpressionSymbolNode targetNode))
                {
                    hasOther = true;
                    continue;
                }

                switch (targetNode.Value)
                {
                    case "target":
                        hasCardTarget = true;
                        break;
                    case "self":
                        hasSelf = true;
                        break;
                    case "all-enemies":
                        hasAllEnemies = true;
                        break;
                    case "all-allies":
                        hasAllAllies = true;
                        break;
                    default:
                        hasOther = true;
                        break;
                }
            }

            var parts = new List<string>();
            if (hasCardTarget) parts.Add("需指向卡牌目标");
            if (hasSelf) parts.Add("包含自身效果");
            if (hasAllEnemies) parts.Add("包含全敌方效果");
            if (hasAllAllies) parts.Add("包含全友方效果");
            if (hasOther) parts.Add("包含特殊目标效果");

            return parts.Count == 0 ? string.Empty : "目标提示：" + string.Join("，", parts);
        }

        public static string BuildDebugSummary(CardDefinition cardDef)
        {
            if (cardDef == null || cardDef.parsedEffects == null)
            {
                return "None";
            }

            var actions = new List<string>();
            foreach (var action in CardEffectCompiler.EnumerateActions(cardDef.parsedEffects))
            {
                actions.Add(BuildInlineActionSummary(action));
            }

            return actions.Count == 0 ? "None" : string.Join(", ", actions.ToArray());
        }

        private static string FormatEffectClause(SExpressionNode node)
        {
            var list = node as SExpressionListNode;
            if (list == null)
            {
                return FormatExpr(node);
            }

            switch (list.Head)
            {
                case "do":
                    return JoinClauses(list.Arguments);
                case "if":
                    return "若" + FormatBool(list.Arguments[0]) + "，" + FormatEffectClause(list.Arguments[1]);
                case "repeat":
                    return "重复" + FormatExpr(list.Arguments[0]) + "次：" + FormatEffectClause(list.Arguments[1]);
                case "damage":
                    return BuildTargetedClause(list.Arguments[0], "造成" + FormatExpr(list.Arguments[1]) + "伤害");
                case "heal":
                    return BuildTargetedClause(list.Arguments[0], "恢复" + FormatExpr(list.Arguments[1]) + "点生命");
                case "draw":
                    return BuildTargetedClause(list.Arguments[0], "抽" + FormatCardCount(list.Arguments[1]));
                case "gain-armor":
                    return BuildTargetedClause(list.Arguments[0], "获得" + FormatExpr(list.Arguments[1]) + "点护甲");
                case "apply-status":
                    if (list.Arguments[1] is SExpressionSymbolNode status && string.Equals(status.Value, "Vulnerable", StringComparison.OrdinalIgnoreCase))
                    {
                        return BuildTargetedClause(list.Arguments[0], "施加" + FormatExpr(list.Arguments[2]) + "层易伤");
                    }

                    return BuildTargetedClause(list.Arguments[0], "施加" + FormatExpr(list.Arguments[2]) + "层" + FormatExpr(list.Arguments[1]));
                case "modify-energy":
                    return BuildTargetedClause(list.Arguments[0], BuildSignedText("能量", list.Arguments[1]));
                case "lose-hp":
                    return BuildTargetedClause(list.Arguments[0], "失去" + FormatExpr(list.Arguments[1]) + "点生命");
                case "modify-threat":
                    return BuildTargetedClause(list.Arguments[0], BuildSignedText("威胁", list.Arguments[1]));
                case "move-area":
                    return "移动到" + FormatMoveDestination(list.Arguments[0]);
                case "copy-and-plunder":
                    return BuildTargetedClause(list.Arguments[0], "复制并掠夺其手牌");
                case "recycle-discard-to-hand":
                    return BuildTargetedClause(list.Arguments[0], "从弃牌堆回收" + FormatCardCount(list.Arguments[1]) + "到手牌"
                        + BuildCostModifierSuffix(list.Arguments[2]));
                case "exhaust-from-hand":
                    return BuildTargetedClause(list.Arguments[0], "消耗手牌中的" + FormatCardCount(list.Arguments[1]));
                case "add-secret":
                    return BuildTargetedClause(list.Arguments[0], "获得奥秘" + FormatExpr(list.Arguments[1]) + " x" + FormatExpr(list.Arguments[2]));
                default:
                    return BuildInlineActionSummary(list);
            }
        }

        private static string BuildInlineActionSummary(SExpressionListNode list)
        {
            var parts = new List<string> { list.Head };
            for (var i = 0; i < list.Arguments.Length; i++)
            {
                parts.Add(FormatExpr(list.Arguments[i]));
            }

            return string.Join(" ", parts.ToArray());
        }

        private static string BuildTargetedClause(SExpressionNode node, string actionText)
        {
            if (!(node is SExpressionSymbolNode symbol))
            {
                return actionText;
            }

            switch (symbol.Value)
            {
                case "target":
                    return "对目标" + actionText;
                case "self":
                    return "自身" + actionText;
                case "all-enemies":
                    return "对全敌方" + actionText;
                case "all-allies":
                    return "使全友方" + actionText;
                default:
                    return symbol.Value + actionText;
            }
        }

        private static string FormatExpr(SExpressionNode node)
        {
            if (node is SExpressionNumberNode number)
            {
                return FormatNumber(number.Value);
            }

            if (node is SExpressionSymbolNode symbol)
            {
                return FormatSymbol(symbol.Value);
            }

            var list = node as SExpressionListNode;
            if (list == null)
            {
                return string.Empty;
            }

            switch (list.Head)
            {
                case "+":
                    return JoinInfix("+", list.Arguments);
                case "-":
                    return JoinInfix("-", list.Arguments);
                case "*":
                    return JoinInfix("×", list.Arguments);
                case "/":
                    return JoinInfix("÷", list.Arguments);
                case "min":
                    return "min(" + JoinComma(list.Arguments) + ")";
                case "max":
                    return "max(" + JoinComma(list.Arguments) + ")";
                default:
                    return BuildInlineActionSummary(list);
            }
        }

        private static string FormatBool(SExpressionNode node)
        {
            if (node is SExpressionSymbolNode symbol)
            {
                switch (symbol.Value)
                {
                    case "last-killed-target?":
                        return "击杀了目标";
                    case "target-is-player?":
                        return "目标是玩家";
                    case "target-is-monster-part?":
                        return "目标是怪物部位";
                    case "has-target?":
                        return "存在目标";
                }
            }

            return FormatExpr(node);
        }

        private static string BuildSignedText(string noun, SExpressionNode node)
        {
            if (node is SExpressionNumberNode number)
            {
                if (number.Value >= 0d)
                {
                    return "获得" + FormatNumber(number.Value) + "点" + noun;
                }

                return "失去" + FormatNumber(Math.Abs(number.Value)) + "点" + noun;
            }

            return "调整" + FormatExpr(node) + "点" + noun;
        }

        private static string BuildCostModifierSuffix(SExpressionNode node)
        {
            if (node is SExpressionNumberNode number && Math.Abs(number.Value) < 0.0001d)
            {
                return string.Empty;
            }

            return " (费用修正 " + FormatExpr(node) + ")";
        }

        private static string FormatCardCount(SExpressionNode node)
        {
            if (node is SExpressionNumberNode number && Math.Abs(number.Value - 1d) < 0.0001d)
            {
                return "一张牌";
            }

            return FormatExpr(node) + "张牌";
        }

        private static string JoinClauses(SExpressionNode[] arguments)
        {
            var parts = new List<string>(arguments.Length);
            for (var i = 0; i < arguments.Length; i++)
            {
                var part = FormatEffectClause(arguments[i]);
                if (!string.IsNullOrWhiteSpace(part))
                {
                    parts.Add(part);
                }
            }

            return string.Join("，", parts.ToArray());
        }

        private static string FormatMoveDestination(SExpressionNode node)
        {
            if (node is SExpressionSymbolNode symbol)
            {
                switch (symbol.Value)
                {
                    case "selected-area":
                        return "目标区域";
                    case "another-side-area":
                        return "另一侧区域";
                }
            }

            return FormatExpr(node);
        }

        private static string JoinInfix(string separator, SExpressionNode[] arguments)
        {
            var parts = new List<string>(arguments.Length);
            for (var i = 0; i < arguments.Length; i++)
            {
                parts.Add(FormatExpr(arguments[i]));
            }

            return string.Join(separator, parts.ToArray());
        }

        private static string JoinComma(SExpressionNode[] arguments)
        {
            var parts = new List<string>(arguments.Length);
            for (var i = 0; i < arguments.Length; i++)
            {
                parts.Add(FormatExpr(arguments[i]));
            }

            return string.Join(", ", parts.ToArray());
        }

        private static string FormatSymbol(string value)
        {
            switch (value)
            {
                case "self-armor":
                    return "自身护甲";
                case "cards-played-this-turn":
                    return "本回合打出牌数";
                case "target-poison":
                    return "目标中毒层数";
                case "target-hp":
                    return "目标生命";
                case "last-damage-dealt":
                    return "上次造成伤害";
                case "energy":
                    return "当前能量";
                default:
                    return value;
            }
        }

        private static string FormatNumber(double value)
        {
            if (Math.Abs(value - Math.Round(value)) < 0.0001d)
            {
                return Mathf.RoundToInt((float)value).ToString();
            }

            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
