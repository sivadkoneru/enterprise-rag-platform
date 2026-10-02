import type { Metadata } from "next";
import { RetrievalPage } from "@/features/retrieval/retrieval-page";
import { EnvironmentView } from "@/features/live/environment-view";
export const metadata: Metadata = { title: "Retrieval" };
export default function Page() {
    return <EnvironmentView page="retrieval"><RetrievalPage /></EnvironmentView>;
}
