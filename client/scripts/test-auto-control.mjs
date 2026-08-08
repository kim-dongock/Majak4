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
const {
  calculateTimeBankSegments,
  enableAutoTapAfterReach,
  getAutoControlDelayMs,
  resetAutoControlForNewKyoku,
  resolveAutoControlAction,
  shouldEnableAutoPassAtKyokuStart,
  shouldSuspendAutoPassForPrompt,
} = module.exports

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

assert.equal(
  JSON.stringify(resetAutoControlForNewKyoku({ prox: false, autoTap: true, autoPass: true, autoHora: true })),
  JSON.stringify(off),
  'manual auto controls should reset at kyoku start',
)
const proxyState = { prox: true, autoTap: true, autoPass: true, autoHora: true }
assert.equal(resetAutoControlForNewKyoku(proxyState), proxyState, 'temporary-away state should survive kyoku start')
assert.equal(
  JSON.stringify(resetAutoControlForNewKyoku({ prox: false, autoTap: true, autoPass: false, autoHora: true }, true)),
  JSON.stringify({ ...off, autoPass: true }),
  'configured auto-pass should be reapplied at kyoku start',
)

assert.equal(getAutoControlDelayMs('Pass', '002'), 1000, 'standard-speed auto pass should wait through keep time')
assert.equal(getAutoControlDelayMs('Tap', '003'), 1200, 'relaxed-speed tsumogiri should wait through keep time')
assert.equal(getAutoControlDelayMs('Pass', '000'), 100, 'fastest-speed auto pass should use its keep time')
assert.equal(getAutoControlDelayMs('Ron', '003'), 50, 'auto ron should not wait through keep time')
assert.equal(getAutoControlDelayMs('Tsumo', '003'), 50, 'auto tsumo should not wait through keep time')
assert.equal(getAutoControlDelayMs('Tap', ''), 1000, 'missing room speed should use the standard keep time')
assert.equal(shouldEnableAutoPassAtKyokuStart(0, '002'), false, 'mode 0 should clear auto pass')
assert.equal(shouldEnableAutoPassAtKyokuStart(1, '000'), false, 'mode 1 should clear auto pass at 100 ms keep speed')
assert.equal(shouldEnableAutoPassAtKyokuStart(1, '001'), false, 'mode 1 should clear auto pass at 500 ms keep speed')
assert.equal(shouldEnableAutoPassAtKyokuStart(1, '002'), true, 'mode 1 should enable auto pass above the legacy 500 ms threshold')
assert.equal(shouldEnableAutoPassAtKyokuStart(2, '000'), true, 'mode 2 should enable auto pass at every speed')

const autoPass = { ...off, autoPass: true }
assert.equal(shouldSuspendAutoPassForPrompt(autoPass, ['Pass', 'Chi'], false), true, 'call-start input should hold auto pass for this response')
assert.equal(shouldSuspendAutoPassForPrompt(autoPass, ['Pass', 'Chi'], true), false, 'turn input should not hold auto pass')
assert.equal(shouldSuspendAutoPassForPrompt(proxyState, ['Pass', 'Chi'], false), false, 'temporary-away auto pass must remain unconditional')
assert.equal(shouldSuspendAutoPassForPrompt(autoPass, ['Pass', 'Ron'], false), false, 'ron offers already suppress auto pass')

const normalize = value => JSON.parse(JSON.stringify(value))

assert.deepEqual(
  normalize(enableAutoTapAfterReach(off, true)),
  { ...off, autoTap: true },
  'configured local reach should enable tsumogiri',
)
assert.equal(enableAutoTapAfterReach(off, false), off, 'disabled reach setting should preserve auto controls')
assert.equal(enableAutoTapAfterReach(proxyState, true), proxyState, 'temporary-away state should remain unchanged after reach')

assert.deepEqual(
  normalize(calculateTimeBankSegments(22_000, 2_000, 100, 20_000, true)),
  { bankMs: 20_000, turnMs: 1_900, keepMs: 100 },
  'turn prompt should begin with bank, turn, and common-wait segments',
)
assert.deepEqual(
  normalize(calculateTimeBankSegments(21_950, 2_000, 100, 20_000, true)),
  { bankMs: 20_000, turnMs: 1_900, keepMs: 50 },
  'common-wait segment should drain first',
)
assert.deepEqual(
  normalize(calculateTimeBankSegments(20_500, 2_000, 100, 20_000, true)),
  { bankMs: 20_000, turnMs: 500, keepMs: 0 },
  'per-turn segment should drain before the bank',
)
assert.deepEqual(
  normalize(calculateTimeBankSegments(19_500, 2_000, 100, 20_000, true)),
  { bankMs: 19_500, turnMs: 0, keepMs: 0 },
  'kyoku bank should drain only after base time',
)
assert.deepEqual(
  normalize(calculateTimeBankSegments(1_500, 2_000, 100, 20_000, false)),
  { bankMs: 20_000, turnMs: 1_500, keepMs: 0 },
  'extendable response should display but not consume the bank before activation',
)

console.log('auto-control source tests passed')