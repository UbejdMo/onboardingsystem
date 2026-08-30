import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    // Pinned so the origin always matches the API's CORS policy. Vite would
    // otherwise pick a different port if 5173 were busy, and the browser
    // would then block every API call.
    port: 5173,
    strictPort: true,
  },
})
