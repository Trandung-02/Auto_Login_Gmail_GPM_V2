import { MarketingLayout } from '#components/layout'
import { Metadata } from 'next'

import { SITE_MAIN_MESSAGE } from '#data/site-message'

export const metadata: Metadata = {
  title: SITE_MAIN_MESSAGE,
  description: SITE_MAIN_MESSAGE,
}

export default function Layout(props: { children: React.ReactNode }) {
  return <MarketingLayout>{props.children}</MarketingLayout>
}
