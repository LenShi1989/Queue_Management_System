<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import { LayoutDashboard, Ticket, BarChart3, LogOut, Menu, Settings, Users, Building2, UserCog, X } from 'lucide-vue-next'
import { ref } from 'vue'

import { ROLES, useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()
const mobileOpen = ref(false)

interface NavItem {
  name: string
  label: string
  icon: typeof LayoutDashboard
  roles?: string[]
}

const NAV: NavItem[] = [
  { name: 'dashboard', label: '總覽', icon: LayoutDashboard },
  { name: 'counter', label: '櫃台叫號', icon: Ticket, roles: [ROLES.admin, ROLES.manager, ROLES.counter] },
  { name: 'tickets', label: '票據查詢', icon: Ticket },
  { name: 'statistics', label: '統計報表', icon: BarChart3, roles: [ROLES.admin, ROLES.manager] },
  { name: 'admin-services', label: '服務類型', icon: Building2, roles: [ROLES.admin, ROLES.manager] },
  { name: 'admin-counters', label: '櫃台管理', icon: UserCog, roles: [ROLES.admin, ROLES.manager] },
  { name: 'admin-users', label: '使用者', icon: Users, roles: [ROLES.admin] },
  { name: 'admin-settings', label: '系統設定', icon: Settings, roles: [ROLES.admin] },
]

const items = computed(() =>
  NAV.filter((item) => !item.roles || item.roles.length === 0 || auth.hasRole(...item.roles)),
)

const isActive = (name: string): boolean => route.name === name

async function logout(): Promise<void> {
  auth.logout()
  await router.replace({ name: 'login' })
}
</script>

<template>
  <div class="min-h-screen lg:flex">
    <!-- 側邊欄 -->
    <aside
      class="fixed inset-y-0 left-0 z-40 w-64 shrink-0 border-r border-slate-800 bg-slate-900 text-slate-300
        transition-transform lg:static lg:translate-x-0"
      :class="mobileOpen ? 'translate-x-0' : '-translate-x-full'"
    >
      <div class="flex h-16 items-center justify-between border-b border-slate-800 px-5">
        <div>
          <p class="text-sm font-bold text-white">排隊叫號系統</p>
          <p class="text-xs text-slate-500">Queue Management</p>
        </div>
        <button class="text-slate-400 lg:hidden" type="button" @click="mobileOpen = false">
          <X class="size-5" />
        </button>
      </div>

      <nav class="flex-1 space-y-1 overflow-y-auto p-3">
        <RouterLink
          v-for="item in items"
          :key="item.name"
          :to="{ name: item.name }"
          class="flex items-center gap-3 rounded-lg px-3 py-2 text-sm transition"
          :class="isActive(item.name) ? 'bg-brand-600 text-white' : 'hover:bg-slate-800 hover:text-white'"
          @click="mobileOpen = false"
        >
          <component :is="item.icon" class="size-4 shrink-0" />
          <span>{{ item.label }}</span>
        </RouterLink>
      </nav>

      <div class="border-t border-slate-800 p-4 text-sm">
        <p class="font-medium text-white">{{ auth.displayName }}</p>
        <p class="mt-0.5 text-xs text-slate-500">{{ auth.roles.join('、') || '未指派角色' }}</p>
        <div class="mt-3 flex gap-2">
          <RouterLink
            :to="{ name: 'account' }"
            class="flex-1 rounded-lg border border-slate-700 px-3 py-1.5 text-center text-xs hover:bg-slate-800"
          >
            帳號
          </RouterLink>
          <button
            class="flex flex-1 items-center justify-center gap-1 rounded-lg border border-slate-700 px-3 py-1.5 text-xs hover:bg-slate-800"
            type="button"
            @click="logout"
          >
            <LogOut class="size-3" /> 登出
          </button>
        </div>
      </div>
    </aside>

    <div v-if="mobileOpen" class="fixed inset-0 z-30 bg-black/40 lg:hidden" @click="mobileOpen = false" />

    <!-- 主要內容 -->
    <div class="flex min-w-0 flex-1 flex-col">
      <header class="flex h-16 items-center gap-3 border-b border-slate-200 bg-white px-4 lg:hidden">
        <button class="text-slate-600" type="button" @click="mobileOpen = true">
          <Menu class="size-6" />
        </button>
        <p class="font-semibold text-slate-800">{{ route.meta.title ?? '' }}</p>
      </header>

      <main class="flex-1 p-4 lg:p-6">
        <RouterView />
      </main>
    </div>
  </div>
</template>
