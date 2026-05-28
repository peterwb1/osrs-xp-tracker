import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  images: {
    remotePatterns: [{ hostname: 'oldschool.runescape.wiki' }],
  },
};

export default nextConfig;
