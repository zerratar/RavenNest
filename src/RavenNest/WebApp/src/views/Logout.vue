<template>
    <div class="logout">
        <h1>Bye bye!</h1>
    </div>
</template>

<script lang="ts">
    import {
        Component,
        Vue,
    } from 'vue-property-decorator';

    import SiteState from '../site-state';
    import router from 'vue-router';
    import { SessionState } from '@/App.vue';

    @Component({})
    export default class Logout extends Vue {
        private mounted() {
            fetch('/api/auth/logout', {
                method: 'GET',
            }).then(async (result) => {
                const json = await result.json();
                SessionState.set(json);
                this.$router.push('/');
                (window as any)['AppClass'].$forceUpdate();
            }).catch(() => {
                window.location.href = '/';
            });
        }
    }
</script>