'use client'

import { Box, SkipNavContent, SkipNavLink } from '@chakra-ui/react'
import { SITE_MAIN_MESSAGE } from '#data/site-message'

import { ReactNode } from 'react'

import {
  AnnouncementBanner,
  AnnouncementBannerProps,
} from '../announcement-banner'
import { Footer, FooterProps } from './footer'
import { Header, HeaderProps } from './header'

interface LayoutProps {
  children: ReactNode
  announcementProps?: AnnouncementBannerProps
  headerProps?: HeaderProps
  footerProps?: FooterProps
}

export const MarketingLayout: React.FC<LayoutProps> = (props) => {
  const { children, announcementProps, headerProps, footerProps } = props
  return (
    <Box>
      <SkipNavLink>{SITE_MAIN_MESSAGE}</SkipNavLink>
      {announcementProps ? <AnnouncementBanner {...announcementProps} /> : null}
      <Header {...headerProps} />
      <Box as="main">
        <SkipNavContent />
        {children}
      </Box>
      <Footer {...footerProps} />
    </Box>
  )
}
