namespace YoungBob.Prototype.Battle
{
    internal static class BattleTextHelper
    {
        // Battle log token palette:
        // - unit / player / monster names: stable cyan
        // - card names: violet
        // - status / secret names: amber
        // - numeric effects: semantic colors per effect type
        private const string UnitNameColor = "#7FD3FF";
        private const string CardNameColor = "#C9A0FF";
        private const string StatusNameColor = "#F6C978";
        private const string DamageColor = "#FF6B6B";
        private const string HealColor = "#6EDC8C";
        private const string ArmorColor = "#73BFFF";
        private const string ResourceColor = "#F7E08A";
        private const string AreaColor = "#A6D8FF";

        public static string Name(string value)
        {
            return "<color=" + UnitNameColor + ">" + value + "</color>";
        }

        public static string Actor(string value)
        {
            return Name(value);
        }

        public static string Unit(string value)
        {
            return Name(value);
        }

        public static string Card(string value)
        {
            return "<color=" + CardNameColor + ">" + value + "</color>";
        }

        public static string Status(string value)
        {
            return "<color=" + StatusNameColor + ">" + value + "</color>";
        }

        public static string DamageText(int value)
        {
            return "<color=" + DamageColor + ">" + value + "点伤害</color>";
        }

        public static string HealText(int value)
        {
            return "<color=" + HealColor + ">" + value + "点治疗</color>";
        }

        public static string ArmorText(int value)
        {
            return "<color=" + ArmorColor + ">" + value + "点护甲</color>";
        }

        public static string DrawText(int value)
        {
            return "<color=" + ResourceColor + ">" + value + "张牌</color>";
        }

        public static string EnergyText(int value)
        {
            return "<color=" + ResourceColor + ">" + value + "点能量</color>";
        }

        public static string AreaText(BattleArea area)
        {
            switch (area)
            {
                case BattleArea.West:
                    return "<color=" + AreaColor + ">西侧</color>";
                case BattleArea.East:
                    return "<color=" + AreaColor + ">东侧</color>";
                case BattleArea.Middle:
                    return "<color=" + AreaColor + ">中间</color>";
                default:
                    return "<color=" + AreaColor + ">未知</color>";
            }
        }
    }
}
