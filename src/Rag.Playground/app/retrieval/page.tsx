import type { Metadata } from "next";
import { RetrievalPage } from "@/features/retrieval/retrieval-page";
export const metadata: Metadata = { title: "Retrieval" };
export default function Page() {
    return <RetrievalPage />;
}
