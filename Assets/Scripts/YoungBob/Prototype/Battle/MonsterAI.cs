using System;
using System.Collections.Generic;

namespace YoungBob.Prototype.Battle
{
    internal static class MonsterAI
    {
        public static MonsterBattleState BuildMonster(MonsterDefinition definition, int seed)
        {
            if (definition == null)
            {
                return null;
            }
            var monster = new MonsterBattleState
            {
                monsterId = definition.monsterId,
                displayName = definition.monsterName,
                coreMaxHp = definition.coreMaxHp,
                coreHp = definition.coreMaxHp,
                facing = ParseFacing(definition.facing),
                stance = ParseStance(definition.stance),
                hasActiveSkill = false,
                skills = definition.skills,
                skillCooldowns = definition.skills == null ? null : new int[definition.skills.Length],
                poses = definition.poses
            };

            if (definition.parts != null)
            {
                for (var i = 0; i < definition.parts.Length; i++)
                {
                    var part = definition.parts[i];
                    monster.parts.Add(new MonsterPartState
                    {
                        partId = part.partId,
                        instanceId = definition.monsterId + "_" + part.partId + "_" + i,
                        displayName = part.displayName,
                        maxHp = part.maxHp,
                        hp = part.maxHp,
                        isBroken = false,
                        relativeZone = BattleTargetResolver.ParseZone(part.relativeZone),
                        relativeHeight = BattleTargetResolver.ParseHeight(part.relativeHeight),
                        offsetX = part.offsetX,
                        offsetY = part.offsetY,
                        width = part.width,
                        height = part.height,
                        radius = part.radius,
                        shape = part.shape,
                        lootOnBreak = part.lootOnBreak
                    });
                }
            }

            var initialPoseId = ResolveInitialPose(definition);
            ApplyMonsterPose(monster, initialPoseId);
            return monster;
        }

        public static void ResolveMonsterSkill(BattleState state, BattleCommandResult result)
        {
            var monster = state.monster;
            if (monster == null)
            {
                return;
            }

            if (monster.skills == null || monster.skills.Length == 0)
            {
                ApplyMonsterPose(monster, BattleEngine.PoseIdleId);
                var target = BattleTargetResolver.FindLowestHpAlivePlayer(state.players);
                if (target == null)
                {
                    return;
                }

                FaceArea(monster, target.area);
                result.events.Add(new BattleEvent
                {
                    message = "<color=#8A8A8A>Monster action:</color> attacking " + BattleTextHelper.Unit(target.displayName) + "."
                });
                var damage = BattleMechanics.ApplyDamage(target, 4);
                result.events.Add(new BattleEvent
                {
                    message = BattleTextHelper.Unit(monster.displayName) + " attacks " + BattleTextHelper.Unit(target.displayName) + " for " + BattleTextHelper.DamageText(damage) + "."
                });
                return;
            }

            TickSkillCooldowns(monster);

            if (!monster.hasActiveSkill || monster.activeSkill == null)
            {
                ApplyMonsterPose(monster, BattleEngine.PoseIdleId);
                var skillIndex = SelectSkillIndex(monster);
                if (skillIndex < 0 || skillIndex >= monster.skills.Length)
                {
                    result.events.Add(new BattleEvent
                    {
                        message = "<color=#8A8A8A>Monster action:</color> waiting for cooldowns."
                    });
                    result.events.Add(new BattleEvent
                    {
                        message = BattleTextHelper.Unit(monster.displayName) + " is waiting for an opening."
                    });
                    return;
                }

                var skillDef = monster.skills[skillIndex];
                var targetArea = ResolveSkillTargetArea(skillDef, state.players);
                var (targetHeight, targetsBothHeights) = ResolveSkillTargetHeight(skillDef);
                var windupTurns = skillDef.windupTurns;
                if (windupTurns <= 0 && IsChargedSkill(skillDef))
                {
                    windupTurns = 1;
                }

                FaceArea(monster, targetArea);
                monster.activeSkill = new MonsterSkillState
                {
                    skillIndex = skillIndex,
                    skillId = skillDef.skillId,
                    displayName = skillDef.name,
                    remainingWindup = windupTurns,
                    damage = skillDef.damage,
                    castPoseId = string.IsNullOrWhiteSpace(skillDef.castPoseId) ? BattleEngine.PoseIdleId : skillDef.castPoseId,
                    onHitAddCardId = skillDef.onHitAddCardId,
                    onHitApplyVulnerable = skillDef.onHitApplyVulnerable,
                    targetArea = targetArea,
                    targetHeight = targetHeight,
                    targetsBothHeights = targetsBothHeights
                };
                monster.hasActiveSkill = true;
                result.events.Add(new BattleEvent
                {
                    message = "<color=#8A8A8A>Monster AI:</color> selected " + BattleTextHelper.Card(skillDef.name) + " (cd=" + GetSkillCooldown(monster, skillIndex) + ")."
                });

                if (monster.activeSkill.remainingWindup > 0)
                {
                    ApplyMonsterPose(monster, BattleEngine.PoseChargeId);
                    result.events.Add(new BattleEvent
                    {
                        message = "<color=#8A8A8A>Monster action:</color> charging " + BattleTextHelper.Card(skillDef.name) + "."
                    });
                    result.events.Add(new BattleEvent
                    {
                        message = BattleTextHelper.Unit(monster.displayName) + " begins charging " + BattleTextHelper.Card(skillDef.name) + "."
                    });
                    return;
                }
            }

            if (monster.activeSkill.remainingWindup > 0)
            {
                monster.activeSkill.remainingWindup -= 1;
                if (monster.activeSkill.remainingWindup > 0)
                {
                    ApplyMonsterPose(monster, BattleEngine.PoseChargeId);
                    result.events.Add(new BattleEvent
                    {
                        message = "<color=#8A8A8A>Monster action:</color> charging " + BattleTextHelper.Card(monster.activeSkill.displayName) + "."
                    });
                    result.events.Add(new BattleEvent
                    {
                        message = BattleTextHelper.Unit(monster.displayName) + " continues charging " + BattleTextHelper.Card(monster.activeSkill.displayName) + "."
                    });
                    return;
                }
            }

            ApplyMonsterPose(monster, string.IsNullOrWhiteSpace(monster.activeSkill.castPoseId) ? BattleEngine.PoseIdleId : monster.activeSkill.castPoseId);
            result.events.Add(new BattleEvent
            {
                message = "<color=#8A8A8A>Monster action:</color> using " + BattleTextHelper.Card(monster.activeSkill.displayName) + "."
            });
            ExecuteMonsterSkill(state, monster.activeSkill, result);
            StartSkillCooldown(monster, monster.activeSkill);
            monster.hasActiveSkill = false;
            monster.activeSkill = null;
        }

