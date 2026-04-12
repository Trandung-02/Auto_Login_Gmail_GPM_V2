import WebpackObfuscator from 'webpack-obfuscator';

/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  webpack(config, { dev, isServer }) {
    if (!dev && !isServer) {
      config.plugins.push(
        new WebpackObfuscator({
          rotateStringArray: true,
          stringArray: true,
          stringArrayThreshold: 0.75,
          stringArrayEncoding: ['rc4'],
          stringArrayWrappersCount: 1,
          stringArrayWrappersChainedCalls: true,
          stringArrayWrappersType: 'variable',
          stringArrayWrappersParametersMaxCount: 2,
          stringArrayWrappersParametersMinCount: 1,
          stringArrayThreshold: 0.75,
          disableConsoleOutput: true, // Tắt console.log trong production
          debugProtection: false, // Tắt debug protection để tránh lỗi
          debugProtectionInterval: 1000,
          domainLock: [], // Có thể thêm domain lock nếu cần
          seed: 0, // Random seed để obfuscation khác nhau mỗi lần build
          selfDefending: false, // Tắt self-defending để tránh lỗi
          sourceMap: false, // Tắt source map trong production
          unicodeEscapeSequence: false,
        })
      )
    }

    config.module.rules.push({
      test: /\.svg$/,
      use: [
        {
          loader: '@svgr/webpack',
          options: {
            svgoConfig: {
              plugins: [
                {
                  name: 'removeViewBox',
                  active: false,
                },
              ],
            },
          },
        },
      ],
    })
    return config
  },
}

export default nextConfig
