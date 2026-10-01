import { defineConfig, devices } from "@playwright/test";
export default defineConfig({
    testDir: "./tests/browser",
    fullyParallel: false,
    retries: 0,
    reporter: "list",
    use: {
        baseURL: "http://127.0.0.1:3100",
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
    webServer: {
        command: "npm run dev -- --hostname 127.0.0.1 --port 3100",
        url: "http://127.0.0.1:3100",
        reuseExistingServer: !process.env.CI,
        timeout: 120000,
    },
});