        private static void TickSkillCooldowns(MonsterBattleState monster)
        {
            if (monster == null || monster.skillCooldowns == null)
            {
                return;
            }

            for (var i = 0; i < monster.skillCooldowns.Length; i++)
            {
                if (monster.skillCooldowns[i] > 0)
                {
                    monster.skillCooldowns[i] -= 1;
                }
            }
        }

        private static int SelectSkillIndex(MonsterBattleState monster)
        {
            if (monster == null || monster.skills == null || monster.skills.Length == 0)
            {
                return -1;
            }

            EnsureSkillCooldownState(monster);

            var fallbackBasic = -1;
            var fallbackAny = -1;
            for (var i = 0; i < monster.skills.Length; i++)
            {
                var skill = monster.skills[i];
                if (skill == null)
                {
                    continue;
                }

                var isReady = monster.skillCooldowns[i] <= 0;
                var isBasic = IsBasicSkill(skill);
                if (isReady && !isBasic)
                {
                    return i;
                }

                if (isReady && isBasic && fallbackBasic < 0)
                {
                    fallbackBasic = i;
                }

                if (isReady && fallbackAny < 0)
                {
                    fallbackAny = i;
                }
            }

            if (fallbackBasic >= 0)
            {
                return fallbackBasic;
            }

            return fallbackAny;
        }

        private static void EnsureSkillCooldownState(MonsterBattleState monster)
        {
            if (monster == null || monster.skills == null)
            {
                return;
            }

            if (monster.skillCooldowns == null || monster.skillCooldowns.Length != monster.skills.Length)
            {
                monster.skillCooldowns = new int[monster.skills.Length];
            }
        }

