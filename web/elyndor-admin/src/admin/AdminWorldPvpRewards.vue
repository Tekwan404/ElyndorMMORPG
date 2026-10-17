<script setup lang="ts">
import { computed, shallowRef } from "vue";

const props = defineProps<{ profile: unknown }>();
const emit = defineEmits<{
  update: [value: { goldPerKill: number; goldPerAssist: number }];
}>();
const error = shallowRef("");
const settings = computed(() => {
  const source =
    props.profile && typeof props.profile === "object"
      ? (props.profile as Record<string, unknown>)
      : {};
  return {
    goldPerKill:
      typeof source.goldPerKill === "number" ? source.goldPerKill : 0,
    goldPerAssist:
      typeof source.goldPerAssist === "number" ? source.goldPerAssist : 0,
  };
});
function edit(key: "goldPerKill" | "goldPerAssist", event: Event) {
  const raw = (event.target as HTMLInputElement).value.trim().replace(",", ".");
  if (!/^\d+(?:\.\d{0,4})?$/.test(raw)) {
    error.value =
      "Значение не сохранено: введи от 0 до 100000 золота, не более четырёх знаков после запятой.";
    return;
  }
  const [whole, fraction = ""] = raw.split(".");
  const units = BigInt(whole!) * 10000n + BigInt(fraction.padEnd(4, "0"));
  if (units > 1000000000n) {
    error.value = "Значение не сохранено: максимум 100000 золота.";
    return;
  }
  error.value = "";
  emit("update", { ...settings.value, [key]: Number(units) });
}
</script>

<template>
  <fieldset id="admin-world-pvp-rewards" class="pvp-reward-settings">
    <legend>Награды World PvP</legend>
    <p>
      Обычное золото за зачётную победу и помощь. 0 отключает выплату. Награда
      за победу достаётся основному участнику по урону; последний удар сам по
      себе не даёт выплату.
    </p>
    <div>
      <label
        >Золото за убийство<input
          data-kill-gold
          type="text"
          inputmode="decimal"
          :value="settings.goldPerKill / 10000"
          @input="edit('goldPerKill', $event)"
      /></label>
      <label
        >Золото за помощь<input
          data-assist-gold
          type="text"
          inputmode="decimal"
          :value="settings.goldPerAssist / 10000"
          @input="edit('goldPerAssist', $event)"
      /></label>
    </div>
    <p>
      Суммы указаны в золотых монетах: 1 золото = 100 серебра = 10000 бронзы.
      Действуют лимиты наград и защита от повторного фарма. Изменения вступят в
      силу для новых боёв после публикации версии контента.
    </p>
    <p v-if="error" role="alert">{{ error }}</p>
  </fieldset>
</template>

<style scoped>
.pvp-reward-settings {
  max-width: 86rem;
  margin: 1rem auto;
  padding: 1rem;
  border: 1px solid #6a583c;
  color: #d6c7ab;
}
.pvp-reward-settings legend {
  color: #e6c483;
  font-weight: 700;
}
.pvp-reward-settings p {
  font-size: 0.85rem;
  line-height: 1.5;
}
.pvp-reward-settings > div {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
}
.pvp-reward-settings label {
  display: grid;
  gap: 0.4rem;
  flex: 1 1 12rem;
}
.pvp-reward-settings input {
  min-height: 44px;
  background: #14161d;
  border: 1px solid #67583f;
  color: #eee0c4;
  padding: 0.5rem;
}
[role="alert"] {
  color: #e9a096;
}
</style>
