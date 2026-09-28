<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { claimMail, commerceMessage, loadMailbox, type CommerceMail } from '@/game/economy/commerce'
import { useGameSessionStore } from '@/stores/gameSession'
import ItemIcon from '@/game/items/components/ItemIcon.vue'

const session = useGameSessionStore()
const letters = ref<CommerceMail[]>([])
const loading = ref(false)
const claiming = ref<string | null>(null)
const error = ref('')
async function refresh(): Promise<void> {
  loading.value = true; error.value = ''
  try { letters.value = await loadMailbox() }
  catch (failure) { error.value = commerceMessage(failure instanceof Error ? failure.message : 'mail_load_failed') }
  finally { loading.value = false }
}
async function claim(id: string): Promise<void> {
  if (claiming.value) return
  claiming.value = id; error.value = ''
  try { await claimMail(id); await Promise.all([refresh(), session.refreshSnapshot()]) }
  catch (failure) { error.value = commerceMessage(failure instanceof Error ? failure.message : 'mail_claim_failed') }
  finally { claiming.value = null }
}
onMounted(() => { void refresh() })
</script>

<template>
  <section class="mailbox-view" data-mailbox-view>
    <header><small>ДОСТАВКА ПРЕДМЕТОВ</small><h2>Почта</h2></header>
    <p v-if="error" role="alert" class="error">{{ error }}</p>
    <p v-if="loading">Проверяем почту…</p>
    <p v-else-if="!letters.length">Новых посылок нет.</p>
    <article v-for="mail in letters" :key="mail.id" class="mail-row">
      <ItemIcon :icon-id="mail.iconId" :item-id="mail.itemId" :name="mail.name" type="Other" />
      <div><strong>{{ mail.name }}</strong><small>{{ mail.source === 'PURCHASE' ? 'Покупка' : 'Возврат лота' }} · ×{{ mail.quantity }} · {{ new Date(mail.createdAt).toLocaleDateString('ru-RU') }}</small></div>
      <button type="button" :disabled="claiming !== null" @click="claim(mail.id)">{{ claiming === mail.id ? 'Забираем…' : 'Забрать' }}</button>
    </article>
  </section>
</template>

<style scoped>
.mailbox-view { display:grid; gap:10px; } header small { color:var(--ui-color-gold); font-size:.65rem; letter-spacing:.12em; } h2 { margin:2px 0; font-family:var(--ui-font-display); }
.mail-row { display:grid; grid-template-columns:48px minmax(0,1fr) auto; gap:8px; align-items:center; padding:8px; border:1px solid var(--ui-color-border); border-radius:var(--ui-radius-md); background:var(--ui-gradient-panel); }
.mail-row :deep(.item-icon) { width:48px; height:48px; } .mail-row div { display:grid; min-width:0; gap:3px; } .mail-row strong { overflow:hidden; text-overflow:ellipsis; white-space:nowrap; } .mail-row small { color:var(--ui-color-text-muted); }
button { min-height:44px; padding:8px; border:1px solid var(--ui-color-gold-muted); border-radius:var(--ui-radius-sm); background:#171713; color:var(--ui-color-text-primary); font:inherit; } button:disabled { opacity:.5; } .error { color:var(--ui-color-danger,#ff8d8d); }
@media(max-width:360px) { .mail-row { grid-template-columns:42px minmax(0,1fr); } .mail-row button { grid-column:2; } }
</style>
