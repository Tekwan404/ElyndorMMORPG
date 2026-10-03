<script setup lang="ts">
import UIButton from './UIButton.vue'
import UIModal from './UIModal.vue'
import type { ConfirmationRequest } from '@/ui/composables/useConfirmation'
defineProps<{ request: ConfirmationRequest | null }>()
defineEmits<{ resolve: [accepted: boolean] }>()
</script>

<template>
  <UIModal :open="request !== null" :title="request?.title ?? ''" @close="$emit('resolve', false)">
    <p data-confirmation>{{ request?.message }}</p>
    <template #actions>
      <UIButton variant="ghost" data-confirm-cancel @click="$emit('resolve', false)"
        >Отмена</UIButton
      >
      <UIButton variant="danger" data-confirm-accept @click="$emit('resolve', true)">{{
        request?.confirmLabel ?? 'Подтвердить'
      }}</UIButton>
    </template>
  </UIModal>
</template>
