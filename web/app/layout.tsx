import type { Metadata } from "next";
import "./globals.css";
import { Providers } from "@/providers";

export const metadata: Metadata = {
  title: "OSRS XP Tracker",
  description: "Track your Old School RuneScape XP over time",
};

// Runs before React hydrates to prevent flash of wrong theme
const themeScript = `
(function() {
  try {
    var t = localStorage.getItem('theme');
    var dark = t === 'dark' || (!t && window.matchMedia('(prefers-color-scheme: dark)').matches);
    if (dark) document.documentElement.classList.add('dark');
  } catch(e) {}
})();
`;

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" className="h-full" suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body className="min-h-full antialiased">
        <Providers>{children}</Providers>
        <footer className="fixed bottom-2 right-3 text-xs text-gray-400 dark:text-gray-500 select-none pointer-events-none">
          v{process.env.NEXT_PUBLIC_APP_VERSION ?? 'dev'}
        </footer>
      </body>
    </html>
  );
}
