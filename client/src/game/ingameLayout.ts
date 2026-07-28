// Legacy CMJGameWnd / MJTblDraw desktop coordinates. Keep these values pixel-identical.
export const INGAME_WORLD = {
  width: 1019,
  height: 735,
} as const

export type IngameLayoutMode = 'desktop' | 'mobileLandscape'

export const DESKTOP_INGAME_LAYOUT = {
  board: {
    x: 5,
    y: 31,
    width: 789,
    height: 704,
  },
  sidePanel: {
    x: 794,
    y: 31,
    width: 225,
    height: 704,
  },
  dragonOverlay: {
    x: 150,
    y: 90,
  },
  centerInfo: {
    x: 262,
    y: 275,
    width: 265,
    height: 161,
  },
  panel: {
    x: 102,
    y: 644,
    width: 580,
    height: 60,
    depth: 1000,
  },
  actionButtons: {
    kan: { x: 257, y: 662, width: 66, height: 40 },
    pon: { x: 324, y: 662, width: 66, height: 40 },
    chi: { x: 391, y: 662, width: 66, height: 40 },
    reach: { x: 458, y: 662, width: 66, height: 40 },
    ron: { x: 525, y: 662, width: 66, height: 40 },
    tsumo: { x: 525, y: 662, width: 66, height: 40 },
    pass: { x: 598, y: 662, width: 76, height: 40 },
    flow: { x: 598, y: 662, width: 76, height: 40 },
    hua: { x: 598, y: 662, width: 58, height: 17 },
    horaError: { x: 525, y: 662, width: 66, height: 40 },
  },
  handOpenOffset: [
    { x:   0, y:  0 },
    { x: -15, y: 18 },
    { x:   0, y: 17 },
    { x:   0, y: 18 },
    { x:   0, y:  0 },
  ],
  handPosition: [
    { x: 127, y: 581 },
    { x: 757, y: 499 },
    { x: 604, y:   0 },
    { x:   2, y: 125 },
    { x: 127, y: 581 },
  ],
  handStep: [
    { x: 31, y:   0 },
    { x:  0, y: -26 },
    { x:-31, y:   0 },
    { x:  0, y:  26 },
    { x: 37, y:   0 },
  ],
  drawTileOffset: [
    { x:  4, y:  0 },
    { x:  0, y: -4 },
    { x: -4, y:  0 },
    { x:  0, y:  4 },
    { x:  4, y:  0 },
  ],
  discardPosition: [
    { x: 301, y: 427 },
    { x: 552, y: 396 },
    { x: 456, y: 171 },
    { x: 192, y: 266 },
  ],
  discardStep: [
    { x: 31, y:   0 },
    { x:  0, y: -26 },
    { x:-31, y:   0 },
    { x:  0, y:  26 },
  ],
  discardRowStep: [
    { x:   0, y:  38 },
    { x:  45, y:   0 },
    { x:   0, y: -38 },
    { x: -45, y:   0 },
  ],
  rotatedDiscardOffset: [
    { x:   0, y:  12 },
    { x:  14, y: -12 },
    { x: -14, y:   0 },
    { x:   0, y:   0 },
  ],
  meldPosition: [
    { x: 686, y: 589 },
    { x: 742, y:  98 },
    { x: 111, y:  17 },
    { x:   2, y: 562 },
  ],
  deadWall: {
    position: { x: 285, y: 228 },
    exposeOffsetY: 17,
  },
  boardEffectPosition: [
    { x: 254, y: 503 },
    { x: 658, y: 214 },
    { x: 251, y:  76 },
    { x:  53, y: 214 },
  ],
  reachTileEffectOffset: [
    { x: -41, y:  -7 },
    { x:  -5, y: -109 },
    { x:-221, y:  -7 },
    { x:  -5, y:  47 },
  ],
  reachTileEffectMove: [
    { x: -30, y:   0 },
    { x:   0, y:  26 },
    { x:  30, y:   0 },
    { x:   0, y: -26 },
  ],
  paifuGraph: {
    bounds: { x: 114, y: 88, w: 792, h: 560 },
    rows: [0, 1, 2, 3].map(row => ({
      name: { x: 70, y: 60 + 125 * row },
      point: { x: 720, y: 69 + 125 * row },
      initial: { x: 143, y: 61 + 125 * row },
      draw: { x: 143, y: 93 + 125 * row },
      discard: { x: 143, y: 119 + 125 * row },
      final: { x: 143, y: 152 + 125 * row },
    })),
    optionMeldGap: 9,
  },
} as const

