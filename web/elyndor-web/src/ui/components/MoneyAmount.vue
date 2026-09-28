<script setup lang="ts">
import { computed } from 'vue'
import { moneyParts, type MoneyValue } from '@/shared/money'

const props = defineProps<{ amount: MoneyValue }>()
const parts = computed(() => moneyParts(props.amount))
const label = computed(() => `Золото: ${parts.value.gold}; серебро: ${parts.value.silver}; бронза: ${parts.value.bronze}`)
</script>

<template>
  <span class="money-amount" role="img" :aria-label="label" :title="`${label}. 100 бронзы = 1 серебро; 100 серебра = 1 золото.`">
    <span v-if="parts.gold > 0n" class="money-amount__gold" aria-hidden="true">{{ parts.gold }}<small>g</small></span>
    <span v-if="parts.silver > 0n" class="money-amount__silver" aria-hidden="true">{{ parts.silver }}<small>s</small></span>
    <span v-if="parts.bronze > 0n || (parts.gold === 0n && parts.silver === 0n)" class="money-amount__bronze" aria-hidden="true">{{ parts.bronze }}<small>b</small></span>
  </span>
</template>

<style scoped>
.money-amount { display: inline-flex; flex-wrap: wrap; align-items: baseline; gap: .1em .4em; min-width: 0; max-width: 100%; font-variant-numeric: tabular-nums; vertical-align: baseline; }
.money-amount > span { overflow-wrap: anywhere; }
.money-amount small { margin-left: .12em; font-size: .85em; font-weight: inherit; }
.money-amount__gold { color: #e8c866; }
.money-amount__silver { color: #ced8e5; }
.money-amount__bronze { color: #daa079; }
</style>
