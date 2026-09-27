import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

import { ROLES, useAuthStore } from '@/stores/auth'

declare module 'vue-router' {
  interface RouteMeta {
    /** 不需登入即可進入 */
    public?: boolean
    /** 允許進入的角色；未指定表示僅需登入 */
    roles?: string[]
    title?: string
  }
}

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/LoginView.vue'),
    meta: { public: true, title: '登入' },
  },
  {
    path: '/kiosk',
    name: 'kiosk',
    component: () => import('@/views/KioskView.vue'),
    meta: { public: true, title: '自助取號' },
  },
  {
    path: '/display',
    name: 'display',
    component: () => import('@/views/DisplayView.vue'),
    meta: { public: true, title: '叫號顯示器' },
  },
  {
    path: '/q/:token',
    name: 'mobile-query',
    component: () => import('@/views/MobileQueryView.vue'),
    meta: { public: true, title: '手機查詢' },
  },
  {
    path: '/',
    component: () => import('@/layouts/AdminLayout.vue'),
    children: [
      { path: '', redirect: { name: 'dashboard' } },
      {
        path: 'dashboard',
        name: 'dashboard',
        component: () => import('@/views/DashboardView.vue'),
        meta: { title: '總覽' },
      },
      {
        path: 'counter',
        name: 'counter',
        component: () => import('@/views/CounterView.vue'),
        meta: { title: '櫃台叫號', roles: [ROLES.admin, ROLES.manager, ROLES.counter] },
      },
      {
        path: 'tickets',
        name: 'tickets',
        component: () => import('@/views/TicketsView.vue'),
        meta: { title: '票據查詢' },
      },
      {
        path: 'statistics',
        name: 'statistics',
        component: () => import('@/views/StatisticsView.vue'),
        meta: {
          title: '統計報表',
          roles: [ROLES.admin, ROLES.manager],
        },
      },
      {
        path: 'admin/services',
        name: 'admin-services',
        component: () => import('@/views/admin/ServicesAdminView.vue'),
        meta: { title: '服務類型', roles: [ROLES.admin, ROLES.manager] },
      },
      {
        path: 'admin/counters',
        name: 'admin-counters',
        component: () => import('@/views/admin/CountersAdminView.vue'),
        meta: { title: '櫃台管理', roles: [ROLES.admin, ROLES.manager] },
      },
      {
        path: 'admin/users',
        name: 'admin-users',
        component: () => import('@/views/admin/UsersAdminView.vue'),
        meta: { title: '使用者管理', roles: [ROLES.admin] },
      },
      {
        path: 'admin/settings',
        name: 'admin-settings',
        component: () => import('@/views/admin/SettingsAdminView.vue'),
        meta: { title: '系統設定', roles: [ROLES.admin] },
      },
      {
        path: 'account',
        name: 'account',
        component: () => import('@/views/AccountView.vue'),
        meta: { title: '我的帳號' },
      },
    ],
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('@/views/NotFoundView.vue'),
    meta: { public: true, title: '找不到頁面' },
  },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior: () => ({ top: 0 }),
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()

  if (auth.token && !auth.user) {
    await auth.restore()
  }

  if (to.meta.public) {
    // 已登入者不需再回到登入頁
    if (to.name === 'login' && auth.isAuthenticated) {
      return { name: 'dashboard' }
    }
    return true
  }

  if (!auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  const allowed = to.meta.roles
  if (allowed && allowed.length > 0 && !auth.hasRole(...allowed)) {
    return { name: 'dashboard' }
  }

  return true
})

router.afterEach((to) => {
  document.title = to.meta.title ? `${to.meta.title}｜排隊叫號系統` : '排隊叫號系統'
})
