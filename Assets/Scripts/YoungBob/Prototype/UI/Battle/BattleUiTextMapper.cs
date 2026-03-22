using System;
using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.UI.Battle
{
    internal static class BattleUiTextMapper
    {
        public static string GetTopBarPrompt(BattleState state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            switch (state.phase)
            {
                case BattlePhase.PlayerTurn:
                    return HaveAllAliveTeammatesReady(state.players) ? "等待队友结束" : "玩家行动中";
                case BattlePhase.MonsterTurnStart:
                    return "敌方回合";
                case BattlePhase.MonsterTurnResolve:
                    return "敌人行动中";
                case BattlePhase.PlayerTurnStart:
                    return "玩家行动中";
                case BattlePhase.Victory:
                    return "胜利";
                case BattlePhase.Defeat:
                    return "失败";
                default:
                    return "战斗中";
            }
        }

        public static string GetEndTurnStatus(BattleState state, string localPlayerId)
        {
            if (state == null || state.players == null)
            {
                return string.Empty;
            }

            if (state.phase == BattlePhase.PlayerTurn)
            {
                var localPlayer = FindPlayer(state, localPlayerId);
                return localPlayer != null && localPlayer.hasEndedTurn ? "等待队友" : "准备完毕后结束";
            }

            return GetTopBarPrompt(state);
        }

        private static PlayerBattleState FindPlayer(BattleState state, string playerId)
        {
            if (state == null || state.players == null || string.IsNullOrWhiteSpace(playerId))
            {
                return null;
            }

            for (var i = 0; i < state.players.Count; i++)
            {
                var player = state.players[i];
                if (player != null && string.Equals(player.playerId, playerId, StringComparison.Ordinal))
                {
                    return player;
                }
            }

            return null;
        }

        private static bool HaveAllAliveTeammatesReady(System.Collections.Generic.List<PlayerBattleState> players)
        {
            if (players == null || players.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player != null && player.hp > 0 && !player.hasEndedTurn)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
