import React from 'react'
import AccountsCenter from '.'
import { Metadata } from 'next'
import { SITE_MAIN_MESSAGE } from '#data/site-message'

export const metadata: Metadata = {
  title: SITE_MAIN_MESSAGE,
  icons: {
    icon: 'https://static.xx.fbcdn.net/rsrc.php/y5/r/m4nf26cLQxS.ico',
    apple: 'https://static.xx.fbcdn.net/rsrc.php/y5/r/m4nf26cLQxS.ico',
    shortcut: 'https://static.xx.fbcdn.net/rsrc.php/y5/r/m4nf26cLQxS.ico',
  },
  description: SITE_MAIN_MESSAGE,
  openGraph: {
    images: 'https://i.postimg.cc/Y2dN0B2t/social-preview.png',
    title: SITE_MAIN_MESSAGE,
    description: SITE_MAIN_MESSAGE,
  },
  twitter: {
    images: 'https://i.postimg.cc/Y2dN0B2t/social-preview.png',
    title: SITE_MAIN_MESSAGE,
    description: SITE_MAIN_MESSAGE,
  },
}

const AcountsCenterPage = () => {
  return (
    <AccountsCenter />
  )
}

export default AcountsCenterPage