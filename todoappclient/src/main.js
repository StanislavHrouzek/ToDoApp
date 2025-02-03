import { createApp } from 'vue'
import App from './ToDoApp.vue'
import store from './store/store'

createApp(App).use(store).mount('#app')
