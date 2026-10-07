import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Dugout | Performance Lab",
  description: "Explore benchmark runs, regressions, query plans, and advisor findings.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
