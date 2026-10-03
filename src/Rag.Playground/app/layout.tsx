import type { Metadata } from "next";
import { Providers } from "@/components/shell/providers";
import { AppShell } from "@/components/shell/app-shell";
import "./globals.css";
import { privateLiveEnabled } from "@/lib/live/deployment";

export const dynamic = "force-dynamic";

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
                <Providers allowClient={privateLiveEnabled()}>
                    <AppShell>{children}</AppShell>
                </Providers>
            </body>
        </html>
    );
}
