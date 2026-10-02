"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { useTheme } from "next-themes";
import {
    Activity,
    ArrowUpRight,
    BookOpen,
    Boxes,
    ChevronRight,
    CircleHelp,
    FlaskConical,
    GitFork as Github,
    Layers3,
    Menu,
    Moon,
    Network,
    Search,
    Settings2,
    Sun,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Sheet, SheetContent, SheetTitle } from "@/components/ui/sheet";
import {
    Tooltip,
    TooltipContent,
    TooltipProvider,
    TooltipTrigger,
} from "@/components/ui/tooltip";
import { AboutDrawer } from "@/features/about/about-drawer";
import { useEnvironment } from "./environment";
import { cn } from "@/lib/utils";

const navigation = [
    { href: "/", label: "Playground", icon: FlaskConical },
    { href: "/retrieval", label: "Retrieval", icon: Search },
    { href: "/evaluation", label: "Evaluation", icon: Activity },
    { href: "/architecture", label: "Architecture", icon: Network },
    { href: "/integrations", label: "Integrations & Setup", icon: Settings2 },
];

export function AppShell({ children }: { children: React.ReactNode }) {
    const pathname = usePathname();
    const { mode, setMode } = useEnvironment();
    const { resolvedTheme, setTheme } = useTheme();
    const [mobileOpen, setMobileOpen] = useState(false);
    const [aboutOpen, setAboutOpen] = useState(false);
    const current =
        navigation.find((item) => item.href === pathname)?.label ??
        "Playground";
    const sidebar = (
        <>
            <Link
                href="/"
                className="flex h-[68px] items-center gap-2.5 border-b px-6"
                onClick={() => setMobileOpen(false)}
            >
                <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
                    <Layers3 size={19} />
                </span>
                <span className="text-[17px] font-semibold tracking-[-.6px]">
                    RAG
                    <span className="font-normal text-muted-foreground">
                        {" "}
                        /{" "}
                    </span>
                    Lab
                </span>
                <span className="ml-auto rounded border px-1.5 py-0.5 text-[9px] font-medium text-muted-foreground">
                    {mode === "demo" ? "DEMO" : "CLIENT"}
                </span>
            </Link>
            <div className="px-4 py-6">
                <div className="mb-7 flex items-center gap-2.5 rounded-lg border bg-background p-2.5">
                    <span className="flex h-8 w-8 items-center justify-center rounded-md border bg-card">
                        <Boxes size={16} className="text-primary" />
                    </span>
                    <div>
                        <div className="text-xs font-medium">
                            Engineering workspace
                        </div>
                        <div className="mt-0.5 text-[10px] text-muted-foreground">
                            {mode === "demo" ? "Local demo environment" : "Client environment"}
                        </div>
                    </div>
                </div>
                <p className="eyebrow mb-3 px-3">Explore</p>
                <nav aria-label="Main navigation" className="space-y-1">
                    {navigation.map(({ href, label, icon: Icon }) => (
                        <Link
                            key={href}
                            href={href}
                            onClick={() => setMobileOpen(false)}
                            aria-current={
                                pathname === href ? "page" : undefined
                            }
                            className={cn(
                                "flex items-center gap-3 rounded-md px-3 py-2.5 text-xs font-medium transition-colors",
                                pathname === href
                                    ? "bg-accent text-accent-foreground"
                                    : "text-muted-foreground hover:bg-muted hover:text-foreground",
                            )}
                        >
                            <Icon size={16} />
                            {label}
                            {pathname === href && (
                                <span className="ml-auto h-1.5 w-1.5 rounded-full bg-primary" />
                            )}
                        </Link>
                    ))}
                </nav>
            </div>
            <div className="mt-auto px-4 pb-5">
                <div className="mb-5 rounded-lg border p-3.5">
                    <div className="mb-2 flex items-center gap-2 text-[11px] font-medium">
                        <BookOpen size={13} className="text-primary" />
                        Evidence, before answers.
                    </div>
                    <p className="text-[11px] leading-[1.7] text-muted-foreground">
                        Explore what was retrieved, why it ranked, and how it
                        shaped the answer.
                    </p>
                    <Link
                        href="/architecture"
                        onClick={() => setMobileOpen(false)}
                        className="mt-3 flex items-center gap-1 text-[11px] font-medium text-primary"
                    >
                        Explore the pipeline
                        <ArrowUpRight size={12} />
                    </Link>
                </div>
                <a
                    href="https://github.com/sivadkoneru/enterprise-rag-platform"
                    target="_blank"
                    rel="noreferrer"
                    className="flex items-center gap-3 rounded px-3 py-2 text-xs text-muted-foreground hover:bg-muted"
                >
                    <Github size={15} />
                    GitHub repository
                    <ArrowUpRight size={12} className="ml-auto" />
                </a>
                <button
                    className="flex w-full items-center gap-3 rounded px-3 py-2 text-xs text-muted-foreground hover:bg-muted"
                    onClick={() => {
                        setAboutOpen(true);
                        setMobileOpen(false);
                    }}
                >
                    <CircleHelp size={15} />
                    About this project
                </button>
                <div className="mt-5 flex items-center justify-between border-t px-2 pt-4 text-[10px] text-muted-foreground">
                    <span className="flex items-center gap-2">
                        <span className="status-dot" />
                        {mode === "demo" ? "Demo Mode" : "Client Environment"}
                    </span>
                    <span className="mono">v1.0</span>
                </div>
            </div>
        </>
    );
    return (
        <TooltipProvider delayDuration={150}>
            <a
                href="#main-content"
                className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded focus:bg-card focus:p-3"
            >
                Skip to main content
            </a>
            <aside className="shell-sidebar">{sidebar}</aside>
            <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
                <SheetContent side="left" className="w-[260px] gap-0 p-0">
                    <SheetTitle className="sr-only">Navigation</SheetTitle>
                    {sidebar}
                </SheetContent>
            </Sheet>
            <div className="shell-main">
                <header className="shell-header">
                    <div className="flex min-w-0 items-center gap-3">
                        <Button
                            variant="ghost"
                            size="icon"
                            className="lg:hidden"
                            aria-label="Open navigation"
                            onClick={() => setMobileOpen(true)}
                        >
                            <Menu size={18} />
                        </Button>
                        <span className="truncate text-xs font-medium">
                            Enterprise RAG Playground
                        </span>
                        <ChevronRight
                            size={12}
                            className="hidden text-muted-foreground 2xl:block"
                        />
                        <span className="hidden text-xs text-muted-foreground 2xl:block">
                            {current}
                        </span>
                    </div>
                    <div className="flex shrink-0 items-center gap-4">
                        <div className="hidden items-center gap-4 text-[10px] text-muted-foreground xl:flex">
                            {(mode === "demo" ? [
                                "Demo Corpus",
                                "Elasticsearch",
                                "Embeddings Ready",
                            ] : ["Client data", "Live API"]).map((label) => (
                                <Tooltip key={label}>
                                    <TooltipTrigger asChild>
                                        <button className="flex items-center gap-1.5">
                                            <span className={mode === "demo" ? "status-dot" : "h-1.5 w-1.5 rounded-full bg-muted-foreground"} />
                                            {label}
                                        </button>
                                    </TooltipTrigger>
                                    <TooltipContent>
                                        {mode === "demo" ? "Simulated status. This demo uses local fixtures." : "Connection status is available in Integrations & Setup."}
                                    </TooltipContent>
                                </Tooltip>
                            ))}
                        </div>
                        <Badge
                            variant="outline"
                            className="hidden gap-1.5 rounded-md bg-muted/50 text-[10px] font-normal sm:flex"
                        >
                            <span className="h-1.5 w-1.5 rounded-full bg-primary" />
                            {mode === "demo" ? "API: Demo Mode" : "API: Client Environment"}
                        </Badge>
                        <select aria-label="Environment mode" className="field !w-auto !max-w-[150px] !py-1.5 !text-[10px]" value={mode} onChange={e => setMode(e.target.value === "client" ? "client" : "demo")}>
                            <option value="demo">Demo</option>
                            <option value="client">Client Environment</option>
                        </select>
                        <div className="h-5 border-l" />
                        <Button
                            variant="ghost"
                            size="icon"
                            className="h-8 w-8"
                            aria-label="Toggle color theme"
                            onClick={() =>
                                setTheme(
                                    resolvedTheme === "dark" ? "light" : "dark",
                                )
                            }
                        >
                            <Sun size={16} className="hidden dark:block" />
                            <Moon size={16} className="dark:hidden" />
                        </Button>
                    </div>
                </header>
                <main id="main-content" className="page-content">
                    {children}
                </main>
            </div>
            <AboutDrawer open={aboutOpen} onOpenChange={setAboutOpen} />
        </TooltipProvider>
    );
}
