import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import vm from 'node:vm'

const require = createRequire(import.meta.url)
const ts = require('typescript')
const __dirname = dirname(fileURLToPath(import.meta.url))
const sourcePath = resolve(__dirname, '../src/screens/outgame/memberFilter.ts')
const source = readFileSync(sourcePath, 'utf8')
const transpiled = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2020,
    strict: true,
  },
})

const module = { exports: {} }
vm.runInNewContext(transpiled.outputText, { module, exports: module.exports }, { filename: sourcePath })
const { DEFAULT_MEMBER_FILTER, isMemberFilterActive, matchesMemberFilter } = module.exports

const member = { sex: 'female', age: 28, nlevel: 4 }
assert.equal(matchesMemberFilter(member, DEFAULT_MEMBER_FILTER), true, 'default filter should show every member')
assert.equal(isMemberFilterActive(DEFAULT_MEMBER_FILTER), false, 'default filter should be inactive')
assert.equal(matchesMemberFilter(member, { sex: 'female', age: '20s', level: 4 }), true, 'all matching conditions should pass')
assert.equal(matchesMemberFilter(member, { sex: 'male', age: '20s', level: 4 }), false, 'sex should be combined with AND')
assert.equal(matchesMemberFilter(member, { sex: 'female', age: '30s', level: 4 }), false, 'age should be combined with AND')
assert.equal(matchesMemberFilter(member, { sex: 'female', age: '20s', level: 3 }), false, 'level should be combined with AND')
assert.equal(matchesMemberFilter({ ...member, age: 0 }, { sex: 'all', age: '20s', level: null }), false, 'unknown age should not match a specific range')
assert.equal(matchesMemberFilter({ ...member, age: 40 }, { sex: 'all', age: '40plus', level: null }), true, '40 should match the 40-plus range')

console.log('member-filter source tests passed')