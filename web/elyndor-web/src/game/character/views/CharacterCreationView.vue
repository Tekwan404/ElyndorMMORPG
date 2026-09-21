<script setup lang="ts">
import { computed, ref } from 'vue'

import type { CreateCharacterRequest } from '@/api/contracts'
import { resolveCharacterArt } from '@/assets/characterArt'
import { useGameSessionStore } from '@/stores/gameSession'
import { UIButton, UIToast } from '@/ui/components'
import IconGenerator from '@/ui/icons/IconGenerator.vue'
import type { GlyphName, IconConfig, ModifierName } from '@/ui/icons/icon.types'

const session = useGameSessionStore()
const requestId = ref(crypto.randomUUID())
const name = ref('')
const nameTouched = ref(false)
const raceId = ref<CreateCharacterRequest['raceId']>('HUMAN')
const genderId = ref<CreateCharacterRequest['genderId']>('MALE')
const classId = ref<CreateCharacterRequest['classId']>('WARRIOR')

const races: readonly { value: CreateCharacterRequest['raceId']; label: string }[] = [
  { value: 'HUMAN', label: 'Человек' },
  { value: 'UNDEAD', label: 'Нежить' },
]
const genders: readonly { value: CreateCharacterRequest['genderId']; label: string }[] = [
  { value: 'MALE', label: 'Мужчина' },
  { value: 'FEMALE', label: 'Женщина' },
]
const classes: readonly {
  value: CreateCharacterRequest['classId']
  label: string
  role: string
  description: string
  traits: readonly string[]
  glyph: GlyphName
  modifier: ModifierName
}[] = [
  {
    value: 'WARRIOR',
    label: 'Воин',
    role: 'Ближний бой · Ярость',
    description: 'Закалённый боец, который держит удар и отвечает сокрушительной силой.',
    traits: ['Броня', 'Натиск', 'Контроль'],
    glyph: 'shield',
    modifier: 'fire',
  },
  {
    value: 'ARCHER',
    label: 'Лучник',
    role: 'Дальний бой · Концентрация',
    description: 'Меткий охотник, который держит дистанцию и выбирает момент для решающего выстрела.',
    traits: ['Дистанция', 'Точность', 'Мобильность'],
    glyph: 'bow',
    modifier: 'poison',
  },
  {
    value: 'MAGE',
    label: 'Маг',
    role: 'Заклинания · Мана',
    description: 'Повелитель стихий и тайной магии с сильным уроном и гибким арсеналом.',
    traits: ['Магия', 'Взрывной урон', 'Контроль'],
    glyph: 'staff',
    modifier: 'ice',
  },
  {
    value: 'PALADIN',
    label: 'Паладин',
    role: 'Свет · Мана',
    description: 'Святой воин в тяжёлой броне: сражается в первых рядах, защищает и исцеляет союзников.',
    traits: ['Защита', 'Исцеление', 'Свет'],
    glyph: 'holy',
    modifier: 'holy',
  },
]

const valid = computed(
  () =>
    /^[\p{Script=Latin} -]+$|^[\p{Script=Cyrillic} -]+$/u.test(name.value) &&
    [...name.value].length >= 3 &&
    [...name.value].length <= 16 &&
    !/^[ -]|[ -]$|[ -]{2}/.test(name.value),
)

const selectedClass = computed(() => classes.find((option) => option.value === classId.value) ?? classes[0]!)
const characterArt = computed(() => resolveCharacterArt(classId.value, genderId.value, 'transparent'))
const nameFeedback = computed(() => {
  if (!name.value) return '3–16 букв · кириллица или латиница'
  if (valid.value) return 'Имя подходит'
  if (!nameTouched.value && [...name.value].length < 3) return 'Минимум 3 буквы'
  return 'Проверьте длину, алфавит и пробелы в имени'
})

