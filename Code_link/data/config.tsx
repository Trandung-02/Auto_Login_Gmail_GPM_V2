import { Link } from '@saas-ui/react'
import { Metadata } from 'next'
import { FaGithub, FaTwitter } from 'react-icons/fa'
import { FiCheck } from 'react-icons/fi'
import { Logo } from './logo'
import { SITE_MAIN_MESSAGE } from './site-message'

const siteConfig = {
  logo: Logo,
  seo: {
    title: SITE_MAIN_MESSAGE,
    description: SITE_MAIN_MESSAGE,
  } as Metadata,
  termsUrl: '#',
  privacyUrl: '#',
  header: {
    links: [
      {
        id: 'features',
        label: 'Thông tin',
      },
      {
        id: 'pricing',
        label: 'Nội dung',
      },
      {
        id: 'faq',
        label: 'Hỏi đáp',
      },
      {
        label: 'Đăng nhập',
        href: '#',
      },
      {
        label: 'Tiếp tục',
        href: '#',
        variant: 'primary',
      },
    ],
  },
  footer: {
    copyright: <>{SITE_MAIN_MESSAGE}</>,
    links: [
      {
        href: '#',
        label: 'Liên hệ',
      },
      {
        href: '#',
        label: <FaTwitter size="14" />,
      },
      {
        href: '#',
        label: <FaGithub size="14" />,
      },
    ],
  },
  signup: {
    title: SITE_MAIN_MESSAGE,
    features: [
      {
        icon: FiCheck,
        title: SITE_MAIN_MESSAGE,
        description: SITE_MAIN_MESSAGE,
      },
      {
        icon: FiCheck,
        title: SITE_MAIN_MESSAGE,
        description: SITE_MAIN_MESSAGE,
      },
      {
        icon: FiCheck,
        title: SITE_MAIN_MESSAGE,
        description: SITE_MAIN_MESSAGE,
      },
      {
        icon: FiCheck,
        title: SITE_MAIN_MESSAGE,
        description: SITE_MAIN_MESSAGE,
      },
    ],
  },
}

export default siteConfig
