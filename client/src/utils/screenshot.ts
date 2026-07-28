function timestampForFilename(date = new Date()): string {
  const pad = (value: number) => String(value).padStart(2, '0')
  return [
    date.getFullYear(),
    pad(date.getMonth() + 1),
    pad(date.getDate()),
    '_',
    pad(date.getHours()),
    pad(date.getMinutes()),
    pad(date.getSeconds()),
  ].join('')
}

function findLargestVisibleCanvas(root: ParentNode): HTMLCanvasElement | null {
  const canvases = Array.from(root.querySelectorAll('canvas'))
    .filter((canvas): canvas is HTMLCanvasElement => canvas instanceof HTMLCanvasElement)
    .map(canvas => ({ canvas, rect: canvas.getBoundingClientRect() }))
    .filter(item => item.rect.width > 0 && item.rect.height > 0)
    .sort((a, b) => (b.rect.width * b.rect.height) - (a.rect.width * a.rect.height))

  return canvases[0]?.canvas ?? null
}

export async function saveLargestCanvasScreenshot({
  root = document,
  filenamePrefix = 'majak-screen',
}: {
  root?: ParentNode
  filenamePrefix?: string
} = {}): Promise<string> {
  const canvas = findLargestVisibleCanvas(root)
  if (!canvas) throw new Error('CAPTURE_CANVAS_NOT_FOUND')

  const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/png'))
  if (!blob) throw new Error('CAPTURE_CANVAS_FAILED')

  const filename = `${filenamePrefix}-${timestampForFilename()}.png`
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = filename
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(url)

  return filename
}