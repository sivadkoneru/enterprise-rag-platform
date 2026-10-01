import type { NextConfig } from "next";
import { fileURLToPath } from "node:url";

const config: NextConfig = {
    turbopack: { root: fileURLToPath(new URL(".", import.meta.url)) },
    devIndicators: false,
    agentRules: false,
};
export default config;
