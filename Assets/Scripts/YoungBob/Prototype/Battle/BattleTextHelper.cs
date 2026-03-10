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
            return "<color=#FF6B6B>" + value + " damage</color>";
        }

        public static string HealText(int value)
        {
            return "<color=#6EDC8C>" + value + " healing</color>";
        }

        public static string ArmorText(int value)
        {
            return "<color=#73BFFF>" + value + " armor</color>";
        }

        public static string DrawText(int value)
        {
            return "<color=#F7E08A>" + value + " card(s)</color>";
        }

        public static string AreaText(BattleArea area)
        {
            switch (area)
            {
                case BattleArea.West:
                    return "<color=#A6D8FF>West</color>";
                case BattleArea.East:
                    return "<color=#A6D8FF>East</color>";
                case BattleArea.Middle:
                    return "<color=#A6D8FF>Middle</color>";
                default:
                    return "<color=#A6D8FF>Unknown</color>";
            }
        }
    }
}
