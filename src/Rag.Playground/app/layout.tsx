import type { Metadata } from "next";
import { Providers } from "@/components/shell/providers";
import { AppShell } from "@/components/shell/app-shell";
import "./globals.css";

export const metadata: Metadata = {
    title: { default: "Playground · RAG Lab", template: "%s · RAG Lab" },
    description:
        "An enterprise RAG engineering workbench. Inspect grounded answers, retrieval evidence, evaluation benchmarks, and pipeline diagnostics.",
};
export default function RootLayout({
    children,
}: Readonly<{ children: React.ReactNode }>) {
    return (
        <html lang="en" suppressHydrationWarning>
            <body>
                <Providers>
                    <AppShell>{children}</AppShell>
                </Providers>
            </body>
        </html>
    );
}
