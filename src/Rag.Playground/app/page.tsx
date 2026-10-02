import { PlaygroundPage } from "@/features/playground/playground-page";
import { EnvironmentView } from "@/features/live/environment-view";
export default function Page() {
    return <EnvironmentView page="playground"><PlaygroundPage /></EnvironmentView>;
}
