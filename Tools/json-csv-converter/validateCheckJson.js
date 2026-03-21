const fs = require('fs');
const Ajv = require('ajv');

function usage() {
  console.log('Usage: node validateCheckJson.js <data.json> <schema.json>');
}

function main() {
  const [, , dataPath, schemaPath] = process.argv;

  if (!dataPath || !schemaPath) {
    usage();
    process.exit(1);
  }

  const schema = JSON.parse(fs.readFileSync(schemaPath, 'utf8'));
  const data = JSON.parse(fs.readFileSync(dataPath, 'utf8'));

  if (data && typeof data === 'object' && !Array.isArray(data)) {
    for (const key of ['$schema', 'version']) {
      if (Object.prototype.hasOwnProperty.call(data, key)) {
        console.error(`Schema validation failed: root property "${key}" is no longer allowed.`);
        process.exit(1);
      }
    }
  }

  const ajv = new Ajv({ allErrors: true });
  const validate = ajv.compile(schema);

  if (!validate(data)) {
    console.error('Schema validation failed:');
    for (const err of validate.errors || []) {
      console.error(`- ${err.instancePath || '/'} ${err.message}`);
    }
    process.exit(1);
  }

  console.log(`Schema validation passed: ${dataPath}`);
}

main();
