const test = require('node:test');
const assert = require('node:assert/strict');
const path = require('node:path');
const { safePath, formatBytes, visibleEntry } = require('../core');

test('normaliza caminhos e recusa byte nulo', () => {
  assert.equal(safePath('/tmp/../tmp'), path.resolve('/tmp'));
  assert.throws(() => safePath('/tmp/\0x'));
});

test('formata tamanhos para a interface', () => {
  assert.equal(formatBytes(512), '512 B');
  assert.equal(formatBytes(1536), '1,5 KB');
});

test('oculta apenas entradas artificiais', () => {
  assert.equal(visibleEntry('.config'), true);
  assert.equal(visibleEntry('..'), false);
});
