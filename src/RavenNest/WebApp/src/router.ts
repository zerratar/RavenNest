import Vue from 'vue';
import Router from 'vue-router';
import Home from './views/Home.vue';

Vue.use(Router);

export default new Router({
  mode: 'history',
  base: process.env.BASE_URL,
  routes: [{
    path: '/',
    name: 'home',
    component: Home,
  }, {
    path: '/how-to-play',
    name: 'how-to-play',
    component: () => import('./views/HowToPlay.vue'),
  }, {
    path: '/docs',
    name: 'docs',
    component: () => import('./views/Documentation.vue'),
  }, {
    path: '/highscore',
    name: 'highscore',
    component: () => import('./views/HighScore.vue'),
  }, {
    path: '/character',
    name: 'character',
    component: () => import('./views/Character.vue'),
    children: [
      { path: '/character/skills', name: 'skills', component:() => import('./views/character/Skills.vue') },
      { path: '/character/inventory', name: 'inventory', component:() => import('./views/character/Inventory.vue') }
    ]
  }, {
    path: '/admin',
    name: 'admin',
    component: () => import('./views/Admin.vue'),
    children: [
      { path: '/admin/items', name: 'items', component:() => import('./views/admin/Items.vue') },
    ]
  }, {
    path: '/login',
    name: 'login',
    component: () => import('./views/Login.vue'),
  }, {
    path: '/logout',
    name: 'logout',
    component: () => import('./views/Logout.vue'),
  }, {
    path: '/password',
    name: 'password',
    component: () => import('./views/Password.vue'),
  }, {
    path: '/password-recovery',
    name: 'password-recovery',
    component: () => import('./views/PasswordRecovery.vue'),
  }, ],
});