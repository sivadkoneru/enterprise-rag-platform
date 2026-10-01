import { Skeleton } from "@/components/ui/skeleton";
export default function Loading() {
    return (
        <div className="space-y-6" role="status" aria-label="Loading page">
            <Skeleton className="h-9 w-56" />
            <Skeleton className="h-4 w-80 max-w-full" />
            <div className="grid gap-6 md:grid-cols-2">
                <Skeleton className="h-96" />
                <Skeleton className="h-96" />
            </div>
        </div>
    );
}
