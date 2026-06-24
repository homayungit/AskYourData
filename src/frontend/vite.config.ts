import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { resolve } from 'path'

export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: { '@': resolve(__dirname, 'src') }
  },
  server: {
    host: '0.0.0.0',
    port: 4501,
    proxy: {
      '/api': {
        target: 'http://localhost:5011',
        changeOrigin: true
      }
    }
  }
})
