import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
import { installPartyInviteDeepLink } from '@/game/party/partyInviteDeepLink'
import './styles/tokens.css'
import './styles/base.scss'
import './styles/combat-icons.css'
import './styles/inventory-mobile-ux.css'
import './styles/combat-layout-v2.css'
import './styles/combat-layout-v2-polish.css'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
installPartyInviteDeepLink(pinia)
app.use(router)

app.mount('#app')