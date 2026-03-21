const fs = require('fs');
const path = require('path');
const { stringifyCsv } = require('./csvUtils');
const { resolveRef } = require('./schemaUtils');

function usage() {
  console.log('Usage: node jsonToSheet.js <input.json> <output.csv> <arrayPath|-> [schema.json]');
  console.log('Example: node jsonToSheet.js data.json data.csv monsters schema.json');
  console.log('Example: node jsonToSheet.js list.json list.csv -');
}

function getByPath(target, dottedPath) {
  const parts = dottedPath.split('.').filter(Boolean);
  let cursor = target;
  for (const part of parts) {
    if (cursor == null || typeof cursor !== 'object') return undefined;
    cursor = cursor[part];
  }
  return cursor;
}

function flattenObject(input, prefix = '', out = {}) {
  if (Array.isArray(input)) {
    out[prefix] = JSON.stringify(input);
    return out;
  }

  if (input && typeof input === 'object') {
    for (const [key, value] of Object.entries(input)) {
      const next = prefix ? `${prefix}.${key}` : key;
      flattenObject(value, next, out);
    }
    return out;
  }

  out[prefix] = input;
  return out;
}

function buildTypeMap(schemaRoot, schemaNode, prefix = '', out = {}) {
  const node = resolveRef(schemaRoot, schemaNode);
  if (!node || typeof node !== 'object') return out;

  if (Array.isArray(node.enum) && node.enum.length > 0 && prefix) {
    out[prefix] = node.enum.join(' | ');
    return out;
  }

  if (node.type === 'array' && prefix) {
    const items = resolveRef(schemaRoot, node.items);
    let itemType = 'unknown';

    if (items && typeof items === 'object') {
      if (Array.isArray(items.enum) && items.enum.length > 0) itemType = items.enum.join(' | ');
      else if (typeof items.type === 'string') itemType = items.type;
    }

    out[prefix] = `array<${itemType}>`;
    return out;
  }

  if (node.type && node.type !== 'object' && prefix) {
    out[prefix] = node.type;
    return out;
  }

  if (node.type === 'object' && node.properties && typeof node.properties === 'object') {
    for (const [key, child] of Object.entries(node.properties)) {
      const next = prefix ? `${prefix}.${key}` : key;
      buildTypeMap(schemaRoot, child, next, out);
    }
  }

  return out;
}

function pickArrayItemSchema(schemaRoot, arrayPath) {
  if (arrayPath === '-') {
    const rootNode = resolveRef(schemaRoot, schemaRoot);
    if (!rootNode || rootNode.type !== 'array') {
      throw new Error('arrayPath is "-" but schema root is not array type');
    }
    return rootNode.items;
  }

  const segments = arrayPath.split('.').filter(Boolean);
  let node = resolveRef(schemaRoot, schemaRoot);

  for (const segment of segments) {
    node = resolveRef(schemaRoot, node);
    if (!node || node.type !== 'object' || !node.properties || !node.properties[segment]) {
      throw new Error(`Schema path not found for arrayPath segment: ${segment}`);
    }
    node = node.properties[segment];
  }

  node = resolveRef(schemaRoot, node);
  if (!node || node.type !== 'array') {
    throw new Error(`Schema node for arrayPath "${arrayPath}" is not array type`);
  }

  return node.items;
}

function inferScalarType(value) {
  if (Number.isInteger(value)) return 'integer';
  if (typeof value === 'number') return 'number';
  if (typeof value === 'boolean') return 'boolean';
  if (Array.isArray(value)) {
    const itemTypes = [...new Set(value.map((item) => inferScalarType(item)))];
    return `array<${itemTypes.length === 1 ? itemTypes[0] : 'object'}>`;
  }
  if (value && typeof value === 'object') return 'object';
  return 'string';
}

function inferTypeMap(records) {
  const typeMap = {};

  for (const record of records) {
    const flat = flattenObject(record);
    for (const [key, value] of Object.entries(flat)) {
      const nextType = inferScalarType(value);
      if (!typeMap[key]) {
        typeMap[key] = nextType;
        continue;
      }

      if (typeMap[key] !== nextType) {
        typeMap[key] = 'string';
      }
    }
  }

  return typeMap;
}

function main() {
  const [, , inputJson, outputCsv, arrayPath, schemaFile] = process.argv;

  if (!inputJson || !outputCsv || !arrayPath) {
    usage();
    process.exit(1);
  }

  const dataRoot = JSON.parse(fs.readFileSync(inputJson, 'utf8'));
  const records = arrayPath === '-' ? dataRoot : getByPath(dataRoot, arrayPath);
  if (!Array.isArray(records)) {
    throw new Error(`Target at arrayPath "${arrayPath}" is not an array`);
  }

  const flatRows = records.map((row) => flattenObject(row));
  const headerSet = new Set();
  for (const row of flatRows) {
    for (const key of Object.keys(row)) headerSet.add(key);
  }

  const headers = [...headerSet];
  let typeMap;
  if (schemaFile) {
    const schemaPath = path.resolve(schemaFile);
    const schemaRoot = JSON.parse(fs.readFileSync(schemaPath, 'utf8'));
    const itemSchema = pickArrayItemSchema(schemaRoot, arrayPath);
    typeMap = buildTypeMap(schemaRoot, itemSchema);
  } else {
    typeMap = inferTypeMap(records);
  }
  const typeRow = headers.map((h) => typeMap[h] || 'string');

  const csvRows = [
    headers,
    typeRow,
    ...flatRows.map((row) => headers.map((h) => row[h] ?? ''))
  ];

  fs.writeFileSync(outputCsv, stringifyCsv(csvRows), 'utf8');
  console.log(`Converted ${inputJson} -> ${outputCsv} (${records.length} rows, path=${arrayPath})`);
}

main();
