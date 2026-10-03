"use client";

import { useMemo, useState } from "react";
import {
    Activity,
    Check,
    CircleAlert,
    CircleHelp,
    CircleOff,
    Copy,
    Download,
    LoaderCircle,
    LockKeyhole,
    RefreshCw,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Panel } from "@/components/shared/panel";

import {
    createAppSettings,
    createEnvironmentFile,
    makeDownload,
    type ConfigurationField,
    type FormValues,
} from "@/lib/integrations/catalog";
import {
    type ClientStatus,
    type EndpointResult,
    type IntegrationStatus,
} from "@/lib/integrations/runtime";

export function fieldLabel(field: ConfigurationField): string {
    const rawLabel = field.option ?? field.key;
    return rawLabel
        .replace(/([a-z])([A-Z])/g, "$1 $2")
        .replaceAll(":", " · ")
        .replaceAll("_", " ");
}

export function statusBadge(result: EndpointResult<unknown> | undefined) {
    if (!result)
        return (
            <Badge variant="outline" className="gap-1.5">
                <CircleHelp size={11} />
                Not checked
            </Badge>
        );
    if (result.status === "error")
        return (
            <Badge
                variant="outline"
                className="gap-1.5 border-destructive/30 text-destructive"
            >
                <CircleAlert size={11} />
                Unavailable
            </Badge>
        );
    return (
        <Badge
            variant="outline"
            className="gap-1.5 border-emerald-600/30 text-emerald-700"
        >
            <Check size={11} />
            Connected
        </Badge>
    );
}

export function displayValue(
    field: ConfigurationField,
    value: string | number | boolean | null,
): string {
    if (value === null) return "Not set";
    let formatted = String(value);
    if (field.type === "url") {
        try {
            const url = new URL(formatted);
            url.username = "";
            url.password = "";
            for (const key of [...url.searchParams.keys()])
                if (/(key|token|secret|password|credential|sig)/i.test(key))
                    url.searchParams.set(key, "[redacted]");
            formatted = url.toString();
        } catch {
            formatted = "Configured";
        }
    }
    return formatted.length > 90 ? `${formatted.slice(0, 87)}…` : formatted;
}

export function CopyCommand({ command }: { command: string }) {
    const [copied, setCopied] = useState(false);
    const copy = async () => {
        try {
            await navigator.clipboard.writeText(command);
            setCopied(true);
            window.setTimeout(() => setCopied(false), 1500);
        } catch {
            setCopied(false);
        }
    };
    return (
        <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => void copy()}
            className="h-7 gap-1 text-[9px]"
        >
            {copied ? <Check size={11} /> : <Copy size={11} />}{" "}
            {copied ? "Copied" : "Copy"}
        </Button>
    );
}

export function DownloadConfiguration({
    values,
    backendUrl,
    disabled,
    onDownloaded,
}: {
    values: FormValues;
    backendUrl: string;
    disabled: boolean;
    onDownloaded: () => void;
}) {
    const downloadEnvironment = () => {
        makeDownload(
            "generated.env.client",
            createEnvironmentFile(values, backendUrl),
            "text/plain;charset=utf-8",
        );
        onDownloaded();
    };
    const downloadSettings = () => {
        makeDownload(
            "appsettings.Client.json",
            createAppSettings(values),
            "application/json;charset=utf-8",
        );
        onDownloaded();
    };

    return (
        <div className="grid gap-3 sm:grid-cols-3">
            <button
                type="button"
                onClick={downloadEnvironment}
                disabled={disabled}
                className="group rounded-lg border p-4 text-left transition hover:border-primary hover:bg-accent/30 disabled:cursor-not-allowed disabled:opacity-50"
            >
                <span className="flex items-center justify-between">
                    <span className="text-xs font-semibold">
                        generated.env.client
                    </span>
                    <Download
                        size={14}
                        className="text-primary transition group-hover:translate-y-0.5"
                    />
                </span>
                <span className="text-muted mt-1 block text-[10px] leading-4">
                    Uses wizard values. Secret fields remain blank and must be
                    filled locally in a private file.
                </span>
            </button>
            <button
                type="button"
                onClick={downloadSettings}
                disabled={disabled}
                className="group rounded-lg border p-4 text-left transition hover:border-primary hover:bg-accent/30 disabled:cursor-not-allowed disabled:opacity-50"
            >
                <span className="flex items-center justify-between">
                    <span className="text-xs font-semibold">
                        appsettings.Client.json
                    </span>
                    <Download
                        size={14}
                        className="text-primary transition group-hover:translate-y-0.5"
                    />
                </span>
                <span className="text-muted mt-1 block text-[10px] leading-4">
                    Produces section-based .NET configuration. Secret fields are
                    omitted for environment-only injection.
                </span>
            </button>
            <a
                href="/compose.client.yml"
                download="compose.client.yml"
                aria-disabled={disabled}
                className={`group rounded-lg border p-4 text-left transition hover:border-primary hover:bg-accent/30 ${disabled ? "pointer-events-none opacity-50" : ""}`}
            >
                <span className="flex items-center justify-between">
                    <span className="text-xs font-semibold">
                        compose.client.yml
                    </span>
                    <Download
                        size={14}
                        className="text-primary transition group-hover:translate-y-0.5"
                    />
                </span>
                <span className="text-muted mt-1 block text-[10px] leading-4">
                    Works with the selected providers in generated.env.client;
                    includes private stores and optional emulators.
                </span>
            </a>
        </div>
    );
}

