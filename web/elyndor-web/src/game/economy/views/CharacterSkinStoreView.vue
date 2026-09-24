<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import type { CharacterSkinOffer, CharacterSkinStoreSnapshot } from '@/api/contracts'
import { resolveSkinPreview } from '@/assets/characterArt'
import { classLabel } from '@/game/character/characterPresentation'
import { useGameSessionStore } from '@/stores/gameSession'

const session = useGameSessionStore()
const emit = defineEmits<{ balance: [value: number] }>()
const catalog = ref<CharacterSkinStoreSnapshot | null>(null)
const error = ref<string | null>(null)
const selectedId = ref<string | null>(null)
const activeSkinId = computed(() => catalog.value?.activeSkinId ?? null)
const skins = computed(() => {
  const character = session.snapshot?.character
  if (!character) return []
  return [...(catalog.value?.skins ?? [])]
    .filter(skin => skin.eligible && skin.classId === character.classId && skin.genderId === character.genderId)
    .sort((a, b) => a.crystalPrice - b.crystalPrice)
})

function skinState(skin: CharacterSkinOffer): string {
  if (!skin.eligible) return 'Недоступно'
  if (activeSkinId.value === skin.id || (!activeSkinId.value && !skin.purchasable)) return 'Используется'
  if (skin.owned) return 'Куплено'
  return 'Купить'
}

function restriction(skin: CharacterSkinOffer): string | null {
  const character = session.snapshot?.character
  if (!character) return 'Нужен персонаж'
  if (skin.genderId !== character.genderId) return 'Доступно только женским персонажам'
  if (skin.classId !== character.classId) return `Только для класса ${classLabel(skin.classId)}`
  return null
}

async function load(): Promise<void> {
  error.value = null
  try {
    catalog.value = await session.getCharacterSkins()
    emit('balance', catalog.value.crystalBalance)
  }
  catch { error.value = 'Не удалось загрузить облики.' }
}

async function act(skin: CharacterSkinOffer): Promise<void> {
  if (!skin.eligible || session.mutationPending || skinState(skin) === 'Используется') return
  selectedId.value = skin.id
  error.value = null
  const result = skin.owned
    ? await session.equipCharacterSkin(skin.id)
    : await session.buyCharacterSkin(skin.id)
  if (!result) error.value = 'Не удалось применить облик. Проверьте баланс и повторите попытку.'
  else if (catalog.value) {
    catalog.value = {
      ...catalog.value,
      crystalBalance: result.crystalBalance,
      activeSkinId: result.activeSkinId,
      skins: catalog.value.skins.map(candidate => candidate.id === skin.id ? { ...candidate, owned: true } : candidate),
    }
    emit('balance', result.crystalBalance)
  }
  selectedId.value = null
}

onMounted(load)
</script>

<template>
  <section class="skin-shop" aria-label="Облики персонажа">
    <header class="skin-shop__heading">
      <span>ПЕРСОНАЛЬНЫЙ АРСЕНАЛ</span>
      <h2>Облики</h2>
      <p>Новый образ не меняет характеристики. Купленный облик остаётся вашим и надевается без повторной оплаты.</p>
    </header>
    <p v-if="error" class="skin-shop__error" role="alert">{{ error }} <button type="button" @click="load">Повторить</button></p>
    <p v-if="!catalog" class="skin-shop__loading">Открываем гардероб…</p>
    <div v-else class="skin-grid">
      <article v-for="skin in skins" :key="skin.id" class="skin-card" :class="{ 'skin-card--locked': !skin.eligible, 'skin-card--active': skinState(skin) === 'Используется' }">
        <div class="skin-card__stage">
          <span class="skin-card__crest">ELYNDOR · {{ classLabel(skin.classId) }}</span>
          <img v-if="resolveSkinPreview(skin.imageId)" :src="resolveSkinPreview(skin.imageId)!" :alt="skin.name" loading="lazy" />
          <span v-else class="skin-card__missing" role="img" :aria-label="skin.name">✧</span>
          <span v-if="skinState(skin) === 'Используется'" class="skin-card__equipped">ИСПОЛЬЗУЕТСЯ</span>
        </div>
        <div class="skin-card__detail">
          <div><small>ЖЕНСКИЙ ОБЛИК · {{ classLabel(skin.classId) }}</small><h3>{{ skin.name }}</h3></div>
          <p v-if="restriction(skin)" class="skin-card__restriction">{{ restriction(skin) }}</p>
          <div class="skin-card__footer">
            <span class="skin-card__price">{{ skin.purchasable ? `✦ ${skin.crystalPrice}` : 'Базовый' }}</span>
            <button type="button" :disabled="!skin.eligible || session.mutationPending || skinState(skin) === 'Используется'" @click="act(skin)">
              {{ selectedId === skin.id ? 'Подождите…' : skin.owned && skinState(skin) === 'Куплено' ? 'Надеть' : skinState(skin) }}
            </button>
          </div>
        </div>
      </article>
    </div>
  </section>
