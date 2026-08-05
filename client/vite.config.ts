import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import { resolve } from 'path'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  return {
  plugins: [react()],
  define: {
    // VITE_API_BASE_URL 環境変数をビルド時に埋め込む
    // 未設定時は空文字 (dev server プロキシ経由)
    __API_BASE__: JSON.stringify(env.VITE_API_BASE_URL ?? ''),
  },
  resolve: {
    alias: {
      '@': resolve(__dirname, 'src'),
    },
  },
  // public/ フォルダは既存の assets が入っているためそのまま使用
  publicDir: 'public',
  server: {
    port: 5173,
    host: true,
    allowedHosts: [
      'localhost',
      '127.0.0.1',
      'dev-majak4.studio35app.net',
    ],
    proxy: {
      // 開発時は .NET サーバーに転送
      '/hubs': {
        target: 'http://localhost:5246',
        ws: true,
        changeOrigin: true,
      },
      '/api': {
        target: 'http://localhost:5246',
        changeOrigin: true,
      },
      '/auth': {
        target: 'http://localhost:5246',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    rollupOptions: {
      output: {
        manualChunks: {
          react: ['react', 'react-dom', 'react-router-dom'],
          phaser: ['phaser'],
          signalr: ['@microsoft/signalr'],
        },
      },
    },
  },
  } // defineConfig return
})