const errorMessage = computed(() => {
  if (!session.errorCode) return null
  return ({
    character_name_taken: 'Это имя уже занято.',
    character_already_exists: 'На аккаунте уже есть герой.',
    character_name_invalid: 'Проверьте имя героя.',
    network_unavailable: 'Нет связи с сервером. Попробуйте ещё раз.',
  } as Record<string, string>)[session.errorCode] ?? 'Не удалось создать героя. Попробуйте ещё раз.'
})

function classIcon(option: (typeof classes)[number]): IconConfig {
  return {
    id: `class-${option.value.toLowerCase()}`,
    glyph: option.glyph,
    category: 'utility',
    modifier: option.modifier,
    state: option.value === classId.value ? 'selected' : 'default',
  }
}

async function submit() {
  nameTouched.value = true
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
  <section class="creation" :data-class="classId">
    <div class="creation__frame">
      <header class="creation__header">
        <div>
          <p class="kicker">Elyndor · Новый путь</p>
          <h1>Создайте героя</h1>
        </div>
        <p class="intro">Выберите того, кем войдёте в этот мир.</p>
      </header>

      <form class="creation__layout" @submit.prevent="submit">
        <section class="hero-stage" aria-live="polite">
          <div class="hero-stage__aura" aria-hidden="true"></div>
          <div class="hero-stage__sigil" aria-hidden="true"></div>
          <img
            v-if="characterArt"
            :key="`${classId}-${genderId}`"
            class="hero-stage__character"
            :src="characterArt"
            :alt="`${selectedClass.label}, ${genderId === 'MALE' ? 'мужчина' : 'женщина'}`"
          />
          <div class="hero-stage__caption">
            <span>{{ selectedClass.role }}</span>
            <strong>{{ selectedClass.label }}</strong>
          </div>
        </section>

        <div class="creation__controls">
          <section class="control-section control-section--class">
            <div class="section-heading">
              <span>Класс</span>
              <small>Выберите стиль игры</small>
            </div>
            <fieldset class="class-grid">
              <legend class="sr-only">Класс</legend>
              <label
                v-for="option in classes"
                :key="option.value"
                class="class-choice"
                :class="{ 'class-choice--selected': classId === option.value }"
              >
                <input v-model="classId" name="classId" type="radio" :value="option.value" />
                <span class="class-choice__icon-wrap">
                  <IconGenerator class="class-choice__icon" :config="classIcon(option)" />
                </span>
                <strong>{{ option.label }}</strong>
              </label>
            </fieldset>

            <div class="class-summary">
              <div class="class-summary__title">
                <strong>{{ selectedClass.label }}</strong>
                <span>{{ selectedClass.role }}</span>
              </div>
              <p>{{ selectedClass.description }}</p>
              <div class="trait-row" aria-label="Особенности класса">
                <span v-for="trait in selectedClass.traits" :key="trait">{{ trait }}</span>
              </div>
            </div>
          </section>

          <section class="control-section identity-section">
            <fieldset>
              <legend>Раса</legend>
              <div class="segmented">
                <label
                  v-for="race in races"
                  :key="race.value"
                  class="segment"
                  :class="{ 'segment--selected': raceId === race.value }"
                >
                  <input v-model="raceId" name="raceId" type="radio" :value="race.value" />
                  <span>{{ race.label }}</span>
                </label>
              </div>
            </fieldset>
            <fieldset>
              <legend>Пол</legend>
              <div class="segmented">
                <label
                  v-for="gender in genders"
                  :key="gender.value"
                  class="segment"
                  :class="{ 'segment--selected': genderId === gender.value }"
                >
                  <input v-model="genderId" name="genderId" type="radio" :value="gender.value" />
                  <span>{{ gender.label }}</span>
                </label>
              </div>
            </fieldset>
          </section>

          <section class="control-section name-section">
            <label class="name-field">
              <span>Имя героя</span>
              <div class="name-input-wrap" :class="{ 'name-input-wrap--valid': valid && name.length > 0 }">
                <input
                  v-model="name"
                  maxlength="16"
                  autocomplete="off"
                  placeholder="Например, Арден"
                  aria-describedby="name-hint"
                  @blur="nameTouched = true"
                />
                <span v-if="valid && name.length > 0" class="name-check" aria-hidden="true">✓</span>
              </div>
            </label>
            <p
              id="name-hint"
              class="name-feedback"
              :class="{ 'name-feedback--valid': valid && name.length > 0 }"
              aria-live="polite"
            >
              {{ nameFeedback }}
            </p>
          </section>

          <div v-if="errorMessage" class="creation__error" role="alert">
            <UIToast tone="danger">{{ errorMessage }}</UIToast>
          </div>

          <div class="creation__action">
            <UIButton
              class="submit"
              :disabled="!valid || session.mutationPending"
              :loading="session.mutationPending"
              type="submit"
            >
              Войти в мир
            </UIButton>
            <span>Класс и внешность можно развивать уже в игре</span>
          </div>
        </div>
      </form>
    </div>
  </section>
</template>

<style scoped>
.creation {
  --creation-accent: var(--ui-color-primary);
  min-height: 100dvh;
  padding: calc(var(--ui-space-4) + var(--ui-safe-area-top))
    calc(var(--ui-space-4) + var(--ui-safe-area-right))
    calc(var(--ui-space-5) + var(--ui-safe-area-bottom))
    calc(var(--ui-space-4) + var(--ui-safe-area-left));
  background:
    radial-gradient(circle at 50% 20%, color-mix(in srgb, var(--creation-accent) 12%, transparent), transparent 36%),
    linear-gradient(180deg, color-mix(in srgb, var(--ui-color-surface-2) 78%, transparent), var(--ui-color-background));
}
.creation[data-class='PALADIN'] {
  --creation-accent: #d8b968;
}
.creation[data-class='MAGE'] {
  --creation-accent: #829ad7;
}
.creation[data-class='ARCHER'] {
  --creation-accent: #8da66b;
}
.creation[data-class='WARRIOR'] {
  --creation-accent: #b76c50;
}
.creation__frame {
  width: min(100%, 1080px);
  margin-inline: auto;
}
.creation__header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: var(--ui-space-4);
  margin-bottom: var(--ui-space-3);
}
.kicker {
  margin: 0;
  color: var(--creation-accent);
  font-size: var(--ui-font-size-xs);
  font-weight: var(--ui-font-weight-bold);
  letter-spacing: 0.16em;
  text-transform: uppercase;
}
h1 {
  margin: var(--ui-space-1) 0 0;
  color: var(--ui-color-text-primary);
  font-family: var(--ui-font-display);
  font-size: clamp(1.65rem, 3vw, 2.35rem);
  line-height: 1.05;
  text-wrap: balance;
}
.intro {
  max-width: 28rem;
  margin: 0 0 var(--ui-space-1);
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
  text-align: right;
}
.creation__layout {
  display: grid;
  grid-template-columns: minmax(320px, 1.08fr) minmax(360px, 0.92fr);
  gap: clamp(var(--ui-space-4), 4vw, var(--ui-space-7));
  align-items: stretch;
}
.hero-stage {
  position: relative;
  isolation: isolate;
  min-height: min(680px, calc(100dvh - 132px));
  overflow: hidden;
  border: 1px solid color-mix(in srgb, var(--creation-accent) 42%, var(--ui-color-border));
  border-radius: calc(var(--ui-radius-md) * 1.5);
  background:
    linear-gradient(180deg, transparent 48%, color-mix(in srgb, var(--ui-color-background) 88%, transparent) 100%),
    radial-gradient(circle at 50% 34%, color-mix(in srgb, var(--creation-accent) 14%, transparent), transparent 43%),
    var(--ui-color-surface-2);
  box-shadow:
    inset 0 0 0 1px color-mix(in srgb, var(--ui-color-text-primary) 4%, transparent),
    0 18px 42px rgb(0 0 0 / 24%);
}
.hero-stage::before,
.hero-stage::after {
  position: absolute;
  z-index: -1;
  content: '';
  pointer-events: none;
}
.hero-stage::before {
  inset: 0;
  background:
    linear-gradient(90deg, transparent, rgb(255 255 255 / 2%) 50%, transparent),
    repeating-linear-gradient(0deg, transparent 0 5px, rgb(255 255 255 / 0.7%) 6px);
  mask-image: linear-gradient(to bottom, black, transparent 86%);
}
.hero-stage::after {
  right: 8%;
  bottom: 10%;
  left: 8%;
  height: 14%;
  border-radius: 50%;
  background: rgb(0 0 0 / 46%);
  filter: blur(18px);
}
.hero-stage__aura {
  position: absolute;
  top: 11%;
  left: 50%;
  width: min(70%, 420px);
  aspect-ratio: 1;
  translate: -50% 0;
  border: 1px solid color-mix(in srgb, var(--creation-accent) 36%, transparent);
  border-radius: 50%;
  background: radial-gradient(circle, color-mix(in srgb, var(--creation-accent) 16%, transparent), transparent 66%);
  box-shadow: 0 0 80px color-mix(in srgb, var(--creation-accent) 11%, transparent);
  opacity: 0.85;
}
.hero-stage__sigil {
  position: absolute;
  top: 22%;
  left: 50%;
  width: min(55%, 330px);
  aspect-ratio: 1;
  translate: -50% 0;
  rotate: 45deg;
  border: 1px solid color-mix(in srgb, var(--creation-accent) 22%, transparent);
  opacity: 0.55;
}
.hero-stage__sigil::after {
  position: absolute;
  inset: 14%;
  border: 1px solid color-mix(in srgb, var(--creation-accent) 18%, transparent);
  content: '';
}
.hero-stage__character {
  position: absolute;
  z-index: 2;
  inset: 1.5% 0 0;
  width: 100%;
  height: 92%;
  object-fit: contain;
  object-position: center bottom;
  filter: drop-shadow(0 18px 24px rgb(0 0 0 / 40%));
  animation: character-enter 220ms ease-out both;
}
.hero-stage__caption {
  position: absolute;
  z-index: 3;
  right: var(--ui-space-4);
  bottom: var(--ui-space-4);
  left: var(--ui-space-4);
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: var(--ui-space-3);
  padding-top: var(--ui-space-4);
  border-top: 1px solid color-mix(in srgb, var(--creation-accent) 35%, transparent);
}
.hero-stage__caption strong {
  color: var(--ui-color-text-primary);
  font-family: var(--ui-font-display);
  font-size: clamp(1.4rem, 3vw, 2rem);
}
.hero-stage__caption span {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
  letter-spacing: 0.06em;
  text-transform: uppercase;
}
.creation__controls {
  display: grid;
  align-content: start;
  gap: var(--ui-space-3);
  padding-block: var(--ui-space-1);
}
.control-section {
  padding: var(--ui-space-3);
  border: 1px solid color-mix(in srgb, var(--ui-color-border) 84%, transparent);
  border-radius: var(--ui-radius-md);
  background: color-mix(in srgb, var(--ui-color-surface-2) 88%, transparent);
  box-shadow: inset 0 1px rgb(255 255 255 / 2%);
}
.section-heading {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--ui-space-3);
  margin-bottom: var(--ui-space-2);
}
.section-heading > span,
.name-field > span,
legend {
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-xs);
  font-weight: var(--ui-font-weight-bold);
  letter-spacing: 0.08em;
  text-transform: uppercase;
}
.section-heading small {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}
fieldset {
  min-width: 0;
  margin: 0;
  padding: 0;
  border: 0;
}
.class-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--ui-space-2);
}
.class-choice {
  position: relative;
  display: grid;
  min-width: 0;
  min-height: 78px;
  place-items: center;
  align-content: center;
  gap: 5px;
  padding: var(--ui-space-2) var(--ui-space-1);
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  background: color-mix(in srgb, var(--ui-color-background) 52%, var(--ui-color-surface-2));
  color: var(--ui-color-text-muted);
  cursor: pointer;
  transition: border-color 140ms ease, background 140ms ease, transform 140ms ease;
}
.class-choice:hover {
  border-color: color-mix(in srgb, var(--creation-accent) 54%, var(--ui-color-border));
}
.class-choice--selected {
  border-color: var(--creation-accent);
  background: color-mix(in srgb, var(--creation-accent) 9%, var(--ui-color-surface-3));
  box-shadow: inset 0 0 18px color-mix(in srgb, var(--creation-accent) 8%, transparent);
  color: var(--ui-color-text-primary);
  transform: translateY(-1px);
}
.class-choice input,
.segment input {
  position: absolute;
  z-index: 1;
  inset: 0;
  width: 100%;
  height: 100%;
  margin: 0;
  opacity: 0;
  cursor: pointer;
}
.class-choice:focus-within,
.segment:focus-within {
  outline: 2px solid var(--creation-accent);
  outline-offset: 2px;
}
.class-choice__icon-wrap {
  display: grid;
  width: 36px;
  height: 36px;
  place-items: center;
}
.class-choice__icon {
  width: var(--ui-icon-slot-sm);
  height: var(--ui-icon-slot-sm);
}
.class-choice strong {
  max-width: 100%;
  overflow: hidden;
  font-size: var(--ui-font-size-xs);
  text-overflow: ellipsis;
  white-space: nowrap;
}
.class-summary {
  display: grid;
  gap: var(--ui-space-2);
  margin-top: var(--ui-space-3);
  padding-top: var(--ui-space-3);
  border-top: 1px solid color-mix(in srgb, var(--creation-accent) 20%, var(--ui-color-border));
}
.class-summary__title {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--ui-space-3);
}
.class-summary__title strong {
  color: var(--creation-accent);
  font-family: var(--ui-font-display);
  font-size: var(--ui-font-size-lg);
}
.class-summary__title span {
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}
.class-summary p {
  margin: 0;
  color: var(--ui-color-text-secondary);
  font-size: var(--ui-font-size-sm);
  line-height: var(--ui-line-height-normal);
}
.trait-row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ui-space-1);
}
.trait-row span {
  padding: 3px var(--ui-space-2);
  border: 1px solid color-mix(in srgb, var(--creation-accent) 24%, var(--ui-color-border));
  border-radius: 999px;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}