export function ConfigurationInput({
    field,
    value,
    onChange,
}: {
    field: ConfigurationField;
    value: string;
    onChange: (id: string, value: string) => void;
}) {
    const id = `client-config-${field.id}`;
    const label = fieldLabel(field);
    const helpId = `${id}-help`;
    const sharedProps = {
        id,
        "aria-describedby": helpId,
        value,
        onChange: (
            event: React.ChangeEvent<
                HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement
            >,
        ) => onChange(field.id, event.target.value),
    };

    if (field.secret)
        return (
            <div className="min-w-0">
                <span className="field-label text-xs">
                    {label}
                    <span className="flex items-center gap-1 text-[9px] text-muted-foreground">
                        <LockKeyhole size={10} />
                        Secret
                    </span>
                </span>
                <div
                    id={id}
                    aria-describedby={helpId}
                    className="flex min-h-9 items-center gap-2 rounded-md border bg-muted/30 px-3 text-[10px] text-muted-foreground"
                >
                    <LockKeyhole size={12} />
                    <span>Set locally in a private environment file</span>
                </div>
                <p
                    id={helpId}
                    className="text-muted mt-1.5 text-[10px] leading-4"
                >
                    {field.help} The downloaded template leaves this value
                    blank. Applies to {field.appliesTo}; change takes effect on{" "}
                    {field.changeEffect}.
                </p>
            </div>
        );

    return (
        <div className="min-w-0">
            <label htmlFor={id} className="field-label text-xs">
                {label}
                {field.secret && (
                    <span className="flex items-center gap-1 text-[9px] text-muted-foreground">
                        <LockKeyhole size={10} />
                        Secret
                    </span>
                )}
            </label>
            {field.type === "select" ? (
                <select {...sharedProps} className="field">
                    {(field.choices ?? []).map((choice) => (
                        <option key={choice} value={choice}>
                            {choice}
                        </option>
                    ))}
                </select>
            ) : field.type === "textarea" || field.type === "multiline" ? (
                <textarea
                    {...sharedProps}
                    rows={field.type === "textarea" ? 3 : 2}
                    placeholder={field.placeholder}
                    className="field resize-y leading-5"
                />
            ) : field.type === "boolean" ? (
                <select {...sharedProps} className="field">
                    <option value="true">Enabled</option>
                    <option value="false">Disabled</option>
                </select>
            ) : (
                <div className="relative">
                    <input
                        {...sharedProps}
                        type={
                            field.type === "number"
                                ? "number"
                                : field.type === "url"
                                  ? "url"
                                  : "text"
                        }
                        inputMode={
                            field.type === "number" ? "decimal" : undefined
                        }
                        min={field.minimum}
                        max={field.maximum}
                        step={
                            field.type === "number" &&
                            typeof field.defaultValue === "number" &&
                            !Number.isInteger(field.defaultValue)
                                ? "any"
                                : undefined
                        }
                        placeholder={field.placeholder}
                        className="field"
                    />
                </div>
            )}
            <p id={helpId} className="text-muted mt-1.5 text-[10px] leading-4">
                {field.help} {field.validation} Applies to {field.appliesTo};
                change takes effect on {field.changeEffect}.
            </p>
        </div>
    );
}

