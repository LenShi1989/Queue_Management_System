<script setup lang="ts">
import { onMounted, ref } from 'vue'

const props = defineProps<{ url: string; size?: number }>()
const src = ref('')

onMounted(async () => {
  const { default: QRCode } = await import('qrcode')
  src.value = await QRCode.toDataURL(props.url, {
    width: props.size ?? 220,
    margin: 1,
    errorCorrectionLevel: 'M',
    color: { dark: '#0f172a', light: '#ffffff' },
  })
})
</script>

<template>
  <img v-if="src" :src="src" alt="QR Code" class="rounded-lg bg-white p-2" :width="size ?? 220" />
  <div v-else class="rounded-lg bg-slate-100 p-8" :style="{ width: `${size ?? 220}px`, height: `${size ?? 220}px` }" />
</template>
