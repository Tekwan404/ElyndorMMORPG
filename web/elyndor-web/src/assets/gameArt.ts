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
  warriorAbilities: { strike, shieldBash, bastion, provoke, whirlwind, wildStrike },
} as const

const preloadUrls = [
  ...Object.values(gameArt.world),
  ...Object.values(gameArt.characters),
  ...Object.values(gameArt.navigation),
  ...Object.values(gameArt.warriorAbilities),
]

let preloadPromise: Promise<void> | undefined

export function preloadGameArt(): Promise<void> {
  preloadPromise ??= Promise.allSettled(
    preloadUrls.map(
      (url) =>
        new Promise<void>((resolve) => {
          const image = new Image()
          image.onload = () => resolve()
          image.onerror = () => resolve()
          image.src = url
        }),
    ),
  ).then(() => undefined)

  return preloadPromise
}
