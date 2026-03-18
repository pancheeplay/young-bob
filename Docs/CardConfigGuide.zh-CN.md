# 卡牌配置指南（中文）

本文用于指导策划/开发在 `Assets/Resources/GameData/cards.json` 中新增或修改卡牌。

## 1. 数据文件位置

- 卡牌定义：`Assets/Resources/GameData/cards.json`
- 卡组定义：`Assets/Resources/GameData/decks.json`

新增卡牌通常分两步：
1. 在 `cards.json` 增加卡牌定义。
2. 在 `decks.json` 的对应卡组里加入该卡的 `id`。

## 2. 单张卡牌结构

每张卡牌是 `cards` 数组中的一个对象，结构如下：

```json
{
  "id": "assassin_quick_jab",
  "name": "快刀",
  "classTag": "Assassin",
  "targetType": "MonsterPart",
  "rangeHeights": "Ground",
  "rangeDistance": "Near",
  "energyCost": 0,
  "tags": [],
  "effects": [
    { "op": "Damage", "target": "CardTarget", "amount": 1 },
    { "op": "Draw", "target": "Self", "amount": 1 }
  ]
}
```

## 3. 顶层字段说明

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `id` | string | 是 | 卡牌唯一标识，必须全局唯一，建议小写下划线风格。 |
| `name` | string | 是 | 显示名称。建议尽量唯一，避免对局日志混淆。 |
| `classTag` | string | 是 | 职业/类别标签，如 `Assassin`、`Warrior`、`Utility`、`Curse`。 |
| `targetType` | string | 是 | 卡牌主目标类型（见下文“目标类型”）。 |
| `rangeHeights` | string | 是 | 高度限制：`Ground`/`Air`/`Both`。 |
| `rangeDistance` | string | 是 | 距离限制：`Near`/`Far`/`Both`。 |
| `rangeZones` | string | 否 | 兼容字段，通常不需要新写。 |
| `energyCost` | int | 是 | 基础能量消耗。 |
| `tags` | string[] | 否 | 预留标签，可空。 |
| `effects` | array | 是 | 效果列表，按顺序结算。 |

## 4. 目标类型（`targetType`）

当前项目已使用：

- `Self`：只能指定自己。
- `SingleAlly`：可指定自己或队友（当前规则）。
- `MonsterPart`：指定单个怪物部位。
- `AllMonsterParts`：选中所有满足范围的怪物部位。
- `Area`：指定站位区域（用于移动卡）。

引擎还支持但当前卡池未使用：

- `AllAllies`
- `OtherAlly`
- `SingleUnit`

## 5. 效果列表（`effects`）

`effects` 是顺序执行的。每个效果对象通用字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| `op` | string | 效果类型（见下一节）。 |
| `target` | string | 子目标模式：`CardTarget`/`Self`/`None`（引擎也支持 `AllAllies`/`AllEnemies`）。 |
| `amount` | int | 主数值参数。 |
| `amount2` | int | 次数值参数（少量效果使用）。 |
| `ratio` | float | 比例参数，默认 `1.0`。 |
| `scaleBy` | string | 缩放来源（当前主要给 `Damage` 用）。 |
| `statusId` | string | 状态名（`ApplyStatus` 必填）。 |
| `pileFrom` | string | 预留字段，当前未使用。 |

## 6. 可用效果 `op` 说明

### 6.1 `Damage`

- 作用：造成伤害。
- 常用字段：`amount`、`target`、可选 `scaleBy` + `ratio`。
- 实际伤害 = `amount + 缩放值 + 力量加成(Strength)`，最低 0。
- `scaleBy` 可选值：
  - `SelfArmor`：按自身护甲缩放。
  - `CardsPlayedThisTurn`：按本回合已出牌数缩放。
  - `TargetPoison`：按目标中毒层数缩放。

### 6.2 `Heal`

- 作用：治疗玩家目标。
- 常用字段：`amount`、`target`。
- 不会超过 `maxHp`。

### 6.3 `Draw`

- 作用：抽牌。
- 常用字段：`amount`、`target`。
- 重要规则：
  - 当前手牌上限为 5。
  - 满手时，抽到的牌进弃牌堆（日志会显示抽到 0）。

### 6.4 `GainArmor`

- 作用：获得护甲。
- 常用字段：`amount`、`target`。

