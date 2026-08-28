import { describe, expect, it } from 'vitest'
import { edgeColorFromPixels } from './imagePalette'

describe('edgeColorFromPixels', () => {
  it('uses the image edge and ignores the center', () => {
    const width = 10
    const height = 10
    const pixels = new Uint8ClampedArray(width * height * 4)
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        const edge = x < 2 || x >= 8 || y < 2 || y >= 8
        const index = (y * width + x) * 4
        pixels.set(edge ? [120, 20, 30, 255] : [10, 200, 10, 255], index)
      }
    }

    expect(edgeColorFromPixels(pixels, width, height)).toBe('rgb(120, 20, 30)')
  })

  it('returns undefined when no visible edge pixels exist', () => {
    expect(edgeColorFromPixels(new Uint8ClampedArray(4 * 4 * 4), 4, 4)).toBeUndefined()
  })
})