using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Battle
{
    internal static class BattleMechanics
    {
        public static int ApplyDamage(PlayerBattleState target, int amount)
        {
            var incoming = Math.Max(0, amount);
            if (target.vulnerableStacks > 0)
            {
                incoming += (incoming + 1) / 2;
                target.vulnerableStacks = Math.Max(0, target.vulnerableStacks - 1);
            }

            var mitigatedByArmor = Math.Min(target.armor, incoming);
            target.armor -= mitigatedByArmor;
            var remainingDamage = incoming - mitigatedByArmor;
            target.hp = Math.Max(0, target.hp - remainingDamage);
            return remainingDamage;
        }

        public static int ApplyDamageToPart(BattleState state, MonsterPartState part, int amount, BattleCommandResult result)
        {
            var incoming = Math.Max(0, amount);
            var appliedToCore = ApplyDamageToMonsterCore(state == null ? null : state.monster, incoming);

            // Part HP is a break gauge. Once broken, it stays at 0 and no longer blocks further damage.
            if (!part.isBroken && part.hp > 0)
            {
                part.hp = Math.Max(0, part.hp - incoming);
            }

            if (!part.isBroken && part.hp <= 0)
            {
                part.isBroken = true;
                if (part.lootOnBreak != null)
                {
                    for (var i = 0; i < part.lootOnBreak.Length; i++)
                    {
                        state.loot.Add(part.lootOnBreak[i]);
                    }
                }

                result.events.Add(new BattleEvent
                {
                    eventId = "part_broken",
                    target = part.displayName
                });
            }

            return appliedToCore;
        }

        public static int ApplyDamageToMonsterCore(MonsterBattleState monster, int amount)
        {
            if (monster == null)
            {
                return 0;
            }

            var incoming = Math.Max(0, amount);
            var coreBefore = monster.coreHp;
            var applied = Math.Min(coreBefore, incoming);
            monster.coreHp = Math.Max(0, coreBefore - applied);
            return applied;
        }

        public static int GetEffectiveEnergyCost(BattleCardState card, Data.CardDefinition definition)
        {
            if (definition == null)
            {
                return 0;
            }

            var delta = card == null ? 0 : card.costDelta;
            return Math.Max(0, definition.energyCost + delta);
        }

        public static int Heal(PlayerBattleState player, int amount)
        {
            var previousHp = player.hp;
            player.hp = Math.Min(player.maxHp, player.hp + amount);
            return player.hp - previousHp;
        }

        public static int DrawCards(BattleState state, PlayerBattleState player, int count)
        {
            var drawn = 0;
            for (var i = 0; i < count; i++)
            {
                if (!TryDrawOne(state, player, out var addedToHand))
                {
                    break;
                }

                if (addedToHand)
                {
                    drawn += 1;
                }
            }

            return drawn;
        }

        public static bool TryDrawOne(BattleState state, PlayerBattleState player, out bool addedToHand)
        {
            addedToHand = false;
            if (player.drawPile.Count == 0 && player.discardPile.Count > 0)
            {
                player.drawPile.AddRange(player.discardPile);
                player.discardPile.Clear();
                Shuffle(player.drawPile, state.randomSeed ^ state.turnIndex ^ player.playerId.GetHashCode());
            }

            if (player.drawPile.Count == 0)
            {
                return false;
            }

            var nextCard = player.drawPile[0];
            player.drawPile.RemoveAt(0);
            if (player.hand.Count >= BattleEngine.MaxHandSize)
            {
                player.discardPile.Add(nextCard);
                return true;
            }

            player.hand.Add(nextCard);
            addedToHand = true;
            return true;
        }

        public static void AddCardToHandOrDiscard(BattleState state, PlayerBattleState player, string cardId, bool forceIntoHand = false)
        {
            if (state == null || player == null || string.IsNullOrWhiteSpace(cardId))
            {
                return;
            }

            var card = new BattleCardState
            {
                instanceId = player.playerId + "_" + cardId + "_" + state.turnIndex + "_" + Guid.NewGuid().ToString("N"),
                cardId = cardId,
                costDelta = 0
            };

            if (player.hand.Count >= BattleEngine.MaxHandSize)
            {
                if (forceIntoHand && player.hand.Count > 0)
                {
                    var displaced = player.hand[0];
                    player.hand.RemoveAt(0);
                    player.discardPile.Add(displaced);
                }
                else
                {
                    player.discardPile.Add(card);
                    return;
                }
            }

            if (player.hand.Count >= BattleEngine.MaxHandSize)
            {
                player.discardPile.Add(card);
                return;
            }

            player.hand.Add(card);
        }

        public static void Shuffle(List<BattleCardState> list, int seed)
        {
            var random = new Random(seed);
            for (var i = list.Count - 1; i > 0; i--)
            {
                var swapIndex = random.Next(i + 1);
                var temp = list[i];
                list[i] = list[swapIndex];
                list[swapIndex] = temp;
            }
        }

        public static void TryResolveBattleEnd(BattleState state, BattleCommandResult result)
        {
            if (state.monster != null && state.monster.coreHp > 0)
            {
                return;
            }

            state.phase = BattlePhase.Victory;
            state.currentPrompt = "胜利";
            result.events.Add(new BattleEvent
            {
                eventId = "stage_cleared",
                context = "胜利"
            });
        }
    }

}
