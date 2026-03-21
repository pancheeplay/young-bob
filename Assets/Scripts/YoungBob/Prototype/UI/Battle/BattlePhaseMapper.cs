using YoungBob.Prototype.Battle;

namespace YoungBob.Prototype.UI.Battle
{
    internal static class BattlePhaseMapper
    {
        public static string GetTitle(BattlePhase phase)
        {
            switch (phase)
            {
                case BattlePhase.PlayerTurn:
                    return "我方回合";
                case BattlePhase.MonsterTurnStart:
                    return "敌方回合";
                case BattlePhase.MonsterTurnResolve:
                    return "敌人行动";
                case BattlePhase.PlayerTurnStart:
                    return "我方回合开始";
                case BattlePhase.Victory:
                    return "胜利";
                case BattlePhase.Defeat:
                    return "失败";
                default:
                    return "战斗中";
            }
        }

        public static string GetColor(BattlePhase phase)
        {
            switch (phase)
            {
                case BattlePhase.PlayerTurn:
                case BattlePhase.PlayerTurnStart:
                    return "#40FF80";
                case BattlePhase.MonsterTurnStart:
                case BattlePhase.MonsterTurnResolve:
                case BattlePhase.Defeat:
                    return "#FF6060";
                case BattlePhase.Victory:
                    return "#FFD166";
                default:
                    return "#D7DCE2";
            }
        }
    }
}
