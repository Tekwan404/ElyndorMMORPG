import assert from 'node:assert/strict'
import { test } from 'node:test'
import { auditLayout } from './check-layout.mjs'

test('accepts maintained entry points, tools and preserved agent configuration', () => {
  assert.deepEqual(auditLayout(['README.md', '.agents/skills/example/SKILL.md',
    '.config/dotnet-tools.json', 'tools/assets/import.py'], new Map([
    ['README.md', '[Importer](tools/assets/import.py)'],
  ])), [])
})

test('rejects arbitrary input JSON in the repository root', () => {
  assert.deepEqual(auditLayout(['dump.json']), ['Unexpected root file: dump.json'])
})

test('rejects an unowned top-level directory', () => {
  assert.deepEqual(auditLayout(['misc/notes.txt']), ['Unexpected root directory: misc'])
})

test('rejects tracked build outputs in nested projects', () => {
  assert.deepEqual(auditLayout(['src/Example/obj/project.assets.json']),
    ['Tracked generated/local artifact: src/Example/obj/project.assets.json'])
})

test('rejects tracked local secrets while allowing example environment files', () => {
  assert.deepEqual(auditLayout(['web/elyndor-web/.env', 'web/elyndor-web/.env.example']),
    ['Tracked generated/local artifact: web/elyndor-web/.env'])
})

test('detects broken local navigation links after a file move', () => {
  assert.deepEqual(auditLayout(['README.md'], new Map([
    ['README.md', '[Guide](docs/development/missing.md#setup)'],
  ])), ['Broken local link: README.md -> docs/development/missing.md'])
})

test('validates relative paths and exact case even on Windows', () => {
  assert.deepEqual(auditLayout(['tools/README.md', 'tools/assets/Import.py'], new Map([
    ['tools/README.md', '[Import](assets/import.py)'],
  ])), ['Broken local link: tools/README.md -> assets/import.py'])
})

test('ignores external links and same-page anchors', () => {
  assert.deepEqual(auditLayout(['README.md'], new Map([
    ['README.md', '[Remote](https://example.com/path) [Setup](#setup)'],
  ])), [])
})

test('rejects duplicate ignore rules but not repeated comments', () => {
  assert.deepEqual(auditLayout(['.gitignore'], new Map([
    ['.gitignore', '# Outputs\n/artifacts/\n# Outputs\n/artifacts/\n'],
  ])), ['Duplicate ignore rule: /artifacts/'])
})
