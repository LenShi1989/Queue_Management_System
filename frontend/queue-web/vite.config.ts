import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    strictPort: false,
    proxy: {
      // 開發環境直接由 Vite 代理到 ASP.NET Core，避免 CORS 與環境設定負擔
      '/api': {
        target: 'http://localhost:5244',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5244',
        changeOrigin: true,
        ws: true,
      },
      '/health': {
        target: 'http://localhost:5244',
        changeOrigin: true,
      },
    },
  },
})
