"use client";
import { AlertCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
export default function ErrorPage({ reset }: { reset: () => void }) {
    return (
        <div role="alert" className="panel p-10 text-center">
            <AlertCircle className="mx-auto mb-4 text-destructive" />
            <h1 className="text-lg font-semibold">
                This view could not be loaded
            </h1>
            <p className="my-4 text-muted-foreground">
                Please try again. Your demo data is stored locally.
            </p>
            <Button onClick={reset}>Try again</Button>
        </div>
    );
}
