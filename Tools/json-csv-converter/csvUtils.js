const NEWLINE = /\r?\n/;

function parseCsv(text) {
  const rows = [];
  let row = [];
  let cell = '';
  let inQuotes = false;

  for (let i = 0; i < text.length; i += 1) {
    const ch = text[i];
    const next = text[i + 1];

    if (inQuotes) {
      if (ch === '"' && next === '"') {
        cell += '"';
        i += 1;
      } else if (ch === '"') {
        inQuotes = false;
      } else {
        cell += ch;
      }
      continue;
    }

    if (ch === '"') {
      inQuotes = true;
      continue;
    }

    if (ch === ',') {
      row.push(cell);
      cell = '';
      continue;
    }

    if (ch === '\n') {
      row.push(cell);
      rows.push(row);
      row = [];
      cell = '';
      continue;
    }

    if (ch !== '\r') {
      cell += ch;
    }
  }

  row.push(cell);
  const isTrailingEmptyLine = row.length === 1 && row[0] === '';
  if (!isTrailingEmptyLine) {
    rows.push(row);
  }

  return rows;
}

function escapeCsvCell(value) {
  const str = String(value ?? '');
  if (str.includes('"') || str.includes(',') || NEWLINE.test(str)) {
    return `"${str.replaceAll('"', '""')}"`;
  }
  return str;
}

function stringifyCsv(rows) {
  return `${rows.map((row) => row.map(escapeCsvCell).join(',')).join('\n')}\n`;
}

module.exports = {
  parseCsv,
  stringifyCsv
};
