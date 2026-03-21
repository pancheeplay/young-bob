const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');
const deepEqual = require('fast-deep-equal');

const TOOL_DIR = __dirname;
const PROJECT_ROOT = path.resolve(TOOL_DIR, '..', '..');

function usage() {
  console.log('Usage:');
  console.log('  entityCli.js json-to-csv <path/to/entity>');
  console.log('  entityCli.js csv-to-json <path/to/entity>');
  console.log('');
  console.log('Example:');
  console.log('  json-to-csv Assets/Resources/GameData/cards');
  console.log('  csv-to-json Assets/Resources/GameData/cards');
}

function stripKnownSuffix(input) {
  if (!input) return input;

  if (input.endsWith('.schema.json')) {
    return input.slice(0, -'.schema.json'.length);
  }

  if (input.endsWith('.json')) {
    return input.slice(0, -'.json'.length);
  }

  if (input.endsWith('.csv')) {
    return input.slice(0, -'.csv'.length);
  }

  return input;
}

function candidateBases(rawInput) {
  const normalized = stripKnownSuffix(rawInput.trim());
  if (!normalized) {
    return [];
  }

  if (path.isAbsolute(normalized)) {
    return [path.normalize(normalized)];
  }

  return [
    path.resolve(process.cwd(), normalized),
    path.resolve(PROJECT_ROOT, normalized)
  ];
}

function scoreCandidate(basePath) {
  let score = 0;
  for (const suffix of ['.json', '.csv', '.schema.json']) {
    if (fs.existsSync(`${basePath}${suffix}`)) {
      score += 1;
    }
  }

  if (fs.existsSync(path.dirname(basePath))) {
    score += 0.25;
  }

  return score;
}

function resolveEntityBase(rawInput) {
  const candidates = candidateBases(rawInput);
  if (candidates.length === 0) {
    throw new Error('Missing entity path.');
  }

  const ranked = [...new Set(candidates)]
    .map((candidate) => ({ candidate, score: scoreCandidate(candidate) }))
    .sort((a, b) => b.score - a.score);

  if (ranked[0] && ranked[0].score > 0) {
    return ranked[0].candidate;
  }

  return ranked[0].candidate;
}

function ensureFileExists(filePath, label) {
  if (!fs.existsSync(filePath)) {
    throw new Error(`${label} not found: ${path.relative(PROJECT_ROOT, filePath) || filePath}`);
  }
}

function runNodeScript(scriptName, args) {
  const result = spawnSync(process.execPath, [path.join(TOOL_DIR, scriptName), ...args], {
    cwd: PROJECT_ROOT,
    stdio: 'inherit'
  });

  if (typeof result.status === 'number' && result.status !== 0) {
    process.exit(result.status);
  }

  if (result.error) {
    throw result.error;
  }
}

function buildEntityPaths(rawInput) {
  const basePath = resolveEntityBase(rawInput);
  const arrayPath = path.basename(basePath);

  return {
    basePath,
    arrayPath,
    csvPath: `${basePath}.csv`,
    jsonPath: `${basePath}.json`,
    schemaPath: `${basePath}.schema.json`
  };
}

function commandJsonToCsv(rawInput) {
  const paths = buildEntityPaths(rawInput);
  ensureFileExists(paths.jsonPath, 'JSON file');
  ensureFileExists(paths.schemaPath, 'Schema file');

  runNodeScript('jsonToSheet.js', [
    paths.jsonPath,
    paths.csvPath,
    paths.arrayPath,
    paths.schemaPath
  ]);
}

function commandCsvToJson(rawInput) {
  const paths = buildEntityPaths(rawInput);
  ensureFileExists(paths.csvPath, 'CSV file');
  ensureFileExists(paths.schemaPath, 'Schema file');

  const tempJsonPath = path.join(
    os.tmpdir(),
    `${path.basename(paths.basePath)}.${process.pid}.${Date.now()}.json`
  );

  try {
    if (fs.existsSync(paths.jsonPath)) {
      fs.copyFileSync(paths.jsonPath, tempJsonPath);
    } else {
      fs.writeFileSync(tempJsonPath, '{}\n', 'utf8');
    }

    runNodeScript('sheetToJson.js', [
      paths.csvPath,
      tempJsonPath,
      paths.arrayPath,
      paths.schemaPath
    ]);

    runNodeScript('validateCheckJson.js', [
      tempJsonPath,
      paths.schemaPath
    ]);

    const nextJsonText = fs.readFileSync(tempJsonPath, 'utf8');
    const nextJson = JSON.parse(nextJsonText);

    if (fs.existsSync(paths.jsonPath)) {
      const currentJson = JSON.parse(fs.readFileSync(paths.jsonPath, 'utf8'));
      if (deepEqual(currentJson, nextJson)) {
        console.log(`No JSON changes for ${path.relative(PROJECT_ROOT, paths.jsonPath)}.`);
        return;
      }
    }

    fs.writeFileSync(paths.jsonPath, nextJsonText, 'utf8');
    console.log(`Updated ${path.relative(PROJECT_ROOT, paths.jsonPath)} after validation.`);
  } finally {
    if (fs.existsSync(tempJsonPath)) {
      fs.unlinkSync(tempJsonPath);
    }
  }
}

function main() {
  const [, , commandName, rawInput] = process.argv;

  if (!commandName || !rawInput) {
    usage();
    process.exit(1);
  }

  try {
    if (commandName === 'json-to-csv') {
      commandJsonToCsv(rawInput);
      return;
    }

    if (commandName === 'csv-to-json') {
      commandCsvToJson(rawInput);
      return;
    }

    throw new Error(`Unsupported command: ${commandName}`);
  } catch (error) {
    console.error(error.message || String(error));
    process.exit(1);
  }
}

main();
