import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      redirect: '/world',
    },
    {
      path: '/world',
      name: 'world',
      component: () => import('@/game/world/views/WorldView.vue'),
    },
  ],
})

export default router
