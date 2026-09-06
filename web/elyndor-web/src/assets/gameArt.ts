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
import marcus from './npc/marcus.webp'
import ruins from './world/ancient-ruins-generated.png'
import combatForest from './world/combat-whispering-forest-generated.png'
import starterTown from './world/starter-town-generated.png'
import forest from './world/whispering-forest-generated.png'

export const gameArt = {
  world: { starterTown, capital: starterTown, forest, ruins, combatForest },
  characters: { warrior },
  npc: { marcus },
  navigation: {
    world: worldNavigation,
    hero: heroNavigation,
    location: locationNavigation,
    quests: questsNavigation,
    menu: menuNavigation,
  },
  warriorAbilities: { strike, shieldBash, bastion, provoke, whirlwind, wildStrike },
} as const
