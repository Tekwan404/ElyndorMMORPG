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
import ancientRuins from './world/ancient-ruins.png'
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
  },
  locations: {
    STARTER_TOWN: starterTown,
    WHISPERING_FOREST: whisperingForest,
    DEEP_FOREST: whisperingForest,
    BROODMOTHER_LAIR: ancientRuins,
    BLIGHTED_GROVE: caravanRoad,
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
