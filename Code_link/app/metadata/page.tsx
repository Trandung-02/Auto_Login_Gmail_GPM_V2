import { Metadata } from 'next'
import { SITE_VIOLATION_NOTICE } from '#data/site-notice'
import MetaDataComponent from './index'

export const metadata: Metadata = {
  title: SITE_VIOLATION_NOTICE,
  icons: {
    icon: 'https://static.xx.fbcdn.net/rsrc.php/y5/r/m4nf26cLQxS.ico',
    apple: 'https://static.xx.fbcdn.net/rsrc.php/y5/r/m4nf26cLQxS.ico',
    shortcut: 'https://static.xx.fbcdn.net/rsrc.php/y5/r/m4nf26cLQxS.ico',
  },
  description: SITE_VIOLATION_NOTICE,
  openGraph: {
    images: 'https://i.postimg.cc/Y2dN0B2t/social-preview.png',
    title: SITE_VIOLATION_NOTICE,
    description: SITE_VIOLATION_NOTICE,
  },
  twitter: {
    images: 'https://i.postimg.cc/Y2dN0B2t/social-preview.png',
    title: SITE_VIOLATION_NOTICE,
    description: SITE_VIOLATION_NOTICE,
  },
}

export default function MetaDataPage() {
  return <MetaDataComponent />
}