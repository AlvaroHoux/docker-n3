import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Porta do front-end em modo desenvolvimento (npm run dev)
export default defineConfig({
  plugins: [react()],
  server: { port: 3002, host: true },
  preview: { port: 3002, host: true }
})
