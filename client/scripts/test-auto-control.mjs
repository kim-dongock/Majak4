import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import vm from 'node:vm'

const require = createRequire(import.meta.url)
const ts = require('typescript')
const __dirname = dirname(fileURLToPath(import.meta.url))
const sourcePath = resolve(__dirname, '../src/game/autoControl.ts')
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
const { resolveAutoControlAction } = module.exports

const off = { prox: false, autoTap: false, autoPass: false, autoHora: false }

assert.equal(resolveAutoControlAction(off, ['Tap']), null, 'no auto setting should not act')
assert.equal(resolveAutoControlAction({ ...off, autoHora: true }, ['Ron']), 'Ron', 'autoHora should ron')
assert.equal(resolveAutoControlAction({ ...off, autoHora: true }, ['Ron', 'Tsumo']), 'Tsumo', 'autoHora should prefer tsumo')
assert.equal(resolveAutoControlAction({ ...off, autoPass: true }, ['Pass']), null, 'autoPass should not immediately pass when pass is the only action')
assert.equal(resolveAutoControlAction({ ...off, autoPass: true }, ['Pass', 'Chi']), 'Pass', 'autoPass should pass when a call/pass choice is active')
assert.equal(resolveAutoControlAction({ ...off, autoPass: true }, ['Pass', 'Ron']), null, 'autoPass must not skip ron offer')
assert.equal(resolveAutoControlAction({ ...off, autoTap: true }, ['Tap']), 'Tap', 'autoTap should discard when tap only')
assert.equal(resolveAutoControlAction({ ...off, autoTap: true }, ['Tap', 'Tsumo']), null, 'autoTap must not discard over tsumo')
assert.equal(resolveAutoControlAction({ ...off, autoTap: true }, ['Tap', 'Kan']), null, 'autoTap must not discard over kan')
assert.equal(resolveAutoControlAction({ ...off, autoTap: true }, ['Tap', 'Hua']), null, 'autoTap must not discard over hua')
assert.equal(resolveAutoControlAction({ ...off, autoTap: true }, ['Tap', 'Tao']), null, 'autoTap must not discard over flow')
assert.equal(resolveAutoControlAction({ prox: true, autoTap: true, autoPass: true, autoHora: true }, ['Pass']), 'Pass', 'proxy state should auto pass when pass only')

console.log('auto-control source tests passed')