### 6.5 `ApplyStatus`

- 作用：给玩家/怪物整体添加状态层数。
- 必填字段：`statusId`。
- 常用字段：`amount`（最少按 1 处理）、`target`。
- 常见状态：`Poison`、`Strength`。

### 6.6 `ApplyVulnerable`

- 作用：给玩家添加易伤层数。
- 常用字段：`amount`、`target`。

### 6.7 `DamageByArmor`

- 作用：按护甲转伤害（典型“盾击”）。
- 伤害值 = `amount + round(当前护甲 * ratio)`。
- 当 `amount2 > 0` 时，会消耗部分护甲（按缩放部分消耗）。

### 6.8 `ModifyEnergy`

- 作用：修改当前能量。
- 常用字段：`amount`（可正可负）。
- 最低不小于 0。

### 6.9 `LoseHp`

- 作用：目标玩家直接失去生命（不走护甲减伤）。
- 常用字段：`amount`、`target`。

### 6.10 `RecycleDiscardToHand`

- 作用：从弃牌堆回收牌到手牌。
- 常用字段：
  - `amount`：回收张数。
  - `amount2`：回收牌的费用修正（加到 `costDelta`）。
- 从弃牌堆末尾开始回收。

### 6.11 `CopyAndPlunder`

- 作用：对友方目标“借牌”并复制。
- 行为：
  - 从目标手牌随机拿 1 张放入其弃牌堆。
  - 施法者获得该卡复制品（手满则进施法者弃牌堆）。
- 目标手牌为空会失败。

### 6.12 `ExhaustFromHand`

- 作用：消耗自己手牌中的牌。
- 常用字段：`amount`（消耗张数）。
- 每次优先消耗手牌中的首个可消耗牌。

### 6.13 `MoveArea`

- 作用：移动到指定区域。
- 一般写法：`target: "None"`。
- 实际区域来自出牌命令中的 `targetArea`（West/East）。

## 7. 重要结算规则（设计卡牌时务必注意）

1. 效果按 `effects` 顺序结算。前面的效果会影响后面的效果。
2. 出牌时，牌会先离开手牌再结算效果（接近 Slay the Spire 习惯）。
3. 手牌上限为 5；满手抽牌会进入弃牌堆。
4. 部位只负责“耐久/破坏槽”；怪物状态（如中毒）统一挂在怪物整体。
5. 部位破坏后仍可被选中并继续受击；部位受击会继续对怪物核心血量生效（直到核心归零）。
6. 单卡效果操作次数有上限（内部保护），避免无限循环。

## 8. 推荐配置模板

### 8.1 单体伤害 + 抽牌

```json
{
  "id": "assassin_new_strike",
  "name": "新斩",
  "classTag": "Assassin",
  "targetType": "MonsterPart",
  "rangeHeights": "Ground",
  "rangeDistance": "Near",
  "energyCost": 1,
  "effects": [
    { "op": "Damage", "target": "CardTarget", "amount": 3 },
    { "op": "Draw", "target": "Self", "amount": 1 }
  ]
}
```

### 8.2 护甲转输出

```json
{
  "id": "warrior_new_slam",
  "name": "新盾击",
  "classTag": "Warrior",
  "targetType": "MonsterPart",
  "rangeHeights": "Both",
  "rangeDistance": "Both",
  "energyCost": 1,
  "effects": [
    { "op": "DamageByArmor", "target": "CardTarget", "amount": 0, "ratio": 1.0, "amount2": 1 }
  ]
}
```

### 8.3 中毒体系

```json
{
  "id": "assassin_new_poison",
  "name": "新毒镖",
  "classTag": "Assassin",
  "targetType": "MonsterPart",
  "rangeHeights": "Both",
  "rangeDistance": "Both",
  "energyCost": 1,
  "effects": [
    { "op": "ApplyStatus", "target": "CardTarget", "statusId": "Poison", "amount": 4 }
  ]
}
```

## 9. 新增卡牌检查清单

- `id` 是否唯一。
- `name` 是否与现有卡高度重复。
- `targetType` 与 `effects[].target` 是否匹配。
- 数值字段是否填写在正确效果上（如 `ApplyStatus` 必须给 `statusId`）。
- 是否已加入目标卡组 `decks.json`。
- 建议补一条 `DotnetVerifier` 场景，验证关键效果与日志顺序。
