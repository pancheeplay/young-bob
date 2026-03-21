using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Battle
{
    internal static class BattleThreatSystem
    {
        private const int ThreatTierSize = 20;
        private const int ThreatDecayPercent = 90;
        private const int ThreatSwitchThreshold = 5;

        public static void ResetThreats(BattleState state)
        {
            if (state == null)
            {
                return;
            }

            ResetThreats(state.players);
            RefreshCurrentTarget(state);
        }

        public static void ResetThreats(List<PlayerBattleState> players)
        {
            if (players == null)
            {
                return;
            }

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                player.threatValue = 0;
                player.threatTier = 1;
            }
        }

        public static int ApplyThreatGain(PlayerBattleState player, int delta)
        {
            if (player == null || delta == 0)
            {
                return player == null ? 0 : player.threatValue;
            }

            player.threatValue = Math.Max(0, player.threatValue + delta);
            player.threatTier = CalculateThreatTier(player.threatValue);
            return player.threatValue;
        }

        public static int ApplyThreatGain(BattleState state, PlayerBattleState player, int delta)
        {
            var total = ApplyThreatGain(player, delta);
            RefreshCurrentTarget(state);
            return total;
        }

        public static int ApplyThreatFromDamage(PlayerBattleState player, int finalDamage)
        {
            return ApplyThreatGain(player, Math.Max(0, finalDamage));
        }

        public static int ApplyThreatFromDamage(BattleState state, PlayerBattleState player, int finalDamage)
        {
            var total = ApplyThreatFromDamage(player, finalDamage);
            RefreshCurrentTarget(state);
            return total;
        }

        public static void ApplyThreatDecay(List<PlayerBattleState> players)
        {
            if (players == null)
            {
                return;
            }

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                if (player.threatValue <= 0)
                {
                    player.threatTier = 1;
                    continue;
                }

                player.threatValue = (player.threatValue * ThreatDecayPercent) / 100;
                if (player.threatValue < 0)
                {
                    player.threatValue = 0;
                }

                player.threatTier = CalculateThreatTier(player.threatValue);
            }
        }

        public static void ApplyThreatDecay(BattleState state)
        {
            if (state == null)
            {
                return;
            }

            ApplyThreatDecay(state.players);
            RefreshCurrentTarget(state);
        }

        public static void RefreshCurrentTarget(BattleState state)
        {
            if (state == null || state.monster == null)
            {
                return;
            }

            var target = SelectMonsterTarget(state);
            if (target == null)
            {
                state.monster.currentThreatTargetPlayerId = null;
            }
        }

        public static PlayerBattleState SelectMonsterTarget(BattleState state)
        {
            if (state == null || state.players == null || state.players.Count == 0)
            {
                if (state != null && state.monster != null)
                {
                    state.monster.currentThreatTargetPlayerId = null;
                }
                return null;
            }

            var current = GetCurrentTarget(state);
            var best = FindBestThreatTarget(state.players);
            if (best == null)
            {
                return current != null && current.hp > 0 ? current : null;
            }

            if (current != null && current.hp > 0)
            {
                if (best.threatTier > current.threatTier)
                {
                    UpdateCurrentTarget(state, best);
                    return best;
                }

                if (best.threatTier < current.threatTier)
                {
                    return current;
                }

                if (best.threatValue > current.threatValue + ThreatSwitchThreshold)
                {
                    UpdateCurrentTarget(state, best);
                    return best;
                }

                return current;
            }

            UpdateCurrentTarget(state, best);
            return best;
        }

        public static int CalculateThreatTier(int threatValue)
        {
            if (threatValue <= 0)
            {
                return 1;
            }

            return Math.Min(3, (threatValue / ThreatTierSize) + 1);
        }

        private static PlayerBattleState FindBestThreatTarget(List<PlayerBattleState> players)
        {
            PlayerBattleState best = null;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player.hp <= 0)
                {
                    continue;
                }

                if (best == null)
                {
                    best = player;
                    continue;
                }

                if (player.threatTier > best.threatTier)
                {
                    best = player;
                    continue;
                }

                if (player.threatTier == best.threatTier && player.threatValue > best.threatValue)
                {
                    best = player;
                }
            }

            return best;
        }

        private static PlayerBattleState GetCurrentTarget(BattleState state)
        {
            if (state == null || state.monster == null || string.IsNullOrEmpty(state.monster.currentThreatTargetPlayerId))
            {
                return null;
            }

            var player = state.GetPlayer(state.monster.currentThreatTargetPlayerId);
            if (player == null || player.hp <= 0)
            {
                return null;
            }

            return player;
        }

        private static void UpdateCurrentTarget(BattleState state, PlayerBattleState target)
        {
            if (state == null || state.monster == null)
            {
                return;
            }

            state.monster.currentThreatTargetPlayerId = target == null ? null : target.playerId;
        }
    }
}
