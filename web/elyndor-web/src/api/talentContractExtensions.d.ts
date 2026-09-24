import '@/api/contracts'

declare module '@/api/contracts' {
  interface TalentNode {
    unlockedAbilityName: string | null
  }
}
