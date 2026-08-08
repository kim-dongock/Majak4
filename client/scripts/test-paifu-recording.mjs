import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import vm from 'node:vm'

const require = createRequire(import.meta.url)
const ts = require('typescript')
const __dirname = dirname(fileURLToPath(import.meta.url))
const sourcePath = resolve(__dirname, '../src/game/paifuRecording.ts')
const source = readFileSync(sourcePath, 'utf8')
const transpiled = ts.transpileModule(source, {
  compilerOptions: {
    module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2020,
    strict: true,
  },
})

const storedValues = new Map()
const window = {
  localStorage: {
    getItem: key => storedValues.get(key) ?? null,
    setItem: (key, value) => storedValues.set(key, value),
  },
}
const module = { exports: {} }
vm.runInNewContext(transpiled.outputText, { module, exports: module.exports, window }, { filename: sourcePath })
const {
  beginPaifuRecording,
  finalizePaifuRecording,
  interruptPaifuRecording,
  isRecordablePaifuPacket,
  loadLastUsedPaifuFileName,
  loadRecordedPaifuEntries,
  recordPaifuPacket,
  replaceRecordedPaifuPackets,
  setPaifuRecordingMode,
  saveLastUsedPaifuFileName,
  shouldRecordPaifu,
} = module.exports
const normalize = value => JSON.parse(JSON.stringify(value))

assert.equal(shouldRecordPaifu(0, false), false, 'mode 0 should not record')
assert.equal(shouldRecordPaifu(1, false), true, 'mode 1 should record own games')
assert.equal(shouldRecordPaifu(1, true), false, 'mode 1 should not record viewed games')
assert.equal(shouldRecordPaifu(2, true), true, 'mode 2 should record viewed games')
assert.equal(isRecordablePaifuPacket('smmc4e', {}), true, 'pai info should be recorded')
assert.equal(isRecordablePaifuPacket('playing', { playType: 'MJPID_ACTION' }), true, 'executed actions should be recorded')
assert.equal(isRecordablePaifuPacket('playing', { playType: 'MJPID_ACTIONS' }), false, 'input prompts should not be recorded')
assert.equal(isRecordablePaifuPacket('playing', { playType: 'MJPID_TIME_BANK_EXTENDED' }), false, 'timer UI packets should not be recorded')

beginPaifuRecording({
  mode: 1,
  isViewer: false,
  roomId: '42',
  roomOption: '120000001000000',
  members: [{ name: '東家' }],
})
recordPaifuPacket('playing', { playType: 'MJPID_ACTIONS', actions: [1] })
recordPaifuPacket('playing', { playType: 'MJPID_INIHAN', chicha: 0 })
recordPaifuPacket('smmc4e', { pai: [{ id: 11 }] })
recordPaifuPacket('playing', { playType: 'MJPID_INIKYO', kyokuCnt: 0 })
recordPaifuPacket('playing', { playType: 'MJPID_ACTION', action: 4 })
assert.equal(loadRecordedPaifuEntries().length, 0, 'an unfinished kyoku should not be persisted')
recordPaifuPacket('playing', { playType: 'MJPID_ENDKYO' })
const finalized = finalizePaifuRecording({
  roomName: '公式卓',
  result: '1位',
  members: [{ name: '東家', result: '1位' }],
})
assert.equal(finalized?.roomName, '公式卓', 'finalized entry should keep room metadata')
assert.equal(finalized?.result, '1位', 'finalized entry should keep local result')
assert.deepEqual(normalize(finalized?.data.packets), [
  { cmd: 'playing', data: { playType: 'MJPID_INIHAN', chicha: 0 } },
  { cmd: 'smmc4e', data: { pai: [{ id: 11 }] } },
  { cmd: 'playing', data: { playType: 'MJPID_INIKYO', kyokuCnt: 0 } },
  { cmd: 'playing', data: { playType: 'MJPID_ACTION', action: 4 } },
  { cmd: 'playing', data: { playType: 'MJPID_ENDKYO' } },
], 'stored packet stream should remain replay-compatible and filtered')
assert.equal(loadRecordedPaifuEntries().length, 1, 'finalized entry should be persisted')
assert.equal(finalizePaifuRecording(), null, 'finalization should consume the active recording')