export function RuntimeStatus({
    status,
    isChecking,
    onCheck,
    clientMode,
    checks,
    checkingProvider,
    onTestProvider,
}: {
    status: ClientStatus | null;
    isChecking: boolean;
    onCheck: () => void;
    clientMode: boolean;
    checks: Partial<
        Record<IntegrationStatus["name"], IntegrationStatus | string>
    >;
    checkingProvider: IntegrationStatus["name"] | null;
    onTestProvider: (provider: IntegrationStatus["name"]) => void;
}) {
    const ready = status?.readiness.data?.ready;
    const readinessError = status?.readiness.message;
    const capabilities = status?.capabilities.data;
    const config = status?.configuration.data;
    const configuredFields = useMemo(
        () =>
            config?.groups.flatMap((group) =>
                group.fields
                    .filter((field) => field.configured)
                    .map((field) => ({ group: group.title, field })),
            ) ?? [],
        [config],
    );

    return (
        <Panel
            title="Runtime connection"
            icon={<Activity size={14} className="text-primary" />}
            action={
                <Button
                    size="sm"
                    variant="outline"
                    onClick={onCheck}
                    disabled={!clientMode || isChecking}
                    className="h-8 gap-1.5 text-[10px]"
                >
                    {isChecking ? (
                        <LoaderCircle size={12} className="animate-spin" />
                    ) : (
                        <RefreshCw size={12} />
                    )}
                    Check status
                </Button>
            }
        >
            {!clientMode && (
                <p className="text-muted mb-3 text-[10px] leading-5">
                    Runtime checks are disabled in Demo Mode. Use a private-live deployment to check your backend.
                </p>
            )}
            <div className="flex flex-wrap items-center gap-2">
                {statusBadge(status?.readiness)}
                {statusBadge(status?.capabilities)}
                {statusBadge(status?.configuration)}
                {typeof ready === "boolean" && (
                    <Badge variant={ready ? "secondary" : "outline"}>
                        {ready ? "Ready" : "Needs attention"}
                    </Badge>
                )}
            </div>
            {status && (
                <p className="text-muted mt-2 text-[10px]">
                    Last checked{" "}
                    {new Date(status.checkedAt).toLocaleTimeString()}
                </p>
            )}

            {readinessError && (
                <p className="mt-3 rounded-md border border-destructive/20 bg-destructive/5 p-3 text-[11px] leading-5 text-destructive">
                    {readinessError}
                </p>
            )}
            {status?.readiness.data?.checks && (
                <ul className="mt-4 grid gap-2 sm:grid-cols-2">
                    {status.readiness.data.checks.map((check) => (
                        <li
                            key={check.id}
                            className="flex items-start gap-2 rounded-md border px-3 py-2.5"
                        >
                            {check.status === "ready" ? (
                                <Check
                                    size={12}
                                    className="mt-0.5 shrink-0 text-emerald-600"
                                />
                            ) : check.status === "configured" ? (
                                <CircleHelp size={12} className="text-amber-600" />
                            ) : check.status === "unconfigured" ? (
                                <CircleHelp
                                    size={12}
                                    className="mt-0.5 shrink-0 text-[var(--warning)]"
                                />
                            ) : (
                                <CircleOff
                                    size={12}
                                    className="mt-0.5 shrink-0 text-destructive"
                                />
                            )}
                            <span>
                                <span className="block text-[10px] font-medium">
                                    {check.id}
                                </span>
                                <span className="text-muted mt-0.5 block text-[10px] leading-4">
                                    {check.detail}
                                </span>
                            </span>
                        </li>
                    ))}
                </ul>
            )}

            {capabilities && (
                <div className="mt-4 rounded-md bg-muted/50 p-3">
                    <div className="eyebrow mb-2">
                        Active backend capabilities
                    </div>
                    <div className="flex flex-wrap gap-1.5">
                        {[
                            capabilities.llmProvider,
                            capabilities.documentStore,
                            capabilities.vectorStore,
                            `${capabilities.embeddingDimensions} dimensions`,
                            capabilities.hybrid
                                ? capabilities.lexicalAlgorithm
                                : "Vector only",
                            capabilities.reranker
                                ? "Reranker enabled"
                                : "No reranker",
                            ...capabilities.supportedStrategies,
                        ].map((item) => (
                            <Badge
                                key={item}
                                variant="secondary"
                                className="text-[9px]"
                            >
                                {item}
                            </Badge>
                        ))}
                        {[
                            `topK ${capabilities.defaultQuery.topK}`,
                            `mode ${capabilities.defaultQuery.mode}`,
                            `min relevance ${capabilities.defaultQuery.minRelevance}`,
                            `context ${capabilities.defaultQuery.maxContextTokens} tokens`,
                            capabilities.defaultQuery.reranker
                                ? "default reranker on"
                                : "default reranker off",
                            capabilities.defaultQuery.neighbors
                                ? "neighbors on"
                                : "neighbors off",
                        ].map((item) => (
                            <Badge
                                key={item}
                                variant="outline"
                                className="text-[9px]"
                            >
                                {item}
                            </Badge>
                        ))}
                    </div>
                </div>
            )}

            {status?.configuration.status === "connected" && (
                <div className="mt-4">
                    <div className="eyebrow mb-2">
                        Configured server values · secrets redacted
                    </div>
                    {configuredFields.length === 0 ? (
                        <p className="text-muted text-[10px]">
                            No optional values are configured.
                        </p>
                    ) : (
                        <div className="grid gap-1.5 sm:grid-cols-2">
                            {configuredFields.map(({ group, field }) => (
                                <div
                                    key={field.id}
                                    className="flex min-w-0 items-center justify-between gap-3 rounded border bg-card px-2.5 py-2 text-[9px]"
                                >
                                    <span className="truncate">
                                        <span className="text-muted">
                                            {group} ·{" "}
                                        </span>
                                        {field.key}
                                    </span>
                                    <span className="mono max-w-[60%] truncate text-right">
                                        {field.secret
                                            ? "•••••• · set"
                                            : displayValue(field, field.value)}
                                    </span>
                                </div>
                            ))}
                        </div>
                    )}
                </div>
            )}

            <div className="mt-4 border-t pt-4">
                <div className="eyebrow mb-2">
                    Test configured model endpoints
                </div>
                <p className="text-muted mb-3 text-[10px] leading-4">
                    Tests use server-side provider credentials and do not accept
                    credentials or endpoint overrides from the browser.
                </p>
                <div className="flex flex-wrap gap-2">
                    {(["embedding", "chat", "reranker"] as const).map(
                        (provider) => {
                            const result = checks[provider];
                            const running = checkingProvider === provider;
                            return (
                                <div
                                    key={provider}
                                    className="flex items-center gap-2 rounded-md border px-2 py-1.5"
                                >
                                    <Button
                                        type="button"
                                        variant="outline"
                                        size="sm"
                                        className="h-7 text-[9px] capitalize"
                                        disabled={
                                            !clientMode ||
                                            running ||
                                            checkingProvider !== null
                                        }
                                        onClick={() => onTestProvider(provider)}
                                    >
                                        {running && (
                                            <LoaderCircle
                                                size={11}
                                                className="animate-spin"
                                            />
                                        )}
                                        Test {provider}
                                    </Button>
                                    {result && (
                                        <span
                                            role="status"
                                            className={`text-[9px] ${typeof result === "string" || result.status === "failed" ? "text-destructive" : result.status === "healthy" ? "text-emerald-700" : "text-muted-foreground"}`}
                                        >
                                            {typeof result === "string"
                                                ? result
                                                : result.status === "healthy"
                                                  ? `${result.message} · ${result.latencyMs} ms`
                                                  : result.status === "failed"
                                                    ? "Check failed; inspect server status."
                                                    : "Not configured"}
                                        </span>
                                    )}
                                </div>
                            );
                        },
                    )}
                </div>
                {!clientMode && (
                    <p className="text-muted mt-2 text-[10px]">
                        Switch to Client Environment to run provider checks.
                    </p>
                )}
            </div>
        </Panel>
    );
}