.identity-section {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--ui-space-3);
}
legend {
  margin-bottom: var(--ui-space-2);
}
.segmented {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  overflow: hidden;
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
}
.segment {
  position: relative;
  display: grid;
  min-height: var(--ui-touch-target);
  place-items: center;
  padding: var(--ui-space-2);
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-sm);
  cursor: pointer;
}
.segment + .segment {
  border-left: 1px solid var(--ui-color-border);
}
.segment--selected {
  background: color-mix(in srgb, var(--creation-accent) 11%, var(--ui-color-surface-3));
  color: var(--ui-color-text-primary);
  box-shadow: inset 0 -2px var(--creation-accent);
}
.name-field {
  display: grid;
  gap: var(--ui-space-2);
}
.name-input-wrap {
  position: relative;
}
.name-input-wrap input {
  width: 100%;
  min-height: var(--ui-control-height-lg);
  padding: var(--ui-space-2) calc(var(--ui-space-5) + var(--ui-space-2)) var(--ui-space-2) var(--ui-space-3);
  border: 1px solid var(--ui-color-border);
  border-radius: var(--ui-radius-md);
  outline: 0;
  background: color-mix(in srgb, var(--ui-color-background) 70%, var(--ui-color-surface-2));
  color: var(--ui-color-text-primary);
  font: inherit;
  font-size: var(--ui-font-size-input);
  transition: border-color 140ms ease, box-shadow 140ms ease;
}
.name-input-wrap input:focus {
  border-color: var(--creation-accent);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--creation-accent) 14%, transparent);
}
.name-input-wrap--valid input {
  border-color: color-mix(in srgb, var(--creation-accent) 60%, var(--ui-color-border));
}
.name-check {
  position: absolute;
  top: 50%;
  right: var(--ui-space-3);
  color: var(--creation-accent);
  font-weight: var(--ui-font-weight-bold);
  translate: 0 -50%;
}
.name-feedback {
  min-height: 1.15em;
  margin: var(--ui-space-1) 0 0;
  color: var(--ui-color-text-muted);
  font-size: var(--ui-font-size-xs);
}
.name-feedback--valid {
  color: var(--creation-accent);
}
.creation__error {
  min-width: 0;
}
.creation__action {
  position: sticky;
  z-index: 5;
  bottom: 0;
  display: grid;
  gap: var(--ui-space-1);
  padding-top: var(--ui-space-2);
  background: linear-gradient(180deg, transparent, var(--ui-color-background) 34%);
}
.submit {
  width: 100%;
  min-height: calc(var(--ui-control-height-lg) + 4px);
}
.creation__action > span {
  color: var(--ui-color-text-muted);
  font-size: 0.7rem;
  text-align: center;
}
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
}
@keyframes character-enter {
  from {
    opacity: 0.45;
    transform: translateY(5px) scale(0.99);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}
@media (max-width: 760px) {
  .creation {
    min-height: 100dvh;
    padding-top: calc(var(--ui-space-3) + var(--ui-safe-area-top));
    padding-inline: calc(var(--ui-space-3) + var(--ui-safe-area-left))
      calc(var(--ui-space-3) + var(--ui-safe-area-right));
  }
  .creation__header {
    align-items: start;
    margin-bottom: var(--ui-space-2);
  }
  .intro {
    display: none;
  }
  h1 {
    font-size: 1.55rem;
  }
  .creation__layout {
    display: grid;
    grid-template-columns: 1fr;
    gap: var(--ui-space-3);
  }
  .hero-stage {
    min-height: clamp(250px, 43dvh, 390px);
    border-radius: var(--ui-radius-md);
  }
  .hero-stage__character {
    inset: 0;
    height: 94%;
  }
  .hero-stage__caption {
    right: var(--ui-space-3);
    bottom: var(--ui-space-3);
    left: var(--ui-space-3);
    padding-top: var(--ui-space-2);
  }
  .hero-stage__caption strong {
    font-size: 1.35rem;
  }
  .hero-stage__caption span {
    max-width: 60%;
    font-size: 0.62rem;
  }
  .creation__controls {
    gap: var(--ui-space-2);
    padding: 0;
  }
  .control-section {
    padding: var(--ui-space-2);
  }
  .class-choice {
    min-height: 68px;
    padding: var(--ui-space-1);
  }
  .class-choice__icon-wrap {
    width: 30px;
    height: 30px;
  }
  .class-summary {
    margin-top: var(--ui-space-2);
    padding-top: var(--ui-space-2);
  }
  .class-summary__title strong {
    font-size: var(--ui-font-size-base);
  }
  .class-summary p {
    display: -webkit-box;
    overflow: hidden;
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
  }
  .creation__action {
    bottom: calc(-1 * var(--ui-space-1));
    margin-inline: calc(-1 * var(--ui-space-3));
    padding: var(--ui-space-3) var(--ui-space-3) calc(var(--ui-space-1) + var(--ui-safe-area-bottom));
  }
}
@media (max-width: 380px) {
  .creation {
    padding-inline: calc(var(--ui-space-2) + var(--ui-safe-area-left))
      calc(var(--ui-space-2) + var(--ui-safe-area-right));
  }
  .hero-stage {
    min-height: 238px;
  }
  .class-grid {
    gap: var(--ui-space-1);
  }
  .class-choice {
    min-height: 62px;
  }
  .class-choice__icon-wrap {
    width: 26px;
    height: 26px;
  }
  .class-choice strong {
    font-size: 0.65rem;
  }
  .identity-section {
    grid-template-columns: 1fr;
    gap: var(--ui-space-2);
  }
  .creation__action {
    margin-inline: calc(-1 * var(--ui-space-2));
    padding-inline: var(--ui-space-2);
  }
}
@media (prefers-reduced-motion: reduce) {
  .hero-stage__character,
  .class-choice,
  .name-input-wrap input {
    animation: none;
    transition: none;
  }
}
</style>
