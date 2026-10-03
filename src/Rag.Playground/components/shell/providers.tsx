"use client";
import { ThemeProvider } from "next-themes";
import { EnvironmentProvider } from "./environment";
export function Providers({ children, allowClient }: { children: React.ReactNode; allowClient: boolean }) {
    return (
        <ThemeProvider
            attribute="class"
            defaultTheme="system"
            enableSystem
            disableTransitionOnChange
        >
            <EnvironmentProvider allowClient={allowClient}>{children}</EnvironmentProvider>
        </ThemeProvider>
    );
}
