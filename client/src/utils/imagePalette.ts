const SAMPLE_SIZE = 48
const EDGE_RATIO = 0.12

function median(values: number[]): number {
  values.sort((left, right) => left - right)
  return values[Math.floor(values.length / 2)] ?? 0
}

export function edgeColorFromPixels(data: Uint8ClampedArray, width: number, height: number): string | undefined {
  if (width <= 0 || height <= 0 || data.length < width * height * 4) return undefined

  const edgeX = Math.max(1, Math.ceil(width * EDGE_RATIO))
  const edgeY = Math.max(1, Math.ceil(height * EDGE_RATIO))
  const red: number[] = []
  const green: number[] = []
  const blue: number[] = []

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      if (x >= edgeX && x < width - edgeX && y >= edgeY && y < height - edgeY) continue
      const index = (y * width + x) * 4
      if (data[index + 3] < 128) continue
      red.push(data[index])
      green.push(data[index + 1])
      blue.push(data[index + 2])
    }
  }

  if (red.length === 0) return undefined
  return `rgb(${median(red)}, ${median(green)}, ${median(blue)})`
}

export function imageEdgeColor(image: CanvasImageSource): string | undefined {
  const canvas = document.createElement('canvas')
  canvas.width = SAMPLE_SIZE
  canvas.height = SAMPLE_SIZE
  const context = canvas.getContext('2d', { willReadFrequently: true })
  if (!context) return undefined
  context.drawImage(image, 0, 0, SAMPLE_SIZE, SAMPLE_SIZE)
  return edgeColorFromPixels(context.getImageData(0, 0, SAMPLE_SIZE, SAMPLE_SIZE).data, SAMPLE_SIZE, SAMPLE_SIZE)
}

export function loadImageEdgeColor(src: string): Promise<string | undefined> {
  return new Promise(resolve => {
    const image = new Image()
    image.onload = () => {
      try {
        resolve(imageEdgeColor(image))
      } catch {
        resolve(undefined)
      }
    }
    image.onerror = () => resolve(undefined)
    image.src = src
  })
}