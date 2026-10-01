import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

export function Panel({
    title,
    icon,
    action,
    children,
    className,
    bodyClassName,
}: {
    title?: string;
    icon?: ReactNode;
    action?: ReactNode;
    children: ReactNode;
    className?: string;
    bodyClassName?: string;
}) {
    return (
        <section className={cn("panel", className)}>
            {title && (
                <div className="panel-heading">
                    <h2 className="panel-title">
                        {icon}
                        {title}
                    </h2>
                    {action}
                </div>
            )}
            <div className={cn("panel-body", bodyClassName)}>{children}</div>
        </section>
    );
}
export function PageHeading({
    eyebrow,
    title,
    description,
    action,
}: {
    eyebrow: string;
    title: string;
    description: string;
    action?: ReactNode;
}) {
    return (
        <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
            <div>
                <div className="eyebrow mb-2">{eyebrow}</div>
                <h1 className="text-[25px] font-semibold tracking-[-0.8px]">
                    {title}
                </h1>
                <p className="text-muted mt-1.5 text-xs leading-5">
                    {description}
                </p>
            </div>
            {action}
        </div>
    );
}
export function PageFooter() {
    return (
        <footer className="page-footer">
            <span className="flex items-center gap-2">
                <span className="status-dot" />
                All systems simulated · No external API calls
            </span>
            <span>
                Built to make retrieval explainable.{" "}
                <span className="mono ml-2">v1.0.0</span>
            </span>
        </footer>
    );
}
