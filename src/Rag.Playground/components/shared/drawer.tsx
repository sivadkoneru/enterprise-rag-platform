"use client";
import type { ReactNode } from "react";
import {
    Sheet,
    SheetContent,
    SheetHeader,
    SheetTitle,
    SheetDescription,
} from "@/components/ui/sheet";

export function Drawer({
    open,
    onOpenChange,
    title,
    description,
    children,
}: {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    title: string;
    description: string;
    children: ReactNode;
}) {
    return (
        <Sheet open={open} onOpenChange={onOpenChange}>
            <SheetContent className="w-full overflow-y-auto sm:max-w-xl">
                <SheetHeader className="border-b pb-5">
                    <SheetTitle>{title}</SheetTitle>
                    <SheetDescription>{description}</SheetDescription>
                </SheetHeader>
                <div className="px-5 pb-8">{children}</div>
            </SheetContent>
        </Sheet>
    );
}
