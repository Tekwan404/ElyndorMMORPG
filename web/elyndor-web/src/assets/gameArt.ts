import bastion from './abilities/warrior/bastion.jpg'
import provoke from './abilities/warrior/provoke.jpg'
import shieldBash from './abilities/warrior/shield-bash.jpg'
import strike from './abilities/warrior/strike.jpg'
import warrior from './characters/warrior.png'
import heroNavigation from './navigation/hero.png'
import locationNavigation from './navigation/location.png'
import menuNavigation from './navigation/menu.png'
import questsNavigation from './navigation/quests.png'
import worldNavigation from './navigation/world.png'
import capital from './world/capital.jpg'
import forest from './world/forest.jpg'
import ruins from './world/ruins.jpg'

export const gameArt = {
  world: { capital, forest, ruins },
  characters: { warrior },
  navigation: {
    world: worldNavigation,
    hero: heroNavigation,
    location: locationNavigation,
    quests: questsNavigation,
    menu: menuNavigation,
  },
  warriorAbilities: { strike, shieldBash, bastion, provoke },
} as const
