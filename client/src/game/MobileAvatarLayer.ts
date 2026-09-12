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

interface MobileCallAvatarState {
  url: string
  fallbackUrl: string
  x: number
  y: number
  width: number
  height: number
}

const imageLoadCache = new Map<string, Promise<void>>()

const COSTUME_AVATAR_BOUNDS: Record<number, { x: number; y: number; width: number; height: number }> = {
  9: { x: 7, y: 37, width: 36, height: 52 },
  10: { x: 2, y: 34, width: 35, height: 55 },
  11: { x: 5, y: 32, width: 35, height: 57 },
}

const COSTUME_IMAGE_WIDTH = 45
const COSTUME_IMAGE_HEIGHT = 102

function costumeBounds(url: string) {
  const costumeId = Number(url.match(/\/skin\/(\d+)\//)?.[1])
  return COSTUME_AVATAR_BOUNDS[costumeId]
}

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
  frame: HTMLDivElement
  image: HTMLImageElement
  requestId: number
  url: string
  pendingUrl: string
  fallbackUrl: string
  entrance?: { offsetX: number; delayMs: number }
}

export default class MobileAvatarLayer {
  private readonly root: HTMLDivElement
  private readonly slots: MobileAvatarSlot[]

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
      const frame = document.createElement('div')
      Object.assign(frame.style, {
        position: 'absolute',
        display: 'none',
        overflow: 'hidden',
        pointerEvents: 'none',
      })
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
      })
      image.addEventListener('click', () => onActivate(loc))
      frame.appendChild(image)
      this.root.appendChild(frame)
      return { frame, image, requestId: 0, url: '', pendingUrl: '', fallbackUrl: '' }
    })

    parent.appendChild(this.root)
  }

  update(loc: number, state: MobileAvatarSlotState): void {
    const slot = this.slots[loc]
    if (!slot) return

    const bounds = costumeBounds(state.url)

    Object.assign(slot.frame.style, {
      left: `${Math.round(state.x)}px`,
      top: `${Math.round(state.y)}px`,
      width: `${Math.round(state.width)}px`,
      height: `${Math.round(state.height)}px`,
    })
    if (bounds) {
      const scale = Math.min(state.width / bounds.width, state.height / bounds.height)
      Object.assign(slot.image.style, {
        left: `${(state.width - bounds.width * scale) / 2 - bounds.x * scale}px`,
        top: `${(state.height - bounds.height * scale) / 2 - bounds.y * scale}px`,
        width: `${COSTUME_IMAGE_WIDTH * scale}px`,
        height: `${COSTUME_IMAGE_HEIGHT * scale}px`,
        objectFit: 'fill',
        objectPosition: 'initial',
      })
    } else {
      Object.assign(slot.image.style, {
        left: '0',
        top: '0',
        width: '100%',
        height: '100%',
        objectFit: 'contain',
        objectPosition: 'center bottom',
      })
    }
    slot.image.alt = state.alt
    slot.fallbackUrl = state.fallbackUrl

    if (!state.visible || !state.url) {
      slot.requestId += 1
      slot.pendingUrl = ''
      slot.frame.style.display = 'none'
      return
    }

    const showLoadedImage = (url: string) => {
      slot.url = url
      slot.pendingUrl = ''
      slot.image.src = url
      slot.image.style.display = 'block'
      slot.frame.style.display = 'block'
      if (slot.entrance) {
        const entrance = slot.entrance
        slot.entrance = undefined
        this.runEntrance(slot.frame, entrance.offsetX, entrance.delayMs)
      }
    }
    if (slot.url === state.url && slot.image.complete && slot.image.naturalWidth > 0) {
      slot.image.style.display = 'block'
      slot.frame.style.display = 'block'
      return
    }

    if (slot.pendingUrl === state.url) return

    const requestId = ++slot.requestId
    slot.pendingUrl = state.url
    if (!slot.url) slot.frame.style.display = 'none'
    void preloadImage(state.url).then(() => {
      if (slot.requestId !== requestId) return
      showLoadedImage(state.url)
    }).catch(() => {
      if (slot.requestId !== requestId || !state.fallbackUrl) return
      slot.pendingUrl = state.fallbackUrl
      void preloadImage(state.fallbackUrl).then(() => {
        if (slot.requestId === requestId) showLoadedImage(state.fallbackUrl)
      }).catch(() => {})
    })
  }

  playEntrance(loc: number, offsetX: number, delayMs: number): void {
    const slot = this.slots[loc]
    if (!slot) return
    if (slot.frame.style.display === 'none') {
      slot.entrance = { offsetX, delayMs }
      return
    }
    this.runEntrance(slot.frame, offsetX, delayMs)
  }

  private runEntrance(frame: HTMLDivElement, offsetX: number, delayMs: number): void {
    frame.getAnimations().forEach(animation => animation.cancel())
    const animation = frame.animate([
      { transform: `translateX(${offsetX}px)`, opacity: 0 },
      { transform: 'translateX(0)', opacity: 1 },
    ], {
      duration: 480,
      delay: delayMs,
      easing: 'cubic-bezier(0.22, 1, 0.36, 1)',
      fill: 'both',
    })
    animation.onfinish = () => {
      frame.style.transform = ''
      frame.style.opacity = ''
    }
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
    })
    image.onerror = () => {
      image.onerror = null
      if (state.fallbackUrl) image.src = state.fallbackUrl
    }
    image.src = state.url
    this.root.appendChild(image)
    return () => image.remove()
  }

  setVisible(visible: boolean): void {
    this.root.hidden = !visible
    this.root.style.display = visible ? 'block' : 'none'
  }

  destroy(): void {
    this.slots.forEach(slot => { slot.requestId += 1 })
    this.root.remove()
  }
}