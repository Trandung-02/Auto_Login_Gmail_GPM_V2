import { HStack, Text } from '@chakra-ui/react'
import { SITE_MAIN_MESSAGE } from './site-message'

export default {
  title: SITE_MAIN_MESSAGE,
  description: SITE_MAIN_MESSAGE,
  plans: [
    {
      id: 'oss',
      title: SITE_MAIN_MESSAGE,
      description: SITE_MAIN_MESSAGE,
      price: '—',
      features: [
        { title: SITE_MAIN_MESSAGE },
        { title: SITE_MAIN_MESSAGE },
        { title: SITE_MAIN_MESSAGE },
      ],
      action: {
        href: '#',
      },
    },
    {
      id: 'bootstrap',
      title: SITE_MAIN_MESSAGE,
      description: SITE_MAIN_MESSAGE,
      price: '—',
      isRecommended: true,
      features: [
        { title: SITE_MAIN_MESSAGE },
        { title: SITE_MAIN_MESSAGE },
        { title: SITE_MAIN_MESSAGE },
        null,
        { title: SITE_MAIN_MESSAGE, iconColor: 'green.500' },
      ],
      action: {
        href: '#',
      },
    },
    {
      id: 'startup',
      title: SITE_MAIN_MESSAGE,
      description: SITE_MAIN_MESSAGE,
      price: (
        <HStack>
          <Text fontSize="sm" color="gray.400">
            —
          </Text>
          <Text>—</Text>
        </HStack>
      ),
      features: [
        { title: SITE_MAIN_MESSAGE },
        { title: SITE_MAIN_MESSAGE },
        null,
        { title: SITE_MAIN_MESSAGE, iconColor: 'green.500' },
      ],
      action: {
        href: '#',
      },
    },
  ],
}
