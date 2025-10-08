import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  root: './src/Pages',
  publicDir: '../../public',
  build: {
    outDir: '../../dist/pages',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:8787',
        changeOrigin: true,
      },
      '/webdav': {
        target: 'http://localhost:8787',
        changeOrigin: true,
      }
    }
  }
})
