<script setup lang="ts">
import { computed, ref } from 'vue'
import type { CreateCharacterRequest } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
const session = useGameSessionStore()
const requestId = ref(crypto.randomUUID())
const name = ref('')
const raceId = ref<CreateCharacterRequest['raceId']>('HUMAN')
const genderId = ref<CreateCharacterRequest['genderId']>('MALE')
const classId = ref<CreateCharacterRequest['classId']>('WARRIOR')
const valid = computed(
  () =>
    /^[\p{Script=Latin} -]+$|^[\p{Script=Cyrillic} -]+$/u.test(name.value) &&
    [...name.value].length >= 3 &&
    [...name.value].length <= 16 &&
    !/^[ -]|[ -]$|[ -]{2}/.test(name.value),
)
async function submit() {
  if (valid.value && !session.mutationPending)
    await session.createCharacter({
      requestId: requestId.value,
      name: name.value,
      raceId: raceId.value,
      genderId: genderId.value,
      classId: classId.value,
    })
}
</script>
<template>
  <section class="creation">
    <p class="kicker">Новый путь</p>
    <h1>Создание героя</h1>
    <form @submit.prevent="submit">
      <label
        >Имя<input v-model="name" maxlength="16" autocomplete="off" placeholder="3–16 букв"
      /></label>
      <p class="hint">Латиница или кириллица; один пробел или дефис внутри имени.</p>
      <fieldset>
        <legend>Раса</legend>
        <label v-for="value in ['HUMAN', 'UNDEAD']" :key="value"
          ><input v-model="raceId" name="raceId" type="radio" :value="value" />{{
            value === 'HUMAN' ? 'Человек' : 'Нежить'
          }}</label
        >
      </fieldset>
      <fieldset>
        <legend>Пол</legend>
        <label v-for="value in ['MALE', 'FEMALE']" :key="value"
          ><input v-model="genderId" name="genderId" type="radio" :value="value" />{{
            value === 'MALE' ? 'Мужской' : 'Женский'
          }}</label
        >
      </fieldset>
      <fieldset>
        <legend>Класс</legend>
        <label v-for="value in ['WARRIOR', 'ARCHER', 'MAGE']" :key="value"
          ><input v-model="classId" name="classId" type="radio" :value="value" />{{
            { WARRIOR: 'Воин', ARCHER: 'Лучник', MAGE: 'Маг' }[value]
          }}</label
        >
      </fieldset>
      <p v-if="session.errorCode" class="error" role="alert">{{ session.errorCode }}</p>
      <button class="primary" :disabled="!valid || session.mutationPending" type="submit">
        {{ session.mutationPending ? 'Создаём…' : 'Войти в мир' }}
      </button>
    </form>
  </section>
</template>
<style scoped lang="scss">
.creation {
  width: min(100%, 480px);
  margin: auto;
  padding: 26px 18px 38px;
}
.kicker {
  margin: 0;
  color: var(--color-gold);
  font-size: 0.7rem;
  letter-spacing: 0.16em;
  text-transform: uppercase;
}
h1 {
  margin: 5px 0 22px;
  font-family: Georgia, serif;
  color: #f0e7d2;
}
form {
  display: grid;
  gap: 16px;
}
label,
legend {
  color: var(--color-text-secondary);
  font-size: 0.82rem;
}
input:not([type='radio']) {
  width: 100%;
  min-height: 46px;
  margin-top: 7px;
  padding: 10px 12px;
  border: 1px solid var(--color-border);
  border-radius: 3px;
  background: #0b111e;
  color: #f0e7d2;
  font: inherit;
}
fieldset {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  margin: 0;
  padding: 12px;
  border: 1px solid var(--color-border);
}
fieldset label {
  display: flex;
  gap: 5px;
  align-items: center;
}
.hint {
  margin: -10px 0 0;
  color: var(--color-text-muted);
  font-size: 0.7rem;
}
.error {
  color: #ef8c93;
}
.primary {
  min-height: 48px;
  border: 1px solid #c8a963;
  border-radius: 4px;
  background: linear-gradient(#6c5224, #39280f);
  color: #fff4d1;
  font: inherit;
  font-weight: 700;
}
.primary:disabled {
  opacity: 0.45;
}
</style>
