/// <reference types="vitest/config" />
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'path'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  // V2-028: in locale /api va al backend su questa macchina, come su Vercel va ad Azure
  // (middleware.ts). Per il browser è lo stesso sito delle pagine: il cookie di sessione funziona.
  server: {
    proxy: {
      '/api': 'http://localhost:5099',
    },
  },
  test: {
    // I test che montano componenti hanno bisogno di un DOM: jsdom lo simula in Node.
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
  },
})
