import { ref, watch } from 'vue'
import { defineStore } from 'pinia'

export type MotionPreference = 'system' | 'reduced' | 'full'

const ATMOSPHERE_KEY = 'elyndor.ui.atmosphere'
const MOTION_KEY = 'elyndor.ui.motion'

function readAtmosphere(): boolean {
  if (typeof localStorage === 'undefined') return true
  try {
    return localStorage.getItem(ATMOSPHERE_KEY) !== 'off'
  } catch {
    return true
  }
}

function readMotion(): MotionPreference {
  if (typeof localStorage === 'undefined') return 'system'
  try {
    const value = localStorage.getItem(MOTION_KEY)
    return value === 'reduced' || value === 'full' ? value : 'system'
  } catch {
    return 'system'
  }
}

function persist(key: string, value: string): void {
  if (typeof localStorage === 'undefined') return
  try {
    localStorage.setItem(key, value)
  } catch {
    // Presentation preferences may remain session-only when storage is unavailable.
  }
}

export const useUiPreferencesStore = defineStore('uiPreferences', () => {
  const atmosphereEnabled = ref(readAtmosphere())
  const motionPreference = ref<MotionPreference>(readMotion())

  function applyToDocument(): void {
    if (typeof document === 'undefined') return
    document.documentElement.dataset.elyndorAtmosphere =
      atmosphereEnabled.value ? 'on' : 'off'
    document.documentElement.dataset.elyndorMotion = motionPreference.value
  }

  function setAtmosphereEnabled(value: boolean): void {
    atmosphereEnabled.value = value
  }

  function setMotionPreference(value: MotionPreference): void {
    motionPreference.value = value
  }

  watch(atmosphereEnabled, value => {
    persist(ATMOSPHERE_KEY, value ? 'on' : 'off')
    applyToDocument()
  }, { immediate: true })

  watch(motionPreference, value => {
    persist(MOTION_KEY, value)
    applyToDocument()
  }, { immediate: true })

  return {
    atmosphereEnabled,
    motionPreference,
    setAtmosphereEnabled,
    setMotionPreference,
    applyToDocument,
  }
})
