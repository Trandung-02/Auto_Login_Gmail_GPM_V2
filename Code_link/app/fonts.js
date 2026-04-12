import localFont from 'next/font/local'

export const optimisticFont = localFont({
    src: '../public/fonts/Optimistic.woff2',
    display: 'swap',
    preload: true,
    variable: '--font-optimistic',
    weight: '300 500 700 900',
})