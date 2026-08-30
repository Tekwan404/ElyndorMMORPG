<script setup lang="ts">
import { computed, ref } from 'vue'

import type { CreateCharacterRequest } from '@/api/contracts'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIPanel, UIToast } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName, IconConfig } from '@/ui/icons/icon.types'

const session = useGameSessionStore()
const requestId = ref(crypto.randomUUID())
const name = ref('')
const raceId = ref<CreateCharacterRequest['raceId']>('HUMAN')
const genderId = ref<CreateCharacterRequest['genderId']>('MALE')
const classId = ref<CreateCharacterRequest['classId']>('WARRIOR')

const races: readonly { value: CreateCharacterRequest['raceId']; label: string }[] = [
  { value: 'HUMAN', label: 'Человек' },
  { value: 'UNDEAD', label: 'Нежить' },
]
const genders: readonly { value: CreateCharacterRequest['genderId']; label: string }[] = [
  { value: 'MALE', label: 'Мужской' },
  { value: 'FEMALE', label: 'Женский' },
]
const classes: readonly {
  value: CreateCharacterRequest['classId']
  label: string
  detail: string
  glyph: GlyphName
}[] = [
  { value: 'WARRIOR', label: 'Воин', detail: 'Rage · реакция', glyph: 'shield' },
  { value: 'ARCHER', label: 'Лучник', detail: 'Focus · темп', glyph: 'bow' },
  { value: 'MAGE', label: 'Маг', detail: 'Mana · порядок', glyph: 'staff' },
]

const valid = computed(
  () =>
    /^[\p{Script=Latin} -]+$|^[\p{Script=Cyrillic} -]+$/u.test(name.value) &&
    [...name.value].length >= 3 &&
    [...name.value].length <= 16 &&
    !/^[ -]|[ -]$|[ -]{2}/.test(name.value),
)

function classIcon(value: CreateCharacterRequest['classId'], glyph: GlyphName): IconConfig {
  return {
    id: `class-${value.toLowerCase()}`,
    glyph,
    category: 'utility',
    modifier: value === 'WARRIOR' ? 'fire' : value === 'ARCHER' ? 'poison' : 'ice',
    state: value === classId.value ? 'selected' : 'default',
  }
}

async function submit() {
  if (!valid.value || session.mutationPending) return
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
    <header>
      <p class="kicker">Новый путь</p>
      <h1>Создание героя</h1>
      <p class="intro">
        Выбери личность героя. Характеристики и игровой ресурс рассчитывает сервер.
      </p>
    </header>

    <form @submit.prevent="submit">
      <UIPanel>
        <template #title>Имя</template>
        <label class="name-field">
          <span>Имя героя</span>
          <input
            v-model="name"
            maxlength="16"
            autocomplete="off"
            placeholder="3–16 букв"
            aria-describedby="name-hint"
          />
        </label>
        <p id="name-hint" class="hint">
          Латиница или кириллица; один пробел или дефис внутри имени.
        </p>
      </UIPanel>

      <UIPanel>
        <template #title>Происхождение</template>
        <div class="identity-grid">
          <fieldset>
            <legend>Раса</legend>
            <label
              v-for="race in races"
              :key="race.value"
              class="choice"
              :class="{ 'choice--selected': raceId === race.value }"
            >
              <input v-model="raceId" name="raceId" type="radio" :value="race.value" />
              <span>{{ race.label }}</span>
            </label>
          </fieldset>
          <fieldset>
            <legend>Пол</legend>
            <label
              v-for="gender in genders"
              :key="gender.value"
              class="choice"
              :class="{ 'choice--selected': genderId === gender.value }"
            >
              <input v-model="genderId" name="genderId" type="radio" :value="gender.value" />
              <span>{{ gender.label }}</span>
            </label>
          </fieldset>
        </div>
      </UIPanel>

      <UIPanel>
        <template #title>Класс</template>
        <fieldset class="class-grid">
          <legend class="sr-only">Класс</legend>
          <label
            v-for="option in classes"
            :key="option.value"
            class="class-choice"
            :class="{ 'class-choice--selected': classId === option.value }"
          >
            <input v-model="classId" name="classId" type="radio" :value="option.value" />
            <IconGenerator
              class="class-choice__icon"
              :config="classIcon(option.value, option.glyph)"
            />
            <strong>{{ option.label }}</strong
            ><small>{{ option.detail }}</small>
          </label>
        </fieldset>
      </UIPanel>

      <div v-if="session.errorCode" role="alert">
        <UIToast tone="danger">{{ session.errorCode }}</UIToast>
      </div>
      <UIButton class="submit" :disabled="!valid" :loading="session.mutationPending" type="submit"
        >Войти в мир</UIButton
      >
    </form>
  </section>
</template>

<style scoped>
.creation {
  width: min(100%, var(--ui-content-width));
  margin-inline: auto;
  padding: var(--ui-space-6) calc(var(--ui-space-4) + var(--ui-safe-area-right)) var(--ui-space-7)
    calc(var(--ui-space-4) + var(--ui-safe-area-left));
}
.creation header {
  margin-bottom: var(--ui-space-5);
}
.kicker {
  margin: 0;
  color: var(--ui-color-primary);
  font-size: var(--ui-font-size-xs);
  font-weight: var(--ui-font-weight-bold);
  letter-spacing: var(--ui-space-1);
  text-transform: uppercase;
}
h1 {
  margin: var(--ui-space-1) 0;
  color: var(--ui-color-text-primary);
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-2xl);
}
.intro {
  margin: 0;
  color: var(--ui-color-text-muted);
  line-height: var(--ui-line-height-normal);
}
form {
  display: grid;
  gap: var(--ui-space-4);
}
.name-field {
  display: grid;
  gap: var(--ui-space-2);
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-sm);
}
input:not([type='radio']) {
  width: 100%;
  min-height: var(--ui-control-height-md);
  padding: var(--ui-space-2) var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-background);
  color: var(--ui-color-text-primary);
  font: inherit;
  font-size: var(--ui-font-size-input);
}
.hint {
  margin: var(--ui-space-2) 0 0;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}
