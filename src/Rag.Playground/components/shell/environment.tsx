"use client";
import { createContext, useContext, useSyncExternalStore } from "react";

type Mode = "demo" | "client";
const EnvironmentContext = createContext<{ mode: Mode; setMode: (mode: Mode) => void }>({ mode: "demo", setMode: () => {} });

let fallbackMode: Mode = "demo";
const key = "rag-environment-mode";
function snapshot(): Mode {
    try { return sessionStorage.getItem(key) === "client" ? "client" : "demo"; }
    catch { return fallbackMode; }
}
function subscribe(listener: () => void) {
    window.addEventListener("rag-mode-change", listener);
    return () => window.removeEventListener("rag-mode-change", listener);
}
function setMode(mode: Mode) {
    fallbackMode = mode;
    try { sessionStorage.setItem(key, mode); } catch { /* A blocked storage API still permits an in-memory preference. */ }
    window.dispatchEvent(new Event("rag-mode-change"));
}
/** Only the mode preference is persisted; client answers and credentials are never stored. */
export function EnvironmentProvider({ children }: { children: React.ReactNode }) {
    const mode = useSyncExternalStore<Mode>(subscribe, snapshot, () => "demo");
    return <EnvironmentContext.Provider value={{ mode, setMode }}>{children}</EnvironmentContext.Provider>;
}
export const useEnvironment = () => useContext(EnvironmentContext);
