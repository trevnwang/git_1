<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted } from 'vue'
import { useWorkbench } from '../../stores/workbench'
import type { MenuItemVm } from '../../types/workbench'

const w = useWorkbench()
const state = w.state

interface MenuDef {
  key: string
  label: string
  items: MenuItemVm[]
}

const MENUS: MenuDef[] = [
  {
    key: 'file',
    label: '文件(F)',
    items: [
      { key: 'file.openWorkspace', label: '打开工作区…' },
      { key: 'file.rescan', label: '重新扫描', shortcut: 'F5' },
      { key: 'file.auth', label: '认证与凭据…' },
      { key: 'sep1', label: '', separator: true },
      { key: 'file.exit', label: '退出' },
    ],
  },
  {
    key: 'edit',
    label: '编辑(E)',
    items: [
      { key: 'edit.search', label: '搜索', shortcut: 'Ctrl+K' },
      { key: 'edit.copyPath', label: '复制所选路径' },
      { key: 'edit.selectAll', label: '全选筛选结果', shortcut: 'Ctrl+A' },
    ],
  },
  {
    key: 'view',
    label: '视图(V)',
    items: [
      { key: 'view.workbench', label: '工作台', shortcut: 'Ctrl+1' },
      { key: 'view.folder', label: '文件夹视图', shortcut: 'Ctrl+2' },
      { key: 'sep2', label: '', separator: true },
      { key: 'view.ignored', label: '显示已忽略文件' },
      { key: 'view.compact', label: '紧凑密度' },
      { key: 'view.theme', label: '主题（跟随系统/浅色/深色）' },
    ],
  },
  {
    key: 'actions',
    label: '操作(O)',
    items: [
      { key: 'actions.PULL', label: '单仓 PULL', shortcut: 'Alt+1' },
      { key: 'actions.FETCH', label: '单仓 FETCH', shortcut: 'Alt+2' },
      { key: 'actions.CHKOUT', label: '单仓 CHKOUT', shortcut: 'Alt+3' },
      { key: 'actions.COMMIT', label: '单仓 COMMIT', shortcut: 'Alt+4' },
      { key: 'actions.PUSH', label: '单仓 PUSH', shortcut: 'Alt+5' },
      { key: 'sep3', label: '', separator: true },
      { key: 'component.SYNC', label: '组件 SYNC…（M2）' },
      { key: 'component.CREATE', label: '组件 CREATE…（M2）' },
      { key: 'component.CLONE', label: '批量 Clone…（M2）' },
      { key: 'component.DELETE', label: '批量 Delete…（M2）', danger: true },
      { key: 'tools.tasks', label: '任务中心' },
    ],
  },
  {
    key: 'tools',
    label: '工具(T)',
    items: [
      { key: 'tools.ci', label: 'CI 流水线' },
      { key: 'tools.unbound', label: '未绑定仓库' },
      { key: 'tools.logs', label: '日志与诊断' },
      { key: 'sep4', label: '', separator: true },
      { key: 'tools.settings', label: '设置', shortcut: 'Ctrl+,' },
    ],
  },
  {
    key: 'help',
    label: '帮助(H)',
    items: [
      { key: 'help.about', label: '关于' },
      { key: 'help.shortcuts', label: '快捷键' },
      { key: 'help.update', label: '检查更新（v1.1）', disabled: true },
    ],
  },
]

const openMenu = computed(() => MENUS.find((menu) => menu.key === state.menuOpen) ?? null)

function toggleMenu(key: string): void {
  state.menuOpen = state.menuOpen === key ? null : key
}

function hoverMenu(key: string): void {
  if (state.menuOpen) state.menuOpen = key
}

function run(item: MenuItemVm): void {
  if (item.separator || item.disabled) return
  w.command(item.key)
  state.menuOpen = null
}

function onDocumentPointerDown(event: MouseEvent): void {
  const target = event.target as HTMLElement | null
  if (!target?.closest('[data-testid="menubar"]')) state.menuOpen = null
}

onMounted(() => document.addEventListener('mousedown', onDocumentPointerDown))
onBeforeUnmount(() => document.removeEventListener('mousedown', onDocumentPointerDown))
</script>

<template>
  <nav class="menu-bar" data-testid="menubar" role="menubar" aria-label="主菜单">
    <div v-for="menu in MENUS" :key="menu.key" class="menu-root">
      <button
        type="button"
        class="menu-trigger"
        role="menuitem"
        :aria-expanded="state.menuOpen === menu.key"
        :class="{ active: state.menuOpen === menu.key }"
        :data-testid="`menu-${menu.key}`"
        @click="toggleMenu(menu.key)"
        @mouseenter="hoverMenu(menu.key)"
      >
        {{ menu.label }}
      </button>
      <transition name="menu-fade">
        <div v-if="state.menuOpen === menu.key && openMenu" class="menu-pop" role="menu">
          <template v-for="item in menu.items" :key="item.key">
            <div v-if="item.separator" class="menu-sep" role="separator" />
            <button
              v-else
              type="button"
              class="menu-item"
              role="menuitem"
              :class="{ danger: item.danger }"
              :disabled="item.disabled"
              @click="run(item)"
            >
              <span class="label">{{ item.label }}</span>
              <span v-if="item.shortcut" class="shortcut">{{ item.shortcut }}</span>
            </button>
          </template>
        </div>
      </transition>
    </div>
    <span class="spacer" />
  </nav>
</template>

<style scoped>
.menu-bar {
  display: flex;
  align-items: center;
  gap: 2px;
  padding: 0 8px;
  height: var(--layout-menu-h);
  background: var(--surface);
  border-bottom: 1px solid var(--border);
  position: relative;
  z-index: var(--z-menu);
  overflow: visible;
}
.menu-root { position: relative; }
.menu-trigger {
  height: calc(var(--layout-menu-h) - 8px);
  padding: 0 8px;
  border: 0;
  border-radius: 4px;
  background: transparent;
  color: var(--text);
  font-size: 12px;
  cursor: pointer;
  white-space: nowrap;
}
.menu-trigger:hover, .menu-trigger.active { background: var(--surface-2); }
.menu-pop {
  position: absolute;
  top: calc(var(--layout-menu-h) - 3px);
  left: 0;
  min-width: 240px;
  padding: 4px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--r-panel);
  box-shadow: 0 8px 24px rgb(16 24 40 / 18%);
  z-index: var(--z-menu);
}
.menu-item {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 16px;
  justify-content: space-between;
  height: 28px;
  padding: 0 8px;
  border: 0;
  border-radius: 4px;
  background: transparent;
  color: var(--text);
  font-size: 12px;
  cursor: pointer;
  text-align: left;
  white-space: nowrap;
}
.menu-item:hover:not(:disabled) { background: var(--surface-2); }
.menu-item:disabled { opacity: 0.5; cursor: default; }
.menu-item.danger { color: var(--st-danger-fg); }
.menu-sep { height: 1px; margin: 4px 6px; background: var(--border); }
.shortcut { color: var(--text-2); font-size: 11px; }
.spacer { flex: 1; }
.menu-fade-enter-active, .menu-fade-leave-active { transition: opacity 100ms var(--ease); }
.menu-fade-enter-from, .menu-fade-leave-to { opacity: 0; }
@media (prefers-reduced-motion: reduce) {
  .menu-fade-enter-active, .menu-fade-leave-active { transition: none; }
}
</style>
