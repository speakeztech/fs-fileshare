/** @type {import('tailwindcss').Config} */
import daisyui from 'daisyui'

export default {
  content: [
    "./src/Pages/**/*.{fs,js,jsx,ts,tsx}",
    "./src/Pages/.fable-build/**/*.{js,ts,jsx,tsx}",
    "./public/index.html",
  ],
  theme: {
    extend: {
      fontFamily: {
        'sans': ['Inter', 'sans-serif'],
      },
      colors: {
        // Telix Brand Colors
        'telix-navy': '#003087',
        'telix-royal-blue': '#0066FF',
        'telix-cyan': '#00D4FF',
        'telix-accent-pink': '#FF0080',
        'telix-teal': '#2D4F4F',

        // Telix Neutrals
        'telix-gray-50': '#F8F9FA',
        'telix-gray-400': '#9E9E9E',
        'telix-gray-600': '#4A4A4A',
        'telix-gray-900': '#1A1A1A',
      },
      animation: {
        'fade-in': 'fadeIn 0.5s ease-in-out',
        'flow-wave': 'flowWave 8s ease-in-out infinite',
      },
      keyframes: {
        fadeIn: {
          '0%': { opacity: 0 },
          '100%': { opacity: 1 },
        },
        flowWave: {
          '0%, 100%': { transform: 'translateX(0) translateY(0)' },
          '50%': { transform: 'translateX(-10px) translateY(-10px)' },
        },
      },
    },
  },
  plugins: [
    daisyui,
  ],
  daisyui: {
    themes: [
      {
        light: {
          ...require("daisyui/src/theming/themes")["light"],
          primary: "#003087",       // Telix Navy
        },
      },
      {
        dark: {
          ...require("daisyui/src/theming/themes")["business"],
          primary: "#0066FF",       // Telix Royal Blue (lighter for dark bg)
        },
      },
    ]
  },
}
