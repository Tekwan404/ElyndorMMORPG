<script setup lang="ts">
import { computed, ref } from 'vue'
import { useGameSessionStore } from '@/stores/gameSession'
import { personalizationError } from '@/game/economy/personalizationContracts'
import { UIButton, UIModal } from '@/ui/components'
const props = defineProps<{ price: number; balance: number }>()
const emit = defineEmits<{ changed: [] }>()
const session = useGameSessionStore()
const name = ref('')
const confirm = ref(false)
const message = ref<string | null>(null)
const pending = computed(() => session.isMutationDomainPending('personalization:rename'))
const canSubmit = computed(() => name.value.length >= 3 && name.value.length <= 16 && props.balance >= props.price)
async function rename(): Promise<void> {
  if (!canSubmit.value || pending.value) return
  message.value = null
  const result = await session.renameCharacter(name.value)
  if (!result) {
    message.value = personalizationError[session.errorCode ?? ''] ?? 'Не удалось изменить имя. Повторите попытку.'
    return
  }
  confirm.value = false
  name.value = ''
  message.value = 'Новое имя сохранено: ' + result.characterName
  emit('changed')
}
</script>

<template>
  <section class="rename-service" data-rename-service>
    <h2>Новое имя — та же история</h2>
    <p>Уровень, вещи, друзья и достижения останутся с героем.</p>
    <form @submit.prevent="canSubmit && !pending && (confirm = true)">
      <label for="rename-character">Новое имя</label>
      <input id="rename-character" v-model="name" data-rename-input maxlength="16" autocomplete="off" :disabled="pending" />
      <small>3–16 символов: буквы и цифры 0–9. Кириллица или латиница, без смешивания. Допустим пробел или дефис.</small>
      <UIButton type="submit" :disabled="!canSubmit || pending" data-rename-open>Изменить имя · {{ price }} кристаллов</UIButton>
      <p v-if="balance < price" class="rename-service__error">Не хватает кристаллов.</p>
    </form>
    <p v-if="message" role="status">{{ message }}</p>
    <UIModal :open="confirm" title="Подтвердить смену имени" :busy="pending" @close="confirm = false">
      <p>{{ session.snapshot?.character?.name }} → <strong>{{ name }}</strong></p>
      <p>Стоимость: {{ price }} кристаллов. После смены: {{ balance - price }}.</p>
      <p v-if="message" role="alert">{{ message }}</p>
      <template #actions><UIButton variant="ghost" :disabled="pending" @click="confirm = false">Отмена</UIButton>
        <UIButton :loading="pending" :disabled="!canSubmit || pending" data-rename-confirm @click="rename">Подтвердить · {{ price }}</UIButton></template>
    </UIModal>
  </section>
</template>

<style scoped>
.rename-service { display: grid; gap: 14px; padding: 18px; border: 1px solid #755c3d; border-radius: 8px; background: #151923; }
.rename-service h2 { margin: 0; color: #ead5a5; font: 700 25px Georgia, serif; }
.rename-service p { margin: 0; font-size: 14px; line-height: 1.5; color: #c8bdad; }
.rename-service form { display: grid; gap: 10px; }
.rename-service label { color: #e8d7b4; font-size: 14px; }
.rename-service input { min-width: 0; width: 100%; min-height: 48px; padding: 10px; border: 1px solid #796744; border-radius: 6px; color: #fff1d6; background: #090f18; font: inherit; }
.rename-service small { color: #a99c88; line-height: 1.5; }
.rename-service .rename-service__error { color: #e9a79a; }
</style>
