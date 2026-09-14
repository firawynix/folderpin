const path = require('node:path');

function safePath(value) {
  if (typeof value !== 'string' || value.includes('\0')) throw new Error('Caminho inválido.');
  return path.resolve(value);
}

function formatBytes(value) {
  if (!Number.isFinite(value) || value < 0) return '';
  if (value < 1024) return `${value} B`;
  const units = ['KB', 'MB', 'GB', 'TB'];
  let n = value / 1024;
  let i = 0;
  while (n >= 1024 && i < units.length - 1) { n /= 1024; i += 1; }
  return `${n < 10 ? n.toFixed(1) : Math.round(n)} ${units[i]}`.replace('.', ',');
}

function visibleEntry(name) {
  return Boolean(name) && name !== '.' && name !== '..';
}

module.exports = { safePath, formatBytes, visibleEntry };
