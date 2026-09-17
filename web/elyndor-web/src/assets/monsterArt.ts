const monsterArtModules = import.meta.glob<string>(
  './monsters/*.{png,jpg,jpeg,webp,svg}',
  { eager: true, import: 'default' },
)

const monsterArtById = new Map<string, string>(
  Object.entries(monsterArtModules).map(([path, url]) => {
    const fileName = path.split('/').pop() ?? path
    const artId = fileName.replace(/\.[^.]+$/, '')
    return [artId, url]
  }),
)

const LOCATION_ART_FALLBACKS: ReadonlyArray<readonly [RegExp, string]> = [
  [/^WHISPERING_FOREST_/, 'lesnoi-volk'],
  [/^FLOWER_MEADOW_/, 'zarazhionnyi-olen'],
  [/^DEEP_FOREST_/, 'lesnaia-vedma'],
  [/^OLD_ROAD_/, 'dorozhnyi-marodior'],
  [/^STONE_SPURS_/, 'molodoi-kamneglot'],
  [/^ANCIENT_MINE_/, 'shakhtnyi-dozornyi'],
  [/^BLIGHTED_GROVE_/, 'porchenyi-dreven'],
  [/^(ASHEN_BORDER|ECLIPSE_OUTSKIRTS)_/, 'soldat-zatmeniia'],
  [/^(MOON_ASH|MOON_ASH_MARSHES)_/, 'lunnopepelnyi-utoplennik'],
  [/^(SHATTERED_LANDS|SHATTERED_ORDER)_/, 'oskolochnyi-strazh'],
  [/^BLACKSTONE_HIGHLANDS_/, 'kamennyi-strazh'],
  [/^CRIMSON_WASTELAND_/, 'prizrachnyi-palomnik'],
  [/^(OBSIDIAN_EDGE|BLACK_BASTION)_/, 'chasovoi-vnutrennikh-vrat'],
  [/^HEART_OF_BLIGHTED_GROVE_/, 'iadovitaia-matrona-chashchi'],
  [/^ECLIPSED_CITADEL_/, 'soldat-zatmeniia'],
]

function semanticFallbackArtId(artId: string, monsterId?: string | null): string | null {
  const source = `${artId} ${monsterId ?? ''}`.toLowerCase()

  if (/spider|pauk|arach/.test(source)) {
    if (/brood|matka|matron/.test(source)) return 'spider-broodmother'
    if (/mine|shakht|glubin/.test(source)) return 'shakhtnyi-pauk'
    if (/poison|iadovit|venom/.test(source)) return 'iadovityi-pauk'
    if (/corrupt|porchen|porchenn/.test(source)) return 'iadovityi-peshchernyi-pauk'
    return 'giant-spider'
  }
  if (/roevik|swarm|bee|insect/.test(source)) return 'iadovitaia-matrona-chashchi'
  if (/boar|kaban|hog/.test(source)) {
    if (/corrupt|oskvern|porchen|porchenn/.test(source)) return 'corrupted-boar'
    if (/vozhak|alpha|wild/.test(source)) return 'dikii-kaban-vozhak'
    return 'forest-boar'
  }
  if (/wolf|volk|hound|gonch/.test(source)) {
    if (/ash|pepel/.test(source)) return 'pepelnyi-gonchii-zver'
    if (/corrupt|porchen|porchenn/.test(source)) return 'porchenyi-volk'
    if (/alpha|vozhak/.test(source)) return 'alpha-wolf'
    return 'wolf'
  }
  if (/deer|olen/.test(source)) return 'zarazhionnyi-olen'
  if (/goblin/.test(source)) return /trap|kapkan/.test(source) ? 'goblin-kapkanshchik' : 'goblin-scout'
  if (/bat|netopyr/.test(source)) return 'prizrachnyi-palomnik'
  if (/bandit|rogue|robber|maraud|razboin|marod|grabitel|naiom|podryv|luchnik|archer/.test(source)) {
    if (/mage|koldun|necrom|zaklin|piromant/.test(source)) return 'charodei-raskola'
    if (/archer|luchnik|strelok/.test(source)) return 'sumerechnyi-luchnik'
    return 'bandit-rogue'
  }
  if (/skelet|kostian|mertv|undead|necrom|banshi|ghost|palomnik|akolit|sluga|marshal/.test(source)) {
    if (/archer|luchnik|strelok/.test(source)) return 'sumerechnyi-luchnik'
    return 'prizrachnyi-palomnik'
  }
  if (/mage|magistr|koldun|zhrets|cultist|kultist|propoved|zaklin|piromant|velarius|azrael/.test(source)) {
    return /moon|bolot|swamp|topei|crimson|kostian/.test(source)
      ? 'zhrets-ugasshego-sveta'
      : 'charodei-raskola'
  }
  if (/archer|luchnik|strelok|sniper/.test(source)) return 'sumerechnyi-luchnik'
  if (/golem|kamenn|stone|skorpion|zhuk|bronespin|runokamenn|koloss/.test(source)) {
    return /mine|shakht|rudnyi/.test(source) ? 'kamennyi-strazh' : 'runnyi-strazh'
  }
  if (/strazh|guard|knight|rytsar|palach|soldat|rekrut|chasovoi|inkvizitor|marshal/.test(source)) {
    if (/bastion|obsidian|black_bastion/.test(source)) return 'alebardshchik-bastiona'
    if (/shattered|raskol|zerkal/.test(source)) return 'oskolochnyi-strazh'
    if (/mine|shakht|rudnyi/.test(source)) return 'shakhtnyi-dozornyi'
    return 'soldat-zatmeniia'
  }
  if (/dreven|tree|root|loz|vine|kornevoi/.test(source)) return 'porchenyi-dreven'
  if (/spirit|dukh|bes|ghost|shadow|tenevoi|pustot|otrazhen/.test(source)) return 'prizrachnyi-palomnik'

  const locationFallback = LOCATION_ART_FALLBACKS.find(([pattern]) => pattern.test(monsterId ?? ''))
  return locationFallback?.[1] ?? null
}

export function monsterArtUrl(
  artId: string | null | undefined,
  monsterId?: string | null,
): string | undefined {
  if (artId) {
    const directArt = monsterArtById.get(artId)
    if (directArt) return directArt
  }

  const fallbackArtId = artId ? semanticFallbackArtId(artId, monsterId) : null
  if (fallbackArtId) return monsterArtById.get(fallbackArtId)
  return undefined
}