        private static void StartSkillCooldown(MonsterBattleState monster, MonsterSkillState usedSkill)
        {
            if (monster == null || usedSkill == null || monster.skills == null)
            {
                return;
            }

            EnsureSkillCooldownState(monster);

            var skillIndex = usedSkill.skillIndex;
            if (skillIndex < 0 || skillIndex >= monster.skills.Length)
            {
                return;
            }

            var skillDef = monster.skills[skillIndex];
            if (skillDef == null || skillDef.cooldownTurns <= 0)
            {
                return;
            }

            monster.skillCooldowns[skillIndex] = skillDef.cooldownTurns;
        }

        private static int GetSkillCooldown(MonsterBattleState monster, int skillIndex)
        {
            if (monster == null || monster.skillCooldowns == null || skillIndex < 0 || skillIndex >= monster.skillCooldowns.Length)
            {
                return 0;
            }

            return monster.skillCooldowns[skillIndex];
        }

        private static bool IsChargedSkill(MonsterSkillDefinition skillDef)
        {
            if (skillDef == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(skillDef.skillId)
                && skillDef.skillId.IndexOf("charged", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(skillDef.name)
                && (skillDef.name.IndexOf("蓄力", StringComparison.OrdinalIgnoreCase) >= 0
                    || skillDef.name.IndexOf("charge", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            return false;
        }

        private static bool IsBasicSkill(MonsterSkillDefinition skillDef)
        {
            if (skillDef == null)
            {
                return false;
            }

            return ContainsIgnoreCase(skillDef.skillId, "normal")
                || ContainsIgnoreCase(skillDef.skillId, "basic")
                || ContainsIgnoreCase(skillDef.name, "普通")
                || ContainsIgnoreCase(skillDef.name, "normal")
                || ContainsIgnoreCase(skillDef.name, "basic");
        }

        private static bool ContainsIgnoreCase(string source, string token)
        {
            return !string.IsNullOrEmpty(source)
                && source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void FaceArea(MonsterBattleState monster, BattleArea area)
        {
            if (monster == null)
            {
                return;
            }

            switch (area)
            {
                case BattleArea.West:
                    monster.facing = BattleFacing.West;
                    break;
                case BattleArea.East:
                    monster.facing = BattleFacing.East;
                    break;
            }
        }

        public static string ResolveInitialPose(MonsterDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(definition.defaultPose))
            {
                return definition.defaultPose;
            }

            if (definition.poses != null && definition.poses.Length > 0)
            {
                return definition.poses[0].poseId;
            }

            return null;
        }

        public static void ApplyMonsterPose(MonsterBattleState monster, string poseId)
        {
            if (monster == null || string.IsNullOrEmpty(poseId) || monster.poses == null || monster.poses.Length == 0)
            {
                return;
            }

            var pose = FindPose(monster.poses, poseId);
            if (pose == null || pose.parts == null || pose.parts.Length == 0)
            {
                return;
            }

            monster.currentPoseId = pose.poseId;
            for (var i = 0; i < pose.parts.Length; i++)
            {
                var partPose = pose.parts[i];
                var part = FindPartById(monster, partPose.partId);
                if (part == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(partPose.relativeZone))
                {
                    part.relativeZone = BattleTargetResolver.ParseZone(partPose.relativeZone);
                }

                if (!string.IsNullOrEmpty(partPose.relativeHeight))
                {
                    part.relativeHeight = BattleTargetResolver.ParseHeight(partPose.relativeHeight);
                }

                part.offsetX = partPose.offsetX;
                part.offsetY = partPose.offsetY;
                part.width = partPose.width;
                part.height = partPose.height;
                part.radius = partPose.radius;

                if (!string.IsNullOrEmpty(partPose.shape))
                {
                    part.shape = partPose.shape;
                }
            }
        }

        public static MonsterPoseDefinition FindPose(MonsterPoseDefinition[] poses, string poseId)
        {
            for (var i = 0; i < poses.Length; i++)
            {
                var pose = poses[i];
                if (pose != null && string.Equals(pose.poseId, poseId, StringComparison.OrdinalIgnoreCase))
                {
                    return pose;
                }
            }

            return null;
        }

        public static MonsterPartState FindPartById(MonsterBattleState monster, string partId)
        {
            if (monster == null || monster.parts == null || string.IsNullOrEmpty(partId))
            {
                return null;
            }

            for (var i = 0; i < monster.parts.Count; i++)
            {
                var part = monster.parts[i];
                if (part != null && string.Equals(part.partId, partId, StringComparison.OrdinalIgnoreCase))
                {
                    return part;
                }
            }

            return null;
        }

        public static void ExecuteMonsterSkill(BattleState state, MonsterSkillState skill, BattleCommandResult result)
        {
            var hitCount = 0;
            for (var i = 0; i < state.players.Count; i++)
            {
                var player = state.players[i];
                if (player.hp <= 0)
                {
                    continue;
                }

                if (player.area != skill.targetArea)
                {
                    continue;
                }

                if (!skill.targetsBothHeights && player.height != skill.targetHeight)
                {
                    continue;
                }

                var damage = BattleMechanics.ApplyDamage(player, skill.damage);
                result.events.Add(new BattleEvent
                {
                    message = BattleTextHelper.Unit(state.monster.displayName) + " hits " + BattleTextHelper.Unit(player.displayName) + " for " + BattleTextHelper.DamageText(damage) + " with " + BattleTextHelper.Card(skill.displayName) + "."
                });

                if (!string.IsNullOrWhiteSpace(skill.onHitAddCardId))
                {
                    BattleMechanics.AddCardToHandOrDiscard(state, player, skill.onHitAddCardId, forceIntoHand: true);
                    result.events.Add(new BattleEvent
                    {
                        message = BattleTextHelper.Unit(player.displayName) + " received " + BattleTextHelper.Card(skill.onHitAddCardId) + " due to " + BattleTextHelper.Card(skill.displayName) + "."
                    });
                }

                if (skill.onHitApplyVulnerable > 0)
                {
                    player.vulnerableStacks += skill.onHitApplyVulnerable;
                    result.events.Add(new BattleEvent
                    {
                        message = BattleTextHelper.Unit(player.displayName) + " gained Vulnerable x" + skill.onHitApplyVulnerable + "."
                    });
                }

                hitCount += 1;
            }

            if (hitCount == 0)
            {
                result.events.Add(new BattleEvent
                {
                    message = BattleTextHelper.Unit(state.monster.displayName) + " used " + BattleTextHelper.Card(skill.displayName) + " but hit nobody."
                });
            }
        }

        public static BattleArea ResolveSkillTargetArea(MonsterSkillDefinition skill, List<PlayerBattleState> players)
        {
            if (skill == null)
            {
                return BattleArea.West;
            }

            if (!string.IsNullOrEmpty(skill.targetArea) && !string.Equals(skill.targetArea, "Any", StringComparison.OrdinalIgnoreCase))
            {
                return ParseArea(skill.targetArea);
            }

            var (targetHeight, targetsBothHeights) = ResolveSkillTargetHeight(skill);
            var westCount = 0;
            var eastCount = 0;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].hp <= 0)
                {
                    continue;
                }

                if (!targetsBothHeights && players[i].height != targetHeight)
                {
                    continue;
                }

                if (players[i].area == BattleArea.West)
                {
                    westCount += 1;
                }
                else if (players[i].area == BattleArea.East)
                {
                    eastCount += 1;
                }
            }

            if (westCount == 0 && eastCount == 0)
            {
                return BattleArea.West;
            }

            if (westCount >= eastCount)
            {
                return BattleArea.West;
            }

            return BattleArea.East;
        }

        public static (BattleHeight height, bool targetsBoth) ResolveSkillTargetHeight(MonsterSkillDefinition skill)
        {
            if (skill == null)
            {
                return (BattleHeight.Ground, false);
            }

            if (string.IsNullOrEmpty(skill.targetHeight))
            {
                return (BattleHeight.Ground, false);
            }

            if (string.Equals(skill.targetHeight, "Any", StringComparison.OrdinalIgnoreCase)
                || string.Equals(skill.targetHeight, "Both", StringComparison.OrdinalIgnoreCase))
            {
                return (BattleHeight.Ground, true);
            }

            return (ParseHeightRange(skill.targetHeight), false);
        }

        public static BattleFacing ParseFacing(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return BattleFacing.East;
            }

            return (BattleFacing)Enum.Parse(typeof(BattleFacing), raw, true);
        }

        public static BattleStance ParseStance(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return BattleStance.Normal;
            }

            return (BattleStance)Enum.Parse(typeof(BattleStance), raw, true);
        }

        public static BattleArea ParseArea(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return BattleArea.West;
            }

            return (BattleArea)Enum.Parse(typeof(BattleArea), raw, true);
        }

        public static BattleHeight ParseHeightRange(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return BattleHeight.Ground;
            }

            return (BattleHeight)Enum.Parse(typeof(BattleHeight), raw, true);
        }
    }
}
