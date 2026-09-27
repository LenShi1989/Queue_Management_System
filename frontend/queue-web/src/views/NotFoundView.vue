<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <main class="flex min-h-screen flex-col items-center justify-center gap-4 px-4 text-center">
    <p class="text-6xl font-bold text-slate-300">404</p>
    <h1 class="text-xl font-semibold text-slate-700">找不到這個頁面</h1>
    <p class="text-sm text-slate-500">請確認網址是否正確，或回到首頁繼續操作。</p>
    <div class="mt-2 flex gap-3">
      <RouterLink class="btn-primary" :to="{ name: 'dashboard' }">回到總覽</RouterLink>
      <RouterLink class="btn-secondary" :to="{ name: 'kiosk' }">前往取號</RouterLink>
    </div>
  </main>
</template>
