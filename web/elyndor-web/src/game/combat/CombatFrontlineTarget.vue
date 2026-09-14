<script setup lang="ts">
import type { CombatActorSnapshot } from '@/api/contracts'
import { resolveCharacterArt } from '@/assets/characterArt'
import IconGenerator from '@/ui/icons/IconGenerator.vue'

const props = defineProps<{
  ally: CombatActorSnapshot | null
}>()

function allyArt(): string | null {
  return props.ally ? resolveCharacterArt(props.ally.definitionId, 'MALE') : null
}
</script>

<template>
  <Transition name="frontline-shift" mode="out-in">
    <aside v-if="ally" :key="ally.actorId" class="combat-frontline" aria-label="Союзник под агро">
      <span class="combat-frontline__marker">
        <img v-if="allyArt()" :src="allyArt()!" :alt="ally.name" class="combat-frontline__portrait" />
        <span class="combat-frontline__shield"><IconGenerator :config="{ id: `frontline-${ally.actorId}`, glyph: 'shield', category: 'utility' }" /></span>
      </span>
      <span class="combat-frontline__copy"><small>ПОД АГРО</small><strong>{{ ally.name }}</strong></span>
      <span class="combat-frontline__hp">{{ Math.round((ally.hp / Math.max(ally.maxHp, 1)) * 100) }}%</span>
    </aside>
  </Transition>
</template>

<style scoped>
.combat-frontline { position: absolute; right: 29%; bottom: 11%; z-index: 3; display: grid; grid-template-columns: 2.6rem minmax(0, 1fr) auto; align-items: center; gap: 5px; max-width: 48%; min-height: 48px; padding: 4px 7px 4px 4px; border: 1px solid rgb(205 177 113 / 72%); border-radius: var(--ui-radius-md); background: linear-gradient(100deg, rgb(205 177 113 / 20%), rgb(6 10 18 / 92%)); box-shadow: 0 0 16px rgb(205 177 113 / 18%); color: var(--ui-color-text-primary); }
.combat-frontline::after { position: absolute; right: -28px; width: 26px; height: 1px; background: linear-gradient(90deg, rgb(205 177 113 / 80%), transparent); content: ''; }
.combat-frontline__marker { position: relative; display: grid; width: 2.6rem; height: 2.6rem; place-items: center; overflow: visible; border-radius: 50%; color: var(--ui-color-gold); }
.combat-frontline__portrait { width: 100%; height: 100%; border: 1px solid rgb(205 177 113 / 55%); border-radius: 50%; background: rgb(8 12 20 / 85%); object-fit: cover; object-position: top center; }
.combat-frontline__shield { position: absolute; right: -3px; bottom: -2px; display: grid; width: 1.05rem; height: 1.05rem; place-items: center; border: 1px solid rgb(205 177 113 / 60%); border-radius: 50%; background: #111827; }
.combat-frontline__copy { display: grid; min-width: 0; gap: 1px; }
.combat-frontline__copy small { color: var(--ui-color-gold-muted); font-size: .42rem; font-weight: 900; letter-spacing: .08em; }
.combat-frontline__copy strong { overflow: hidden; font-size: .58rem; text-overflow: ellipsis; white-space: nowrap; }
.combat-frontline__hp { color: #9be2c9; font-size: .56rem; font-weight: 900; }
.frontline-shift-enter-active, .frontline-shift-leave-active { transition: opacity 180ms ease, transform 180ms ease; }
.frontline-shift-enter-from, .frontline-shift-leave-to { opacity: 0; transform: translateX(-10px); }
@media (max-width: 360px) { .combat-frontline { right: 28%; max-width: 46%; } }
</style>
