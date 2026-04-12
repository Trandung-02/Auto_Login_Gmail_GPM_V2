import { ColorModeScript, theme } from '@chakra-ui/react'
import ReduxProvider from './store/provider'
import LocationBootstrap from './store/LocationBootstrap'
import { Provider } from './provider'
import { optimisticFont } from '@/app/fonts';
import "react-phone-input-2/lib/style.css";
import "@/public/styles/checkbox.scss"
import "@/public/styles/custom.css"
import "./globals.css"

export default function Layout(props: { children: React.ReactNode }) {
  const colorMode = theme.config.initialColorMode

  return (
    <html lang="en" data-theme={colorMode} style={{ colorScheme: colorMode }}>
      <head>
        <link
          rel="apple-touch-icon"
          sizes="76x76"
          href="/static/favicons/apple-touch-icon.png"
        />
        <link
          rel="icon"
          type="image/png"
          sizes="32x32"
          href="/static/favicons/favicon-32x32.png"
        />
        <link
          rel="icon"
          type="image/png"
          sizes="16x16"
          href="/static/favicons/favicon-16x16.png"
        />
        <link rel="manifest" href="/static/favicons/manifest.json" />
      </head>
      <body className={`chakra-ui-${colorMode} ${optimisticFont.variable}`}>
        <ColorModeScript initialColorMode={colorMode} />
        <Provider>
          <ReduxProvider>
            <LocationBootstrap />
            {props.children}
          </ReduxProvider>
        </Provider>
      </body>
    </html>
  )
}
