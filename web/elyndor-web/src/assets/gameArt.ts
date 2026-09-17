import bastion from './abilities/warrior/bastion.jpg'
import provoke from './abilities/warrior/provoke.jpg'
import shieldBash from './abilities/warrior/shield-bash.jpg'
import strike from './abilities/warrior/strike.jpg'
import whirlwind from './abilities/warrior/whirlwind.jpg'
import wildStrike from './abilities/warrior/wild-strike.jpg'
import warrior from './characters/warrior.png'
import heroNavigation from './navigation/hero.png'
import locationNavigation from './navigation/location.png'
import menuNavigation from './navigation/menu.png'
import questsNavigation from './navigation/quests.png'
import worldNavigation from './navigation/world.png'
import starterTown from './world/starter-town.png'
import whisperingForest from './world/whispering-forest.png'
import flowerMeadow from './world/tsvetochnaia-poliana.webp'
import deepForest from './world/glubokii-les.webp'
import oldRoad from './world/staryi-trakt.webp'
import stoneSpurs from './world/kamennye-otrogi.webp'
import blightedGrove from './world/oskvernionnaia-chashcha.webp'
import ashenBorder from './world/pepelnaia-granitsa.webp'
import moonAshMarshes from './world/topi-lunnogo-pepla.webp'
import eclipseOutskirts from './world/predmestia-zatmeniia.webp'
import shatteredLands from './world/zemli-raskola.webp'
import blackstoneHighlands from './world/chernokamennoe-nagore.webp'
import crimsonWasteland from './world/bagrovaia-pustosh.webp'
import obsidianEdge from './world/obsidianovyi-predel.webp'
import blightedGroveRaid from './world/serdtse-oskvernionnoi-chashchi.webp'
import shatteredOrderRaid from './world/tsitadel-raskolotogo-ordena-raid.webp'
import blackBastionRaid from './world/chiornyi-bastion.webp'
import ancientRuins from './world/ancient-ruins.png'
import worldAtlas from './world/world-atlas.svg'
import caravanRoad from './world/caravan-road.png'
import combatWhispering from './world/combat-whispering.png'
import marcus from './npc/marcus.webp'
import combatTrainer from './npc/combat-trainer.webp'
import blacksmith from './npc/blacksmith.webp'
import alchemist from './npc/alchemist.webp'
import innkeeper from './npc/innkeeper.webp'
import elder from './npc/starter-town-elder.webp'
import registrar from './npc/registrar.webp'
import quartermaster from './npc/quartermaster.webp'
import huntMaster from './npc/hunt-master.webp'
import cartographer from './npc/cartographer.webp'

export const gameArt = {
  world: {
    starterTown,
    capital: starterTown,
    forest: whisperingForest,
    ruins: ancientRuins,
    whisperingForest,
    ancientRuins,
    caravanRoad,
    combatWhispering,
    worldAtlas,
  },
  locations: {
    STARTER_TOWN: starterTown,
    WHISPERING_FOREST: whisperingForest,
    DEEP_FOREST: deepForest,
    FLOWER_MEADOW: flowerMeadow,
    OLD_ROAD: oldRoad,
    STONE_SPURS: stoneSpurs,
    BLIGHTED_GROVE: blightedGrove,
    ASHEN_BORDER: ashenBorder,
    MOON_ASH_MARSHES: moonAshMarshes,
    ECLIPSE_OUTSKIRTS: eclipseOutskirts,
    SHATTERED_LANDS: shatteredLands,
    BLACKSTONE_HIGHLANDS: blackstoneHighlands,
    CRIMSON_WASTELAND: crimsonWasteland,
    OBSIDIAN_EDGE: obsidianEdge,
    ANCIENT_MINE: ancientRuins,
    ECLIPSED_CITADEL: ancientRuins,
    SHATTERED_ORDER_CITADEL_TEST: ancientRuins,
    HEART_OF_BLIGHTED_GROVE_RAID: blightedGroveRaid,
    SHATTERED_ORDER_CITADEL_RAID: shatteredOrderRaid,
    BLACK_BASTION_RAID: blackBastionRaid,
    BROODMOTHER_LAIR: ancientRuins,
  },
  npc: {
    marcus,
    combatTrainer,
    blacksmith,
    alchemist,
    innkeeper,
    elder,
    registrar,
    quartermaster,
    huntMaster,
    cartographer,
  },
  characters: { warrior },
  navigation: {
    world: worldNavigation,
    hero: heroNavigation,
    location: locationNavigation,
    quests: questsNavigation,
    menu: menuNavigation,
  },
  warriorAbilities: { strike, shieldBash, bastion, provoke, whirlwind, wildStrike },
} as const
