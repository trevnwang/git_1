/**
 * 固定行高虚拟滚动（spec §4.5 / §5.5）。
 * 只渲染 [start-overscan, end+overscan) 行，DOM 行数远小于总行数。
 */
import { computed, onBeforeUnmount, onMounted, ref, watch, type Ref } from 'vue'

export interface VirtualRows {
  scrollTop: import('vue').Ref<number>
  start: import('vue').ComputedRef<number>
  end: import('vue').ComputedRef<number>
  offsetY: import('vue').ComputedRef<number>
  totalHeight: import('vue').ComputedRef<number>
  viewportH: import('vue').Ref<number>
  onScroll: () => void
  scrollToIndex: (index: number) => void
}

export function useVirtualRows(
  scroller: Ref<HTMLElement | null>,
  rowHeight: () => number,
  total: () => number,
  overscan = 6,
): VirtualRows {
  const scrollTop = ref(0)
  const viewportH = ref(0)
  let observer: ResizeObserver | null = null

  const start = computed(() => Math.max(0, Math.floor(scrollTop.value / rowHeight()) - overscan))
  const end = computed(() =>
    Math.min(
      total(),
      Math.ceil((scrollTop.value + Math.max(viewportH.value, rowHeight())) / rowHeight()) + overscan,
    ),
  )
  const offsetY = computed(() => start.value * rowHeight())
  const totalHeight = computed(() => total() * rowHeight())

  function measure(): void {
    const node = scroller.value
    if (!node) return
    viewportH.value = node.clientHeight
    scrollTop.value = node.scrollTop
  }

  function onScroll(): void {
    measure()
  }

  function scrollToIndex(index: number): void {
    const node = scroller.value
    if (!node) return
    const target = Math.max(0, index) * rowHeight()
    const top = Math.min(Math.max(0, target - (node.clientHeight - rowHeight()) / 2), totalHeight.value)
    node.scrollTop = top
    measure()
  }

  onMounted(() => {
    measure()
    const node = scroller.value
    if (node && typeof ResizeObserver !== 'undefined') {
      observer = new ResizeObserver(() => measure())
      observer.observe(node)
    }
  })

  onBeforeUnmount(() => {
    observer?.disconnect()
    observer = null
  })

  watch(scroller, () => {
    measure()
    const node = scroller.value
    if (node && !observer && typeof ResizeObserver !== 'undefined') {
      observer = new ResizeObserver(() => measure())
      observer.observe(node)
    }
  })

  return { scrollTop, start, end, offsetY, totalHeight, viewportH, onScroll, scrollToIndex }
}