type WidenLayout<T> = T extends number
  ? number
  : T extends readonly (infer U)[]
    ? ReadonlyArray<WidenLayout<U>>
    : T extends object
      ? { readonly [K in keyof T]: WidenLayout<T[K]> }
      : T

export type IngameLayout = WidenLayout<typeof DESKTOP_INGAME_LAYOUT>

export const MOBILE_INGAME_LAYOUT: IngameLayout = {
  ...DESKTOP_INGAME_LAYOUT,
  centerInfo: {
    x: 262,
    y: 246,
    width: 265,
    height: 161,
  },
  panel: {
    x: 205,
    y: 464,
    width: 580,
    height: 60,
    depth: 1000,
  },
  actionButtons: {
    kan: { x: 240, y: 482, width: 66, height: 40 },
    pon: { x: 307, y: 482, width: 66, height: 40 },
    chi: { x: 374, y: 482, width: 66, height: 40 },
    reach: { x: 441, y: 482, width: 66, height: 40 },
    ron: { x: 508, y: 482, width: 66, height: 40 },
    tsumo: { x: 508, y: 482, width: 66, height: 40 },
    pass: { x: 581, y: 482, width: 76, height: 40 },
    flow: { x: 581, y: 482, width: 76, height: 40 },
    hua: { x: 581, y: 482, width: 58, height: 17 },
    horaError: { x: 508, y: 482, width: 66, height: 40 },
  },
  handPosition: [
    { x: 203, y: 482 },
    { x: 642, y: 365 },
    { x: 430, y: 190 },
    { x: 110, y: 325 },
    { x: 203, y: 482 },
  ],
  handStep: [
    { x: 24, y:   0 },
    { x:  0, y: -20 },
    { x:-24, y:   0 },
    { x:  0, y:  20 },
    { x: 29, y:   0 },
  ],
  drawTileOffset: [
    { x:  3, y:  0 },
    { x:  0, y: -3 },
    { x: -3, y:  0 },
    { x:  0, y:  3 },
    { x:  3, y:  0 },
  ],
  discardPosition: [
    { x: 325, y: 395 },
    { x: 515, y: 380 },
    { x: 480, y: 228 },
    { x: 256, y: 246 },
  ],
  discardStep: [
    { x: 18, y:   0 },
    { x:  0, y: -15 },
    { x:-18, y:   0 },
    { x:  0, y:  15 },
  ],
  discardRowStep: [
    { x:   0, y:  22 },
    { x:  26, y:   0 },
    { x:   0, y: -22 },
    { x: -26, y:   0 },
  ],
  rotatedDiscardOffset: [
    { x:   0, y:   7 },
    { x:   8, y:  -7 },
    { x:  -8, y:   0 },
    { x:   0, y:   0 },
  ],
  meldPosition: [
    { x: 686, y: 548 },
    { x: 742, y: 245 },
    { x: 111, y: 190 },
    { x:   2, y: 548 },
  ],
  deadWall: {
    position: { x: 510, y: 230 },
    exposeOffsetY: 17,
  },
  boardEffectPosition: [
    { x: 254, y: 472 },
    { x: 658, y: 275 },
    { x: 251, y: 210 },
    { x:  53, y: 275 },
  ],
}

export function getIngameLayout(mode: IngameLayoutMode = 'desktop'): IngameLayout {
  return mode === 'mobileLandscape' ? MOBILE_INGAME_LAYOUT : DESKTOP_INGAME_LAYOUT
}