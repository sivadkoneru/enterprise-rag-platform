"use client";
import { Info } from "lucide-react";
import {
    Tooltip,
    TooltipContent,
    TooltipTrigger,
} from "@/components/ui/tooltip";
export function Hint({ text }: { text: string }) {
    return (
        <Tooltip>
            <TooltipTrigger asChild>
                <button
                    type="button"
                    aria-label={text}
                    className="text-muted inline-flex rounded"
                >
                    <Info size={12} />
                </button>
            </TooltipTrigger>
            <TooltipContent className="max-w-72 text-xs leading-5">
                {text}
            </TooltipContent>
        </Tooltip>
    );
}
