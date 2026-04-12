'use client'

import {
  Box,
  ButtonGroup,
  Container,
  Flex,
  HStack,
  Heading,
  Icon,
  IconButton,
  Stack,
  Tag,
  Text,
  VStack,
  Wrap,
  useClipboard,
} from '@chakra-ui/react'
import { Br, Link } from '@saas-ui/react'
import type { NextPage } from 'next'
import Image from 'next/image'
import {
  FiArrowRight,
  FiBox,
  FiCheck,
  FiCode,
  FiCopy,
  FiFlag,
  FiGrid,
  FiLock,
  FiSearch,
  FiSliders,
  FiSmile,
  FiTerminal,
  FiThumbsUp,
  FiToggleLeft,
  FiTrendingUp,
  FiUserPlus,
} from 'react-icons/fi'

import * as React from 'react'

import { ButtonLink } from '#components/button-link/button-link'
import { Faq } from '#components/faq'
import { Features } from '#components/features'
import { BackgroundGradient } from '#components/gradients/background-gradient'
import { Hero } from '#components/hero'
import {
  Highlights,
  HighlightsItem,
  HighlightsTestimonialItem,
} from '#components/highlights'
import { ChakraLogo, NextjsLogo } from '#components/logos'
import { FallInPlace } from '#components/motion/fall-in-place'
import { Pricing } from '#components/pricing/pricing'
import { Testimonial, Testimonials } from '#components/testimonials'
import { Em } from '#components/typography'
import faq from '#data/faq'
import pricing from '#data/pricing'
import { SITE_MAIN_MESSAGE } from '#data/site-message'
import testimonials from '#data/testimonials'

const Home: NextPage = () => {
  return (
    <Box>
      <HeroSection />

      <HighlightsSection />

      <FeaturesSection />

      <TestimonialsSection />

      <PricingSection />

      <FaqSection />
    </Box>
  )
}

const HeroSection: React.FC = () => {
  return (
    <Box position="relative" overflow="hidden">
      <BackgroundGradient height="100%" zIndex="-1" />
      <Container maxW="container.xl" pt={{ base: 40, lg: 60 }} pb="40">
        <Stack direction={{ base: 'column', lg: 'row' }} alignItems="center">
          <Hero
            id="home"
            justifyContent="flex-start"
            px="0"
            title={
              <FallInPlace>
                <Em>{SITE_MAIN_MESSAGE}</Em>
              </FallInPlace>
            }
            description={
              <FallInPlace delay={0.4} fontWeight="medium">
                {SITE_MAIN_MESSAGE}
                <Br />
                {SITE_MAIN_MESSAGE}
              </FallInPlace>
            }
          >
            <FallInPlace delay={0.8}>
              <HStack pt="4" pb="12" spacing="8">
                <NextjsLogo height="28px" /> <ChakraLogo height="20px" />
              </HStack>

              <ButtonGroup spacing={4} alignItems="center">
                <ButtonLink colorScheme="primary" size="lg" href="#">
                  Tiếp tục
                </ButtonLink>
                <ButtonLink
                  size="lg"
                  href="#"
                  variant="outline"
                  rightIcon={
                    <Icon
                      as={FiArrowRight}
                      sx={{
                        transitionProperty: 'common',
                        transitionDuration: 'normal',
                        '.chakra-button:hover &': {
                          transform: 'translate(5px)',
                        },
                      }}
                    />
                  }
                >
                  Xem thêm
                </ButtonLink>
              </ButtonGroup>
            </FallInPlace>
          </Hero>
          <Box
            height="600px"
            position="absolute"
            display={{ base: 'none', lg: 'block' }}
            left={{ lg: '60%', xl: '55%' }}
            width="80vw"
            maxW="1100px"
            margin="0 auto"
          >
            <FallInPlace delay={1}>
              <Box overflow="hidden" height="100%">
                <Image
                  src="/static/screenshots/list.png"
                  width={1200}
                  height={762}
                  alt={SITE_MAIN_MESSAGE}
                  quality="75"
                  priority
                />
              </Box>
            </FallInPlace>
          </Box>
        </Stack>
      </Container>

      <Features
        id="benefits"
        columns={[1, 2, 4]}
        iconSize={4}
        innerWidth="container.xl"
        pt="20"
        features={[
          {
            title: SITE_MAIN_MESSAGE,
            icon: FiSmile,
            description: SITE_MAIN_MESSAGE,
            iconPosition: 'left',
            delay: 0.6,
          },
          {
            title: SITE_MAIN_MESSAGE,
            icon: FiSliders,
            description: SITE_MAIN_MESSAGE,
            iconPosition: 'left',
            delay: 0.8,
          },
          {
            title: SITE_MAIN_MESSAGE,
            icon: FiGrid,
            description: SITE_MAIN_MESSAGE,
            iconPosition: 'left',
            delay: 1,
          },
          {
            title: SITE_MAIN_MESSAGE,
            icon: FiThumbsUp,
            description: SITE_MAIN_MESSAGE,
            iconPosition: 'left',
            delay: 1.1,
          },
        ]}
        reveal={FallInPlace}
      />
    </Box>
  )
}

