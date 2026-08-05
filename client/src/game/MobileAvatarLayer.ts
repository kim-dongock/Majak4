interface MobileAvatarSlotState {
  url: string
  fallbackUrl: string
  x: number
  y: number
  width: number
  height: number
  visible: boolean
  alt: string
}

interface MobileTurnMarkState {
  url: string
  x: number
  y: number
  width: number
  height: number
  visible: boolean
}

interface MobileCallAvatarState {
  url: string
  fallbackUrl: string
  x: number
  y: number
  width: number
  height: number
}

const imageLoadCache = new Map<string, Promise<void>>()

function preloadImage(url: string): Promise<void> {
  const cached = imageLoadCache.get(url)
  if (cached) return cached

  const pending = new Promise<void>((resolve, reject) => {
    const image = new Image()
    image.decoding = 'async'
    image.onload = () => resolve()
    image.onerror = () => reject(new Error(`Failed to load avatar: ${url}`))
    image.src = url
  }).catch(error => {
    imageLoadCache.delete(url)
    throw error
  })
  imageLoadCache.set(url, pending)
  return pending
}

interface MobileAvatarSlot {
  image: HTMLImageElement
  requestId: number
  url: string
  fallbackUrl: string
}

export default class MobileAvatarLayer {
  private readonly root: HTMLDivElement
  private readonly slots: MobileAvatarSlot[]
  private readonly turnMark: HTMLImageElement

  constructor(parent: HTMLElement, onActivate: (loc: number) => void) {
    this.root = document.createElement('div')
    this.root.className = 'majak-mobile-avatar-layer'
    Object.assign(this.root.style, {
      position: 'absolute',
      left: '0',
      top: '0',
      width: '100%',
      height: '100%',
      zIndex: '10',
      overflow: 'hidden',
      pointerEvents: 'none',
    })

    this.slots = Array.from({ length: 4 }, (_, loc) => {
      const image = document.createElement('img')
      image.alt = ''
      image.decoding = 'async'
      image.draggable = false
      Object.assign(image.style, {
        position: 'absolute',
        display: 'none',
        objectFit: 'contain',
        objectPosition: 'center bottom',
        imageRendering: 'auto',
        pointerEvents: 'auto',
        cursor: 'pointer',
        transform: 'translateZ(0)',
      })
      image.addEventListener('click', () => onActivate(loc))
      this.root.appendChild(image)
      return { image, requestId: 0, url: '', fallbackUrl: '' }
    })

    this.turnMark = document.createElement('img')
    this.turnMark.alt = ''
    this.turnMark.draggable = false
    Object.assign(this.turnMark.style, {
      position: 'absolute',
      display: 'none',
      zIndex: '1',
      imageRendering: 'pixelated',
      pointerEvents: 'none',
    })
    this.root.appendChild(this.turnMark)

    parent.appendChild(this.root)
  }

  update(loc: number, state: MobileAvatarSlotState): void {
    const slot = this.slots[loc]
    if (!slot) return

    Object.assign(slot.image.style, {
      left: `${state.x}px`,
      top: `${state.y}px`,
      width: `${state.width}px`,
      height: `${state.height}px`,
    })
    slot.image.alt = state.alt
    slot.fallbackUrl = state.fallbackUrl

    if (!state.visible || !state.url) {
      slot.requestId += 1
      slot.image.style.display = 'none'
      return
    }

    const showLoadedImage = (url: string) => {
      slot.url = url
      slot.image.src = url
      slot.image.style.display = 'block'
    }
    if (slot.url === state.url && slot.image.complete && slot.image.naturalWidth > 0) {
      slot.image.style.display = 'block'
      return
    }

    const requestId = ++slot.requestId
    slot.image.style.display = 'none'
    void preloadImage(state.url).then(() => {
      if (slot.requestId !== requestId) return
      showLoadedImage(state.url)
    }).catch(() => {
      if (slot.requestId !== requestId || !state.fallbackUrl) return
      void preloadImage(state.fallbackUrl).then(() => {
        if (slot.requestId === requestId) showLoadedImage(state.fallbackUrl)
      }).catch(() => {})
    })
  }

  updateTurnMark(state: MobileTurnMarkState): void {
    Object.assign(this.turnMark.style, {
      display: state.visible && state.url ? 'block' : 'none',
      left: `${state.x}px`,
      top: `${state.y}px`,
      width: `${state.width}px`,
      height: `${state.height}px`,
    })
    if (state.url && this.turnMark.src !== state.url) this.turnMark.src = state.url
  }

  hideTurnMark(): void {
    this.turnMark.style.display = 'none'
  }

  showCallAvatar(state: MobileCallAvatarState): () => void {
    const image = document.createElement('img')
    image.alt = ''
    image.decoding = 'async'
    image.draggable = false
    Object.assign(image.style, {
      position: 'absolute',
      left: `${state.x}px`,
      top: `${state.y}px`,
      width: `${state.width}px`,
      height: `${state.height}px`,
      zIndex: '2',
      objectFit: 'contain',
      objectPosition: 'center center',
      imageRendering: 'auto',
      pointerEvents: 'none',
      transform: 'translateZ(0)',
    })
    image.onerror = () => {
      image.onerror = null
      if (state.fallbackUrl) image.src = state.fallbackUrl
    }
    image.src = state.url
    this.root.appendChild(image)
    return () => image.remove()
  }

  destroy(): void {
    this.slots.forEach(slot => { slot.requestId += 1 })
    this.root.remove()
  }
}