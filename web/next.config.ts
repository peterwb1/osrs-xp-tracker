import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: 'standalone',
  images: {
    unoptimized: true,
    remotePatterns: [{ hostname: 'oldschool.runescape.wiki' }],
  },
};

export default nextConfig;