</template>

<style scoped>
.skin-shop{display:grid;gap:18px;min-width:0;padding-bottom:max(18px,env(safe-area-inset-bottom))}.skin-shop__heading{padding:4px 2px 0}.skin-shop__heading span,.skin-card__detail small{color:#b39660;font-size:10px;font-weight:800;letter-spacing:.14em}.skin-shop__heading h2{margin:2px 0 5px;color:#f0dfb9;font:700 27px Georgia,serif}.skin-shop__heading p{max-width:600px;margin:0;color:#ada5a6;font-size:12px;line-height:1.5}.skin-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px}.skin-card{min-width:0;overflow:hidden;border:1px solid rgb(185 145 82 / 35%);border-radius:13px;background:linear-gradient(165deg,#202132,#10141c 60%,#19131b);box-shadow:0 8px 24px rgb(0 0 0 / 22%)}.skin-card--active{border-color:#d8b375;box-shadow:0 0 0 1px rgb(217 180 113 / 34%),0 9px 26px rgb(0 0 0 / 32%)}.skin-card--locked{opacity:.64}.skin-card__stage{position:relative;display:grid;place-items:end center;height:clamp(175px,47vw,310px);overflow:hidden;background:radial-gradient(ellipse at 50% 55%,rgb(138 91 102 / 37%),transparent 56%),radial-gradient(ellipse at 50% 98%,rgb(208 151 81 / 24%),transparent 50%),linear-gradient(#171927,#0c1018)}.skin-card__stage::after{position:absolute;inset:auto 0 0;height:38%;background:linear-gradient(transparent,rgb(9 10 16 / 76%));content:'';pointer-events:none}.skin-card__stage img{position:relative;width:100%;height:100%;object-fit:contain;object-position:bottom center;filter:drop-shadow(0 8px 14px rgb(0 0 0 / 40%))}.skin-card__crest,.skin-card__equipped{position:absolute;z-index:1;left:8px;padding:4px 6px;border:1px solid rgb(211 172 100 / 38%);border-radius:4px;background:rgb(15 14 22 / 78%);color:#e2c995;font-size:8px;font-weight:800;letter-spacing:.1em}.skin-card__crest{top:8px}.skin-card__equipped{bottom:8px}.skin-card__missing{align-self:center;color:#d2b174;font-size:54px}.skin-card__detail{display:grid;gap:9px;padding:10px}.skin-card__detail small{font-size:8px;letter-spacing:.08em}.skin-card__detail h3{min-height:2.2em;margin:4px 0 0;color:#eee1c7;font:700 15px/1.1 Georgia,serif}.skin-card__restriction{margin:0;color:#d9a497;font-size:10px}.skin-card__footer{display:flex;align-items:center;justify-content:space-between;gap:5px}.skin-card__price{color:#d9bb79;font-size:12px;font-weight:800;white-space:nowrap}.skin-card__footer button{min-height:42px;padding:5px 9px;border:1px solid #b5945d;border-radius:7px;background:linear-gradient(#8a6338,#5e3c2b);color:#fff1ce;font:700 11px/1.2 system-ui,sans-serif;cursor:pointer}.skin-card__footer button:disabled{border-color:#625c5d;background:#34323a;color:#aaa1a0;cursor:default}.skin-shop__error,.skin-shop__loading{margin:0;padding:12px;color:#e9b4a3;font-size:12px}.skin-shop__error button{margin-left:8px;border:0;background:none;color:#f2d398;text-decoration:underline}@media(max-width:350px){.skin-grid{grid-template-columns:1fr}.skin-card__stage{height:260px}}@media(min-width:700px){.skin-grid{grid-template-columns:repeat(3,minmax(0,1fr));gap:14px}.skin-card__stage{height:320px}.skin-card__detail{padding:14px}}
</style>
