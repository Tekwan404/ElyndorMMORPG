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
import ancientRuins from './world/ancient-ruins.webp'
import caravanAmbush from './world/caravan-ambush.webp'
import deepForest from './world/deep-forest.webp'
import forest from './world/forest.jpg'
import forestCombat from './world/forest-combat.webp'
import starterTown from './world/starter-town.webp'

export const gameArt = {
  world: {
    starterTown,
    capital: starterTown,
    forest,
    deepForest,
    ancientRuins,
    ruins: ancientRuins,
    caravanAmbush,
    forestCombat,
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
