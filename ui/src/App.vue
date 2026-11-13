<script setup lang="ts">
/**
 * AppShell 根（spec §2.1 / §9.1）：菜单栏 → 标题区 → 主区（工作台/文件夹）→ 状态栏 + 浮层宿主。
 * 全局状态在 stores/workbench.ts；此文件只做组装、生命周期与全局快捷键。
 */
import { computed, onBeforeUnmount, onMounted } from 'vue'
import MenuBar from './components/shell/MenuBar.vue'
import TitleBar from './components/shell/TitleBar.vue'
import StatusBar from './components/shell/StatusBar.vue'
import WorkbenchView from './components/workbench/WorkbenchView.vue'
import FolderView from './components/folder/FolderView.vue'
import ToastHost from './components/overlays/ToastHost.vue'
import DrawerHost from './components/overlays/DrawerHost.vue'
import ModalHost from './components/overlays/ModalHost.vue'
import ConfirmDialog from './components/overlays/ConfirmDialog.vue'
import ContextMenu from './components/overlays/ContextMenu.vue'
import { useWorkbench } from './stores/workbench'
import { REPO_ACTIONS } from './types/workbench'

const w = useWorkbench()
const state = w.state

const themeAttr = computed(() => (state.theme === 'system' ? undefined : state.theme))
let timer: ReturnType<typeof window.setInterval> | undefined

function onKeydown(event: KeyboardEvent): void {
  const target = event.target as HTMLElement | null
  const editing =
    target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA' || target?.isContentEditable === true

  if (event.key === 'Escape') {
    event.preventDefault()
    w.closeTopOverlay()
    return
  }
  if (event.key.toLowerCase() === 'k' && (event.ctrlKey || event.metaKey)) {
    event.preventDefault()
    w.focusSearch()
    return
  }
  if (event.key === ',' && (event.ctrlKey || event.metaKey)) {
    event.preventDefault()
    w.openModal('settings')
    return
  }
  if ((event.key === '1' || event.key === '2') && (event.ctrlKey || event.metaKey)) {
    event.preventDefault()
    w.setView(event.key === '1' ? 'workbench' : 'folder')
    return
  }
  if (event.key === 'F5') {
    event.preventDefault()
    void w.refresh()
    return
  }
  if (event.altKey && !editing && ['1', '2', '3', '4', '5'].includes(event.key)) {
    event.preventDefault()
    const action = REPO_ACTIONS[Number(event.key) - 1]
    if (action) void w.runAction(action)
  }
}

onMounted(() => {
  void w.init()
  void w.loadHostInfo()
  timer = window.setInterval(() => w.tickNow(), 5_000)
  window.addEventListener('keydown', onKeydown)
})

onBeforeUnmount(() => {
  if (timer !== undefined) window.clearInterval(timer)
  window.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <div class="app-shell" data-testid="app-shell" :data-theme="themeAttr" :data-view="state.view">
    <MenuBar />
    <TitleBar />
    <main class="main-area" aria-label="主工作区">
      <WorkbenchView v-if="state.view === 'workbench'" :key="'workbench'" />
      <FolderView v-else :key="'folder'" />
    </main>
    <StatusBar />
    <ToastHost />
    <DrawerHost />
    <ModalHost />
    <ContextMenu />
    <ConfirmDialog />
  </div>
</template>
