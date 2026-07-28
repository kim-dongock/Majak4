import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { resolve } from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: { '@': resolve(__dirname, 'src') },
  },
  server: {
    port: 5174,
    host: true,
    allowedHosts: [
      'localhost',
      '127.0.0.1',
      'dev-majak4.studio35app.net',
    ],
    proxy: {
      '/api/admin': {
        target: 'http://localhost:5246',
        changeOrigin: true,
      },
    },
  },
})