for (let index = 0; index < 22; index += 1) {
  beginPaifuRecording({ mode: 1, isViewer: false, roomId: String(index), roomOption: '' })
  recordPaifuPacket('playing', { playType: 'MJPID_INIHAN' })
  recordPaifuPacket('smmc4e', { index })
  recordPaifuPacket('playing', { playType: 'MJPID_INIKYO' })
  recordPaifuPacket('playing', { playType: 'MJPID_ENDKYO' })
  finalizePaifuRecording()
}
assert.equal(loadRecordedPaifuEntries().length, 20, 'browser storage should retain the latest 20 entries')

storedValues.clear()
beginPaifuRecording({ mode: 0, isViewer: false, roomId: 'mid', roomOption: '' })
recordPaifuPacket('playing', { playType: 'MJPID_INIHAN' })
recordPaifuPacket('smmc4e', { beforeEnable: true })
recordPaifuPacket('playing', { playType: 'MJPID_INIKYO' })
setPaifuRecordingMode(1, false)
recordPaifuPacket('playing', { playType: 'MJPID_ACTION', afterEnable: true })
recordPaifuPacket('playing', { playType: 'MJPID_ENDKYO' })
assert.equal(loadRecordedPaifuEntries().length, 1, 'enabling mid-game should record the current kyoku')

storedValues.clear()
beginPaifuRecording({ mode: 1, isViewer: false, roomId: 'disabled', roomOption: '' })
recordPaifuPacket('playing', { playType: 'MJPID_INIHAN' })
recordPaifuPacket('playing', { playType: 'MJPID_INIKYO' })
setPaifuRecordingMode(0, false)
recordPaifuPacket('playing', { playType: 'MJPID_ENDKYO' })
assert.equal(loadRecordedPaifuEntries().length, 0, 'disabling mid-kyoku should discard that kyoku')

beginPaifuRecording({ mode: 1, isViewer: false, roomId: 'lost', roomOption: '' })
recordPaifuPacket('playing', { playType: 'MJPID_INIHAN' })
recordPaifuPacket('playing', { playType: 'MJPID_INIKYO' })
recordPaifuPacket('playing', { playType: 'MJPID_ENDKYO' })
recordPaifuPacket('playing', { playType: 'MJPID_INIKYO' })
recordPaifuPacket('playing', { playType: 'MJPID_ACTION' })
interruptPaifuRecording()
const interruptedEntry = loadRecordedPaifuEntries().find(entry => entry.fieldName === 'lost')
assert.ok(interruptedEntry, 'kyoku committed before a disconnect should remain available')
assert.equal(interruptedEntry.data.packets.filter(packet => packet.data.playType === 'MJPID_INIKYO').length, 1, 'the interrupted kyoku should be excluded')

beginPaifuRecording({ mode: 1, isViewer: false, roomId: 'resume', roomOption: '' })
replaceRecordedPaifuPackets([
  { cmd: 'playing', data: { playType: 'MJPID_INIHAN' } },
  { cmd: 'playing', data: { playType: 'MJPID_INIKYO', kyokuCnt: 0 } },
  { cmd: 'playing', data: { playType: 'MJPID_ENDKYO', kyokuCnt: 0 } },
  { cmd: 'smmc4e', data: { resumed: true } },
  { cmd: 'playing', data: { playType: 'MJPID_INIKYO', kyokuCnt: 1 } },
  { cmd: 'playing', data: { playType: 'MJPID_ACTION', resumed: true } },
])
recordPaifuPacket('playing', { playType: 'MJPID_ENDKYO', kyokuCnt: 1 })
const resumed = finalizePaifuRecording()
assert.equal(resumed?.data.packets.some(packet => packet.data.kyokuCnt === 0), false, 'reconnect should not merge earlier kyoku')
assert.equal(resumed?.data.packets.some(packet => packet.data.resumed === true), true, 'reconnect should seed the recovered kyoku')

assert.equal(loadLastUsedPaifuFileName(), 'Majak2Paifu.txt', 'the default filename should be used before file access')
saveLastUsedPaifuFileName(' imported-paifu.txt ')
assert.equal(loadLastUsedPaifuFileName(), 'imported-paifu.txt', 'the last successful filename should be restored')

console.log('paifu-recording source tests passed')