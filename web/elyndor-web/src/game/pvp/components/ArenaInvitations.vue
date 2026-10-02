<script setup lang="ts">
import { shallowRef } from 'vue'
import type { ArenaInvitation } from '@/game/pvp/arenaContracts'
import { UIButton } from '@/ui/components'

defineProps<{
  invitations: ArenaInvitation[]
  pending: boolean
  unavailable: boolean
  notice: string | null
}>()
const emit = defineEmits<{
  invite: [name: string]
  respond: [id: string, action: 'accept' | 'decline' | 'cancel']
  refresh: []
}>()
const targetName = shallowRef('')
</script>

<template>
  <section class="arena-invites" aria-label="Дружеские бои">
    <header class="arena-invites__header">
      <div><h3>Вызов другу</h3><p>Реальные билды · без рейтинга и чести</p></div>
      <UIButton variant="ghost" :disabled="pending" @click="emit('refresh')">Обновить</UIButton>
    </header>
    <form class="arena-invites__form" @submit.prevent="emit('invite', targetName)">
      <label class="arena-invites__label" for="arena-friend-name">Имя персонажа друга</label>
      <input id="arena-friend-name" v-model="targetName" class="arena-invites__input"
        maxlength="32" placeholder="Например, tekwan" autocomplete="off" :disabled="pending || unavailable" />
      <UIButton type="submit" :disabled="pending || unavailable || !targetName.trim()">Пригласить</UIButton>
    </form>
    <p class="arena-invites__hint">Приглашение действует 5 минут. Для начала боя оба откройте арену. Друг получит уведомление от Telegram-бота, если ранее запускал его.</p>
    <p v-if="notice" class="arena-invites__notice" role="status">{{ notice }}</p>
    <article v-for="invite in invitations" :key="invite.id" class="arena-invites__challenge">
      <strong>{{ invite.incoming ? `${invite.inviterName} вызывает вас` : `Вызов: ${invite.targetName}` }}</strong>
      <span>До {{ new Date(invite.expiresAtUtc).toLocaleTimeString('ru', { hour: '2-digit', minute: '2-digit' }) }}</span>
      <div class="arena-invites__actions">
        <template v-if="invite.incoming">
          <UIButton :disabled="pending || unavailable" @click="emit('respond', invite.id, 'accept')">Принять бой</UIButton>
          <UIButton variant="ghost" :disabled="pending" @click="emit('respond', invite.id, 'decline')">Отклонить</UIButton>
        </template>
        <UIButton v-else variant="ghost" :disabled="pending" @click="emit('respond', invite.id, 'cancel')">Отменить вызов</UIButton>
      </div>
    </article>
  </section>
</template>

<style scoped>
.arena-invites { display: grid; gap: .8rem; padding: 1rem; border: 1px solid #8d7447; border-radius: .65rem; background: linear-gradient(145deg, #211c16, #10121a); }
.arena-invites__header { display: flex; align-items: center; justify-content: space-between; gap: .5rem; }
.arena-invites__header h3 { margin: 0; color: #e2c789; font-family: var(--font-display, Georgia, serif); }
.arena-invites__header p, .arena-invites__hint { margin: .3rem 0 0; color: #b5af9e; font-size: .8rem; line-height: 1.5; }
.arena-invites__form { display: grid; grid-template-columns: minmax(0, 1fr) auto; gap: .5rem; }
.arena-invites__label { grid-column: 1 / -1; font-size: .85rem; }
.arena-invites__input { width: 100%; min-width: 0; min-height: 44px; padding: .6rem; color: #f0e4cf; background: #0d1016; border: 1px solid #665944; border-radius: .4rem; font: inherit; }
.arena-invites__input:focus-visible { outline: 2px solid #e2c789; outline-offset: 2px; }
.arena-invites__notice { margin: 0; color: #b6d3a4; font-size: .85rem; }
.arena-invites__challenge { display: grid; gap: .5rem; padding: .8rem; background: #11131b; border: 1px solid #514938; border-radius: .5rem; overflow-wrap: anywhere; }
.arena-invites__challenge span { color: #b5af9e; font-size: .8rem; }
.arena-invites__actions { display: flex; flex-wrap: wrap; gap: .5rem; }
@media (max-width: 360px) { .arena-invites__form { grid-template-columns: 1fr; } .arena-invites__header { align-items: flex-start; } }
</style>
