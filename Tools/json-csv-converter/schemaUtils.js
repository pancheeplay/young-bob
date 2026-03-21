function resolveRef(schemaRoot, schemaNode) {
  let cursor = schemaNode;
  const seen = new Set();

  while (cursor && typeof cursor === 'object' && typeof cursor.$ref === 'string' && cursor.$ref.startsWith('#/')) {
    if (seen.has(cursor.$ref)) break;
    seen.add(cursor.$ref);

    const refParts = cursor.$ref.slice(2).split('/');
    let resolved = schemaRoot;
    for (const part of refParts) {
      if (!resolved || typeof resolved !== 'object') return cursor;
      resolved = resolved[part];
    }
    cursor = resolved;
  }

  return cursor;
}

module.exports = {
  resolveRef
};
