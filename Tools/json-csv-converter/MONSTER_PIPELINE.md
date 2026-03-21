# Minimal JSON Data Center

## Generic commands (all arguments required)

```bash
node jsonToSheet.js <input.json> <output.csv> <arrayPath|-> <schema.json>
node sheetToJson.js <input.csv> <output.json> <arrayPath|-> <schemaPathToWrite>
node validateCheckJson.js <data.json> <schema.json>
```

## Notes

- `arrayPath` is the array field path in JSON (example: `monsters`, `heroes`, `config.enemies.wave1`).
- Use `-` when JSON root itself is an array.
- `sheetToJson.js` writes `"$schema"` with the `schemaPathToWrite` argument when `arrayPath` is not `-`.
- `jsonToSheet.js` uses the provided schema file to generate CSV type-hint row.

## Example (Monster)

```bash
node jsonToSheet.js monsters.json monsterSheet.csv monsters monsters.schema.json
node sheetToJson.js monsterSheet.csv monsters.json monsters ./monsters.schema.json
node validateCheckJson.js monsters.json monsters.schema.json
```

## Example (Hero)

```bash
node validateCheckJson.js heroes.json heroes.schema.json
```
