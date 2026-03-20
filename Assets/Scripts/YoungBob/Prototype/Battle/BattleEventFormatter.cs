using System;

namespace YoungBob.Prototype.Battle
{
    internal static class BattleEventFormatter
    {
        public static string Format(BattleEvent battleEvent, bool richText)
        {
            if (battleEvent == null || string.IsNullOrWhiteSpace(battleEvent.eventId))
            {
                return string.Empty;
            }

            var actor = richText ? BattleTextHelper.Actor(battleEvent.actor) : battleEvent.actor;
            var target = richText ? BattleTextHelper.Unit(battleEvent.target) : battleEvent.target;
            var card = richText ? BattleTextHelper.Card(battleEvent.cardId) : battleEvent.cardId;
            var status = richText ? BattleTextHelper.Card(battleEvent.statusId) : battleEvent.statusId;
            var amountDamage = richText ? BattleTextHelper.DamageText(battleEvent.amount) : (battleEvent.amount + "点伤害");
            var amountHeal = richText ? BattleTextHelper.HealText(battleEvent.amount) : (battleEvent.amount + "点治疗");
            var amountArmor = richText ? BattleTextHelper.ArmorText(battleEvent.amount) : (battleEvent.amount + "点护甲");
            var amountDraw = richText ? BattleTextHelper.DrawText(battleEvent.amount) : (battleEvent.amount + "张牌");
            var amountEnergy = richText ? BattleTextHelper.EnergyText(battleEvent.amount) : (battleEvent.amount + "点能量");
            var area = richText ? BattleTextHelper.AreaText(battleEvent.area) : AreaTextPlain(battleEvent.area);

            switch (battleEvent.eventId)
            {
                case "player_end_turn":
                    return actor + " 结束了回合。";
                case "round_start":
                    return richText
                        ? "<color=#E6C36A>第 " + battleEvent.turn + " 回合开始。</color>"
                        : "第 " + battleEvent.turn + " 回合开始。";
                case "part_broken":
                    return richText
                        ? "<color=#FFC874>部位破坏：</color> " + target
                        : "部位破坏: " + battleEvent.target;
                case "encounter_cleared":
                    return richText
                        ? "<color=#7FD67F>遭遇已完成：</color> " + battleEvent.context
                        : "遭遇已完成: " + battleEvent.context;
                case "stage_cleared":
                    return richText
                        ? "<color=#7FD67F>关卡已完成：</color> " + battleEvent.context
                        : "关卡已完成: " + battleEvent.context;
                case "encounter_next":
                    return richText
                        ? "<color=#7FD67F>下一场遭遇：</color> " + battleEvent.context + "（" + battleEvent.amount + "/" + battleEvent.amount2 + "）"
                        : "下一场遭遇: " + battleEvent.context + "（" + battleEvent.amount + "/" + battleEvent.amount2 + "）";
                case "monster_action":
                    return richText
                        ? "<color=#8A8A8A>怪物行动：</color> " + battleEvent.context
                        : "怪物行动: " + battleEvent.context;
                case "monster_ai":
                    return richText
                        ? "<color=#8A8A8A>怪物AI：</color> " + battleEvent.context
                        : "怪物AI: " + battleEvent.context;
                case "monster_wait":
                    return target + " 正在寻找出手时机。";
                case "card_damage":
                    return actor + " 使用 " + card + " 攻击 " + target + "，造成 " + amountDamage + "。";
                case "draw_cards":
                    return target + " 抽了 " + amountDraw + "。";
                case "heal":
                    return target + " 恢复了 " + amountHeal + "。";
                case "gain_armor":
                    return target + " 获得了 " + amountArmor + "。";
                case "lose_armor":
                    return target + " 失去了 " + amountArmor + "。";
                case "apply_status":
                    return target + " 获得 " + status + " x" + battleEvent.amount + "（总计 " + battleEvent.amount2 + "）。";
                case "threat_change":
                    return target + " 仇恨变化 " + (battleEvent.amount >= 0 ? "+" : string.Empty) + battleEvent.amount + "（仇恨值 " + battleEvent.amount2 + "，层级 " + battleEvent.turn + "）。";
                case "gain_secret":
                    return target + " 获得奥秘 " + status + " x" + battleEvent.amount + "（总计 " + battleEvent.amount2 + "）。";
                case "move_area":
                    return actor + " 移动到了 " + area + "。";
                case "refund_energy":
                    return target + " 返还了 " + amountEnergy + "。";
                case "damage_by_armor":
                    return target + " 受到" + amountDamage + "，来自护甲冲击。";
                case "apply_vulnerable":
                    return target + " 获得易伤 x" + battleEvent.amount + "。";
                case "modify_energy":
                    return target + " 能量变化 " + battleEvent.amount + "。";
                case "lose_hp":
                    return target + " 失去 " + amountDamage + "。";
                case "recycle_from_discard":
                    return target + " 从弃牌堆回收了 " + card + "。";
                case "copy_and_plunder":
                    return actor + " 使用 " + card + " 对 " + target + " 进行掠夺并复制了一张。";
                case "exhaust_card":
                    return target + " 消耗了 " + card + "。";
                case "monster_hit":
                    return target + " 使用 " + card + " 命中 " + actor + "，造成 " + amountDamage + "。";
                case "monster_gain_card":
                    return target + " 因 " + card + " 获得 " + status + "。";
                case "monster_no_hit":
                    return target + " 使用 " + card + "，但没有命中任何目标。";
                case "poison_damage":
                    return target + " 受到" + amountDamage + "，来自中毒。";
                case "secret_guard_redirect":
                    return target + " 触发奥秘 " + status + "，代替 " + actor + " 承受了这次攻击。";
                case "secret_sidestep":
                    return target + " 触发奥秘 " + status + "，在被 " + card + " 命中后切换到 " + area + "。";
                case "secret_moved":
                    return target + " 从 " + battleEvent.context + " 移动到 " + area + "。";
                case "secret_counter":
                    return target + " 触发奥秘 " + status + "，对 " + actor + " 造成 " + amountDamage + "。";
                case "secret_gain_strength":
                    return target + " 触发奥秘 " + status + "，获得 " + BattleTextHelper.Card(BattleStatusSystem.StrengthStatusId) + " x" + battleEvent.amount + "（持续1回合）。";
                case "secret_gain_armor":
                    return target + " 触发奥秘 " + status + "，获得 " + amountArmor + "（当前护甲 " + battleEvent.amount2 + "）。";
                case "no_monster":
                    return "当前没有怪物。";
                case "team_defeated":
                    return "队伍被击败了。";
                default:
                    return battleEvent.eventId;
            }
        }

        private static string AreaTextPlain(BattleArea area)
        {
            switch (area)
            {
                case BattleArea.West:
                    return "西侧";
                case BattleArea.East:
                    return "东侧";
                case BattleArea.Middle:
                    return "中间";
                default:
                    return "未知";
            }
        }
    }
}
