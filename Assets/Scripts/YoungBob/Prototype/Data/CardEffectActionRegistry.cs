using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Data
{
    internal enum CardEffectValueKind
    {
        Target,
        Number,
        Symbol
    }

    internal sealed class CardEffectActionMetadata
    {
        public string Name;
        public string RuntimeOp;
        public CardEffectValueKind[] ArgumentKinds;
        public int NumericArgumentStartIndex;
        public string SymbolArgumentError;

        public int Arity => ArgumentKinds == null ? 0 : ArgumentKinds.Length;
    }

    internal static class CardEffectActionRegistry
    {
        private static readonly Dictionary<string, CardEffectActionMetadata> Actions =
            new Dictionary<string, CardEffectActionMetadata>(StringComparer.Ordinal)
            {
                ["damage"] = AmountAction("damage", "Damage"),
                ["heal"] = AmountAction("heal", "Heal"),
                ["draw"] = AmountAction("draw", "Draw"),
                ["gain-armor"] = AmountAction("gain-armor", "GainArmor"),
                ["lose-hp"] = AmountAction("lose-hp", "LoseHp"),
                ["modify-threat"] = AmountAction("modify-threat", "ModifyThreat"),
                ["modify-energy"] = AmountAction("modify-energy", "ModifyEnergy"),
                ["apply-status"] = new CardEffectActionMetadata
                {
                    Name = "apply-status",
                    RuntimeOp = "ApplyStatus",
                    ArgumentKinds = new[] { CardEffectValueKind.Target, CardEffectValueKind.Symbol, CardEffectValueKind.Number },
                    NumericArgumentStartIndex = 2,
                    SymbolArgumentError = "apply-status 的状态参数必须是 symbol。"
                },
                ["add-secret"] = new CardEffectActionMetadata
                {
                    Name = "add-secret",
                    RuntimeOp = "AddSecret",
                    ArgumentKinds = new[] { CardEffectValueKind.Target, CardEffectValueKind.Symbol, CardEffectValueKind.Number },
                    NumericArgumentStartIndex = 2,
                    SymbolArgumentError = "add-secret 的状态参数必须是 symbol。"
                },
                ["copy-and-plunder"] = new CardEffectActionMetadata
                {
                    Name = "copy-and-plunder",
                    RuntimeOp = "CopyAndPlunder",
                    ArgumentKinds = new[] { CardEffectValueKind.Target }
                },
                ["move-area"] = new CardEffectActionMetadata
                {
                    Name = "move-area",
                    RuntimeOp = "MoveArea",
                    ArgumentKinds = new[] { CardEffectValueKind.Symbol },
                    SymbolArgumentError = "move-area 的目的地参数必须是 symbol。"
                },
                ["recycle-discard-to-hand"] = new CardEffectActionMetadata
                {
                    Name = "recycle-discard-to-hand",
                    RuntimeOp = "RecycleDiscardToHand",
                    ArgumentKinds = new[] { CardEffectValueKind.Target, CardEffectValueKind.Number, CardEffectValueKind.Number },
                    NumericArgumentStartIndex = 1
                },
                ["exhaust-from-hand"] = new CardEffectActionMetadata
                {
                    Name = "exhaust-from-hand",
                    RuntimeOp = "ExhaustFromHand",
                    ArgumentKinds = new[] { CardEffectValueKind.Target, CardEffectValueKind.Number },
                    NumericArgumentStartIndex = 1
                }
            };

        public static bool TryGet(string actionName, out CardEffectActionMetadata metadata)
        {
            return Actions.TryGetValue(actionName, out metadata);
        }

        private static CardEffectActionMetadata AmountAction(string name, string runtimeOp)
        {
            return new CardEffectActionMetadata
            {
                Name = name,
                RuntimeOp = runtimeOp,
                ArgumentKinds = new[] { CardEffectValueKind.Target, CardEffectValueKind.Number },
                NumericArgumentStartIndex = 1
            };
        }
    }
}
