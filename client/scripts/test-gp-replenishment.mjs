import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import vm from 'node:vm'

const require = createRequire(import.meta.url)
const ts = require('typescript')
const __dirname = dirname(fileURLToPath(import.meta.url))
const sourcePath = resolve(__dirname, '../src/screens/outgame/gpReplenishment.ts')
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
const { gpReplenishmentFailureMessage, isOwnGpReplenishmentResponse } = module.exports

assert.equal(isOwnGpReplenishmentResponse({ pix: 'self' }, 'self'), true, 'own response should be applied')
assert.equal(isOwnGpReplenishmentResponse({ pix: 'other' }, 'self'), false, 'another player response should be ignored')
assert.equal(isOwnGpReplenishmentResponse({}, 'self'), true, 'legacy response without a target should remain compatible')
assert.match(gpReplenishmentFailureMessage({ mjkk42e: 3 }), /午前6時/, 'used-up message should explain the reset time')
assert.match(gpReplenishmentFailureMessage({ gammoney: 1000 }), /1,000以上/, 'sufficient balance should have a specific message')
assert.equal(gpReplenishmentFailureMessage({ gammoney: 999 }), 'GP補充に失敗しました', 'unknown failures should use the generic message')

console.log('gp-replenishment source tests passed')