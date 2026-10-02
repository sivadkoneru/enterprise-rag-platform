"use client";
import { ThemeProvider } from "next-themes";
import { EnvironmentProvider } from "./environment";
export function Providers({ children }: { children: React.ReactNode }) {
    return (
        <ThemeProvider
            attribute="class"
            defaultTheme="system"
            enableSystem
            disableTransitionOnChange
        >
            <EnvironmentProvider>{children}</EnvironmentProvider>
        </ThemeProvider>
    );
}
