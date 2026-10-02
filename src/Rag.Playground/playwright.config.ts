import { defineConfig, devices } from "@playwright/test";
export default defineConfig({
    testDir: "./tests/browser",
    fullyParallel: false,
    retries: 0,
    reporter: "list",
    use: {
        actionTimeout: 10000,
        baseURL: process.env.PLAYWRIGHT_BASE_URL ?? "http://127.0.0.1:3100",
        trace: "retain-on-failure",
        screenshot: "only-on-failure",
    },
    projects: [
        {
            name: "chromium",
            use: {
                ...devices["Desktop Chrome"],
                viewport: { width: 1440, height: 1000 },
            },
        },
    ],
    webServer: process.env.PLAYWRIGHT_BASE_URL ? undefined : {
        command: "npm run dev -- --hostname 127.0.0.1 --port 3100",
        url: process.env.PLAYWRIGHT_BASE_URL ?? "http://127.0.0.1:3100",
        reuseExistingServer: !process.env.CI,
        timeout: 120000,
    },
});