.identity-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--ui-space-4);
}
fieldset {
  display: grid;
  gap: var(--ui-space-2);
  margin: 0;
  padding: 0;
  border: 0;
}
legend {
  margin-bottom: var(--ui-space-2);
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
  text-transform: uppercase;
}
.choice {
  display: flex;
  min-height: var(--ui-touch-target);
  align-items: center;
  gap: var(--ui-space-2);
  padding: var(--ui-space-2) var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-surface-2);
  color: var(--ui-color-text-secondary);
  cursor: pointer;
}
.choice--selected {
  border-color: var(--ui-color-primary);
  background: var(--ui-color-surface-3);
  color: var(--ui-color-text-primary);
}
.choice input,
.class-choice input {
  accent-color: var(--ui-color-primary);
}
.class-grid {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}
.class-choice {
  position: relative;
  display: grid;
  min-width: 0;
  min-height: calc(var(--ui-touch-target) * 2);
  place-items: center;
  align-content: center;
  gap: var(--ui-space-1);
  padding: var(--ui-space-2);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: var(--ui-color-surface-2);
  color: var(--ui-color-text-secondary);
  text-align: center;
  cursor: pointer;
}
.class-choice--selected {
  border-color: var(--ui-color-primary);
  background: var(--ui-color-surface-3);
  box-shadow: var(--ui-glow-selected);
  color: var(--ui-color-text-primary);
}
.class-choice input {
  position: absolute;
  z-index: 1;
  inset: 0;
  width: 100%;
  height: 100%;
  margin: 0;
  opacity: 0;
  cursor: pointer;
}
.class-choice__icon {
  width: var(--ui-icon-slot-sm);
  height: var(--ui-icon-slot-sm);
}
.class-choice small {
  overflow: hidden;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
  text-overflow: ellipsis;
  white-space: nowrap;
}
.submit {
  width: 100%;
}
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
}
@media (max-width: 360px) {
  .creation {
    padding-inline: calc(var(--ui-space-3) + var(--ui-safe-area-left))
      calc(var(--ui-space-3) + var(--ui-safe-area-right));
  }
  .class-grid {
    grid-template-columns: 1fr;
  }
  .class-choice {
    grid-template-columns: auto 1fr;
    min-height: var(--ui-control-height-lg);
    justify-items: start;
    text-align: left;
  }
  .class-choice__icon {
    grid-row: span 2;
  }
}
</style>
