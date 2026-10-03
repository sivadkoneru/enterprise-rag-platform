import { validateResponse } from "./validate-response";
export async function clientRequest<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(`/api/client/${path}`, {
        ...init,
        headers: { ...(init?.body ? { "Content-Type": "application/json" } : {}), ...init?.headers },
        cache: "no-store",
    });
    if (!response.ok) {
        const data: unknown = await response.json().catch(() => null);
        const message = data && typeof data === "object" && "detail" in data && typeof data.detail === "string"
            ? data.detail : data && typeof data === "object" && "message" in data && typeof data.message === "string"
                ? data.message : `Request failed (${response.status}). Check Integrations & Setup.`;
        throw new Error(message);
    }
    const value: unknown = await response.json();
    validateResponse(path, value, init?.method);
    return value as T;
}

export function downloadJson(filename: string, value: unknown) {
    const url = URL.createObjectURL(new Blob([JSON.stringify(value, null, 2)], { type: "application/json" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}
