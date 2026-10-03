import { execFileSync } from 'node:child_process'
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const rootFiles = new Set(['.editorconfig', '.gitattributes', '.gitignore', 'AGENTS.md',
  'CONTRIBUTING.md', 'Directory.Build.props', 'Directory.Packages.props', 'Elyndor-Control.cmd',
  'Elyndor.slnx', 'global.json', 'README.md'])
const rootDirectories = new Set(['.agents', '.claude', '.codex', '.config', '.github',
  'apphost', 'content', 'content-analysis', 'deploy', 'docs', 'reference', 'src', 'tests', 'tools', 'web'])
const generated = /(^|\/)(bin|obj|node_modules|dist|output|artifacts|\.artifacts|\.elyndor|__pycache__|test-results|playwright-report)\//
const localFile = /(^|\/)(\.env(?:\..+)?|secrets\.json)$|\.(log|bak|tmp|patch|bundle|pyc)$/

export function auditLayout(files, documents = new Map()) {
  const known = new Set(files)
  const problems = new Set()
  for (const file of files) {
    const root = file.split('/')[0]
    if (generated.test(file) || (localFile.test(file) && !file.endsWith('/.env.example'))) {
      problems.add(`Tracked generated/local artifact: ${file}`)
    } else if (!file.includes('/') && !rootFiles.has(file)) {
      problems.add(`Unexpected root file: ${file}`)
    } else if (file.includes('/') && !rootDirectories.has(root)) {
      problems.add(`Unexpected root directory: ${root}`)
    }
  }
  for (const [file, text] of documents) {
    if (file === '.gitignore') {
      const rules = new Set()
      for (const line of text.split(/\r?\n/)) {
        const rule = line.trim()
        if (!rule || rule.startsWith('#')) continue
        if (rules.has(rule)) problems.add(`Duplicate ignore rule: ${rule}`)
        rules.add(rule)
      }
      continue
    }
    for (const match of text.matchAll(/\[[^\]\n]*\]\(([^)\s]+)\)/g)) {
      const href = match[1]
      if (/^(?:[a-z][a-z0-9+.-]*:|#|\/\/)/i.test(href)) continue
      const target = decodeURIComponent(href.split(/[?#]/)[0])
      const resolved = path.posix.normalize(path.posix.join(path.posix.dirname(file), target))
      if (!known.has(resolved)) problems.add(`Broken local link: ${file} -> ${target}`)
    }
  }
  return [...problems]
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const root = fileURLToPath(new URL('../../', import.meta.url))
  const files = execFileSync('git', ['ls-files', '--cached', '--others', '--exclude-standard', '-z'],
    { cwd: root, encoding: 'utf8' }).split('\0').filter(file => file && existsSync(path.join(root, file)))
  const entryPoints = ['.gitignore', 'README.md', 'AGENTS.md', 'CONTRIBUTING.md', 'docs/README.md',
    'docs/development/repository-layout.md', 'tools/README.md', 'tools/assets/README.md',
    'content-analysis/README.md', 'docs/source-of-truth/README.md']
  const documents = new Map(entryPoints.filter(file => files.includes(file))
    .map(file => [file, readFileSync(path.join(root, file), 'utf8')]))
  const problems = auditLayout(files, documents)
  if (problems.length) {
    problems.forEach(problem => console.error(problem))
    process.exitCode = 1
  } else {
    console.log(`Repository layout OK: ${files.length} files; ${documents.size} maintained entry points.`)
  }
}
