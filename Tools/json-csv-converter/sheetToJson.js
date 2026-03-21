const fs = require('fs');
const { parseCsv } = require('./csvUtils');

function usage() {
  console.log('Usage: node sheetToJson.js <input.csv> <output.json> <arrayPath|-> <schemaPathToWrite>');
  console.log('Example: node sheetToJson.js data.csv data.json monsters ./schema.json');
  console.log('Example: node sheetToJson.js list.csv list.json - ./schema.json');
}

function parsePrimitive(value, typeHint, rowIndex, column) {
  const hint = (typeHint || '').trim();
  const lower = hint.toLowerCase();

  if (hint.includes('|')) {
    const options = hint.split('|').map((s) => s.trim()).filter(Boolean);
    if (options.length > 0 && !options.includes(value)) {
      throw new Error(`Row ${rowIndex}: ${column} must be one of [${options.join(', ')}], got ${value}`);
    }
    return value;
  }

  if (lower.startsWith('array<') && lower.endsWith('>')) {
    const itemType = lower.slice('array<'.length, -1).trim();

    if (value.startsWith('[')) {
      const parsed = JSON.parse(value);
      if (!Array.isArray(parsed)) {
        throw new Error(`Row ${rowIndex}: ${column} must be array`);
      }
      return parsed;
    }

    const pieces = value.split('|').map((s) => s.trim()).filter(Boolean);
    return pieces.map((p) => parsePrimitive(p, itemType, rowIndex, column));
  }

  if (lower === 'integer') {
    if (!/^-?\d+$/.test(value)) throw new Error(`Row ${rowIndex}: ${column} must be integer`);
    return Number.parseInt(value, 10);
  }

  if (lower === 'number') {
    const num = Number(value);
    if (!Number.isFinite(num)) throw new Error(`Row ${rowIndex}: ${column} must be number`);
    return num;
  }

  if (lower === 'boolean') {
    if (value === 'true') return true;
    if (value === 'false') return false;
    throw new Error(`Row ${rowIndex}: ${column} must be true/false`);
  }

  if (lower === 'object') {
    return JSON.parse(value);
  }

  if (!hint && (value.startsWith('{') || value.startsWith('['))) {
    try {
      return JSON.parse(value);
    } catch (error) {
      return value;
    }
  }

  return value;
}

function setByPath(target, dottedPath, value) {
  const parts = dottedPath.split('.').filter(Boolean);
  let cursor = target;

  for (let i = 0; i < parts.length - 1; i += 1) {
    const key = parts[i];
    if (cursor[key] === undefined || cursor[key] === null || typeof cursor[key] !== 'object' || Array.isArray(cursor[key])) {
      cursor[key] = {};
    }
    cursor = cursor[key];
  }

  cursor[parts[parts.length - 1]] = value;
}

function main() {
  const [, , inputCsv, outputJson, arrayPath, schemaPathToWrite] = process.argv;

  if (!inputCsv || !outputJson || !arrayPath || !schemaPathToWrite) {
    usage();
    process.exit(1);
  }

  const csvText = fs.readFileSync(inputCsv, 'utf8');
  const rows = parseCsv(csvText).filter((row) => row.some((c) => String(c).trim() !== ''));

  if (rows.length < 2) {
    throw new Error('CSV must include header row and type row');
  }

  const headers = rows[0].map((h) => h.trim());
  const typeHints = rows[1].map((t) => t.trim());
  const dataRows = rows.slice(2);

  const records = dataRows.map((row, idx) => {
    const record = {};
    const rowIndex = idx + 3;

    headers.forEach((header, col) => {
      const raw = (row[col] ?? '').trim();
      if (!header || raw === '') return;

      const parsed = parsePrimitive(raw, typeHints[col], rowIndex, header);
      setByPath(record, header, parsed);
    });

    return record;
  });

  let root = {};
  if (fs.existsSync(outputJson)) {
    root = JSON.parse(fs.readFileSync(outputJson, 'utf8'));
  }

  if (arrayPath === '-') {
    if (typeof root === 'object' && root !== null && !Array.isArray(root) && Object.keys(root).length > 0) {
      throw new Error('arrayPath is "-" but existing output JSON root is object; use explicit path instead');
    }
    root = records;
  } else {
    if (Array.isArray(root)) {
      throw new Error('Existing output JSON root is array; use arrayPath "-"');
    }

    if (!root || typeof root !== 'object') {
      root = {};
    }

    setByPath(root, arrayPath, records);
  }

  fs.writeFileSync(outputJson, `${JSON.stringify(root, null, 2)}\n`, 'utf8');
  console.log(`Converted ${inputCsv} -> ${outputJson} (${records.length} rows, path=${arrayPath})`);
}

main();
