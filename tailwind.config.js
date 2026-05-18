/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Views/**/*.cshtml',
    './wwwroot/**/*.html'
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#f4f6f8',
          100: '#e3e8ef',
          500: '#3b82f6',
          600: '#2563eb',
          900: '#1e3a8a',
        }
      }
    },
  },
  plugins: [],
}
