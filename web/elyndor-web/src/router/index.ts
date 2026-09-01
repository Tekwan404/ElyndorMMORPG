import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

const developmentRoutes: RouteRecordRaw[] = import.meta.env.DEV
  ? [
      {
        path: '/dev/ui',
        name: 'ui-playground',
        component: () => import('@/ui/playground/UiPlaygroundView.vue'),
      },
      {
        path: '/dev/talents',
        name: 'talent-tree-playground',
        component: () => import('@/game/talents/views/WarriorTalentTreeView.vue'),
      },
    ]
  : []

export function createRoutes(isDevelopment: boolean): RouteRecordRaw[] {
  const routes: RouteRecordRaw[] = [
    {
      path: '/',
      redirect: '/world',
    },
    {
      path: '/world',
      name: 'world',
      component: () => import('@/app/AppShell.vue'),
    },
  ]

  if (isDevelopment) routes.push(...developmentRoutes)

  return routes
}

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: createRoutes(import.meta.env.DEV),
})

export default router
