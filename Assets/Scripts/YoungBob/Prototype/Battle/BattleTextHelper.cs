namespace YoungBob.Prototype.Battle
{
    internal static class BattleTextHelper
    {
        public static string Actor(string value)
        {
            return "<color=#6FC3FF>" + value + "</color>";
        }

        public static string Unit(string value)
        {
            return "<color=#FFD27A>" + value + "</color>";
        }

        public static string Card(string value)
        {
            return "<color=#C9A0FF>" + value + "</color>";
        }

        public static string DamageText(int value)
        {
            return "<color=#FF6B6B>" + value + "点伤害</color>";
        }

        public static string HealText(int value)
        {
            return "<color=#6EDC8C>" + value + "点治疗</color>";
        }

        public static string ArmorText(int value)
        {
            return "<color=#73BFFF>" + value + "点护甲</color>";
        }

        public static string DrawText(int value)
        {
            return "<color=#F7E08A>" + value + "张牌</color>";
        }

        public static string AreaText(BattleArea area)
        {
            switch (area)
            {
                case BattleArea.West:
                    return "<color=#A6D8FF>西侧</color>";
                case BattleArea.East:
                    return "<color=#A6D8FF>东侧</color>";
                case BattleArea.Middle:
                    return "<color=#A6D8FF>中间</color>";
                default:
                    return "<color=#A6D8FF>未知</color>";
            }
        }
    }
}
