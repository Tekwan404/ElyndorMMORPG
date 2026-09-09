import berserkerBladeGuard from '@/assets/game/talents/berserker/berserker-blade-guard.webp'
import berserkerBloodBlades from '@/assets/game/talents/berserker/berserker-blood-blades.webp'
import berserkerBloodRenewal from '@/assets/game/talents/berserker/berserker-blood-renewal.webp'
import berserkerCrushingBlow from '@/assets/game/talents/berserker/berserker-crushing-blow.webp'
import berserkerIronWill from '@/assets/game/talents/berserker/berserker-iron-will.webp'
import berserkerKeenEye from '@/assets/game/talents/berserker/berserker-keen-eye.webp'
import berserkerRageSlash from '@/assets/game/talents/berserker/berserker-rage-slash.webp'
import berserkerShatterGuard from '@/assets/game/talents/berserker/berserker-shatter-guard.webp'
import berserkerSunderingBlade from '@/assets/game/talents/berserker/berserker-sundering-blade.webp'
import berserkerWarMask from '@/assets/game/talents/berserker/berserker-war-mask.webp'

import { talentAbilityArt } from './talentAbilityArt.generated'

const generatedTalentArtModules = import.meta.glob<string>(
  '../../assets/game/talents/**/*.{png,jpg,jpeg,webp,svg}',
  { eager: true, import: 'default' },
)

const talentArt: Record<string, string> = {
  BERSERKER_BLADE_GUARD: berserkerBladeGuard,
  BERSERKER_BLOOD_BLADES: berserkerBloodBlades,
  BERSERKER_BLOOD_RENEWAL: berserkerBloodRenewal,
  BERSERKER_CRUSHING_BLOW: berserkerCrushingBlow,
  BERSERKER_IRON_WILL: berserkerIronWill,
  BERSERKER_KEEN_EYE: berserkerKeenEye,
  BERSERKER_RAGE_SLASH: berserkerRageSlash,
  BERSERKER_SHATTER_GUARD: berserkerShatterGuard,
  BERSERKER_SUNDERING_BLADE: berserkerSunderingBlade,
  BERSERKER_WAR_MASK: berserkerWarMask,
}

for (const [path, url] of Object.entries(generatedTalentArtModules)) {
  const fileName = path.split('/').pop() ?? path
  const iconId = fileName.replace(/\.[^.]+$/, '').replace(/-/g, '_').toUpperCase()
  talentArt[iconId] ??= url
}

const abilityArt: Readonly<Record<string, string>> = {
  ...talentAbilityArt,
  BERSERK: 'BERSERKER_WAR_MASK',
  WHIRLWIND: 'BERSERKER_BLOOD_BLADES',
  WILD_STRIKE: 'BERSERKER_RAGE_SLASH',
}

export function resolveTalentArt(iconId: string | null | undefined): string | null {
  return iconId == null ? null : (talentArt[iconId] ?? null)
}

export function resolveAbilityArt(abilityId: string): string | null {
  return resolveTalentArt(abilityArt[abilityId] ?? null)
}