const HighlightsSection = () => {
  const { onCopy, hasCopied } = useClipboard(SITE_MAIN_MESSAGE)

  return (
    <Highlights>
      <HighlightsItem colSpan={[1, null, 2]} title={SITE_MAIN_MESSAGE}>
        <VStack alignItems="flex-start" spacing="8">
          <Text color="muted" fontSize="xl">
            <Em>{SITE_MAIN_MESSAGE}</Em>
            <Br />
            {SITE_MAIN_MESSAGE}
          </Text>

          <Flex
            rounded="full"
            borderWidth="1px"
            flexDirection="row"
            alignItems="center"
            py="1"
            ps="8"
            pe="2"
            bg="primary.900"
            _dark={{ bg: 'gray.900' }}
          >
            <Box>
              <Text color="yellow.400" display="inline">
                {SITE_MAIN_MESSAGE}
              </Text>
            </Box>
            <IconButton
              icon={hasCopied ? <FiCheck /> : <FiCopy />}
              aria-label="Sao chép nội dung"
              onClick={onCopy}
              variant="ghost"
              ms="4"
              isRound
              color="white"
            />
          </Flex>
        </VStack>
      </HighlightsItem>
      <HighlightsItem title={SITE_MAIN_MESSAGE}>
        <Text color="muted" fontSize="lg">
          {SITE_MAIN_MESSAGE}
        </Text>
      </HighlightsItem>
      <HighlightsTestimonialItem
        name="Người dùng"
        description={SITE_MAIN_MESSAGE}
        avatar="/static/images/avatar.jpg"
        gradient={['pink.200', 'purple.500']}
      >
        “{SITE_MAIN_MESSAGE}”
      </HighlightsTestimonialItem>
      <HighlightsItem colSpan={[1, null, 2]} title={SITE_MAIN_MESSAGE}>
        <Text color="muted" fontSize="lg">
          {SITE_MAIN_MESSAGE}
        </Text>
        <Wrap mt="8">
          {[SITE_MAIN_MESSAGE, SITE_MAIN_MESSAGE, SITE_MAIN_MESSAGE].map(
            (value, idx) => (
              <Tag
                key={idx}
                variant="subtle"
                colorScheme="purple"
                rounded="full"
                px="3"
              >
                {value}
              </Tag>
            ),
          )}
        </Wrap>
      </HighlightsItem>
    </Highlights>
  )
}

const FeaturesSection = () => {
  const inline = { variant: 'inline' as const }
  return (
    <Features
      id="features"
      title={
        <Heading
          lineHeight="short"
          fontSize={['2xl', null, '4xl']}
          textAlign="left"
          as="p"
        >
          {SITE_MAIN_MESSAGE}
          <Br />
          {SITE_MAIN_MESSAGE}
        </Heading>
      }
      description={
        <>
          {SITE_MAIN_MESSAGE}
          <Br />
          {SITE_MAIN_MESSAGE}
        </>
      }
      align="left"
      columns={[1, 2, 3]}
      iconSize={4}
      features={[
        {
          title: SITE_MAIN_MESSAGE,
          icon: FiBox,
          description: SITE_MAIN_MESSAGE,
          ...inline,
        },
        {
          title: SITE_MAIN_MESSAGE,
          icon: FiLock,
          description: SITE_MAIN_MESSAGE,
          ...inline,
        },
        {
          title: SITE_MAIN_MESSAGE,
          icon: FiSearch,
          description: SITE_MAIN_MESSAGE,
          ...inline,
        },
        {
          title: SITE_MAIN_MESSAGE,
          icon: FiUserPlus,
          description: SITE_MAIN_MESSAGE,
          ...inline,
        },
        {
          title: SITE_MAIN_MESSAGE,
          icon: FiFlag,
          description: SITE_MAIN_MESSAGE,
          ...inline,
        },
        {
          title: SITE_MAIN_MESSAGE,
          icon: FiTrendingUp,
          description: SITE_MAIN_MESSAGE,
          ...inline,
        },
        {
          title: SITE_MAIN_MESSAGE,
          icon: FiToggleLeft,
          description: SITE_MAIN_MESSAGE,
          ...inline,
        },
        {
          title: SITE_MAIN_MESSAGE,
          icon: FiTerminal,
          description: SITE_MAIN_MESSAGE,
          ...inline,
        },
        {
          title: SITE_MAIN_MESSAGE,
          icon: FiCode,
          description: (
            <>
              {SITE_MAIN_MESSAGE}{' '}
              <Link href="#">{SITE_MAIN_MESSAGE}</Link>
            </>
          ),
          ...inline,
        },
      ]}
    />
  )
}

const TestimonialsSection = () => {
  const columns = React.useMemo(() => {
    return testimonials.items.reduce<Array<typeof testimonials.items>>(
      (columns, t, i) => {
        columns[i % 3].push(t)

        return columns
      },
      [[], [], []],
    )
  }, [])

  return (
    <Testimonials
      title={testimonials.title}
      columns={[1, 2, 3]}
      innerWidth="container.xl"
    >
      <>
        {columns.map((column, i) => (
          <Stack key={i} spacing="8">
            {column.map((t, i) => (
              <Testimonial key={i} {...t} />
            ))}
          </Stack>
        ))}
      </>
    </Testimonials>
  )
}

const PricingSection = () => {
  return (
    <Pricing {...pricing}>
      <Text p="8" textAlign="center" color="muted">
        {SITE_MAIN_MESSAGE}
      </Text>
    </Pricing>
  )
}

const FaqSection = () => {
  return <Faq {...faq} />
}

export default Home
