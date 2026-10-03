"use client";

import {
    ArrowLeft,
    ArrowRight,
    CircleHelp,
    KeyRound,
    Server,
    ShieldCheck,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Panel, PageHeading } from "@/components/shared/panel";
import { DataWorkspace } from "@/features/live/data-workspace";
import { configCatalog } from "@/lib/integrations/catalog";
import { steps, useIntegrationSetup } from "./use-integration-setup";

import {
    CopyCommand,
    DownloadConfiguration,
    ConfigurationInput,
    RuntimeStatus,
} from "./setup-panels";

export function IntegrationsPage() {
    const {
        mode,
        setMode,
        allowClient,
        stepId,
        setStepId,
        values,
        backendUrl,
        setBackendUrl,
        status,
        checkingStatus,
        providerChecks,
        checkingProvider,
        downloaded,
        setDownloaded,
        storageTopology,
        activeStep,
        fields,
        handleFieldChange,
        selectStorageTopology,
        enableEmulator,
        numericErrors,
        checkStatus,
        runProviderTest,
        index,
        nextStep,
        previousStep,
    } = useIntegrationSetup();
    return (
        <div className="space-y-5">
            <PageHeading
                eyebrow="Connect your backend"
                title="Integration setup"
                description="Configure the .NET runtime, inspect active server settings, and download client files for deployment."
                action={
                    <Badge variant="outline" className="gap-1.5">
                        <ShieldCheck size={12} className="text-primary" />
                        Secret values stay local
                    </Badge>
                }
            />

            <Panel
                title="Environment"
                icon={<Server size={14} className="text-primary" />}
                action={
                    <Button
                        size="sm"
                        variant={mode === "client" ? "secondary" : "outline"}
                        disabled={!allowClient}
                        onClick={() =>
                            setMode(mode === "client" ? "demo" : "client")
                        }
                        className="h-8 gap-1.5 text-[10px]"
                    >
                        {mode === "client"
                            ? "Client Environment · connected mode"
                            : allowClient
                              ? "Use Private Client Environment"
                              : "Public demo · client access disabled"}
                    </Button>
                }
            >
                <p className="text-muted text-[10px] leading-5">
                    {mode === "client"
                        ? "Client mode enables same-origin requests to your API through the server-side proxy. Your API key stays on the server."
                        : "Demo mode makes no runtime API requests. Client access requires a separately configured private-live deployment."}
                </p>
            </Panel>

            <RuntimeStatus
                status={status}
                isChecking={checkingStatus}
                onCheck={checkStatus}
                clientMode={mode === "client"}
                checks={providerChecks}
                checkingProvider={checkingProvider}
                onTestProvider={runProviderTest}
            />

            <Panel className="overflow-hidden" bodyClassName="p-0">
                <div
                    className="grid border-b bg-muted/30 sm:grid-cols-3 lg:grid-cols-6"
                    role="tablist"
                    aria-label="Integration setup steps"
                >
                    {steps.map(({ id, label, icon: Icon }, stepIndex) => (
                        <button
                            key={id}
                            type="button"
                            role="tab"
                            id={`setup-tab-${id}`}
                            aria-selected={stepId === id}
                            aria-controls="setup-step-panel"
                            onClick={() => setStepId(id)}
                            className={`flex min-w-0 items-center gap-2 border-b-2 px-3 py-3 text-left text-[10px] font-medium transition ${stepId === id ? "border-primary bg-card text-foreground" : "border-transparent text-muted-foreground hover:bg-muted/60"}`}
                        >
                            <span className="mono text-[9px] opacity-60">
                                {String(stepIndex + 1).padStart(2, "0")}
                            </span>
                            <Icon size={13} />
                            <span className="truncate">{label}</span>
                        </button>
                    ))}
                </div>
                <section
                    id="setup-step-panel"
                    role="tabpanel"
                    aria-labelledby={`setup-tab-${activeStep.id}`}
                    className="p-5"
                >
                    <div className="mb-5">
                        <div className="text-sm font-semibold">
                            {activeStep.label}
                        </div>
                        <p className="text-muted mt-1 text-[11px] leading-5">
                            {activeStep.id === "launch"
                                ? "Download an environment file or .NET settings document, then start the local stack."
                                : "These values build a local config file; they do not edit or persist changes to the running backend."}
                        </p>
                    </div>

                    {activeStep.id === "launch" ? (
                        <div className="space-y-5">
                            <div className="rounded-lg border border-primary/15 bg-accent/25 p-4">
                                <div className="flex items-center gap-2 text-xs font-semibold">
                                    <KeyRound
                                        size={13}
                                        className="text-primary"
                                    />
                                    Create the API key
                                </div>
                                <p className="text-muted mt-1 text-[10px] leading-4">
                                    Compose requires a non-empty RAG_API_KEY and
                                    shares it with the API and server-side
                                    client proxy. Generate it locally with{" "}
                                    <code className="mono">
                                        openssl rand -hex 32
                                    </code>
                                    , then add it to your private{" "}
                                    <code className="mono">.env.client</code>.
                                    Credentials are not entered or stored in
                                    this browser.
                                </p>
                            </div>
                            <div className="max-w-xl">
                                <label
                                    htmlFor="client-proxy-api-url"
                                    className="field-label text-xs"
                                >
                                    Backend URL for the server-side proxy
                                </label>
                                <input
                                    id="client-proxy-api-url"
                                    type="url"
                                    value={backendUrl}
                                    onChange={(event) =>
                                        setBackendUrl(event.target.value)
                                    }
                                    placeholder="http://api:8080"
                                    className="field"
                                />
                                <p className="text-muted mt-1.5 text-[10px] leading-4">
                                    Used in the generated env file as
                                    RAG_API_URL. Browser requests still go to
                                    the same-origin /api/client proxy.
                                </p>
                            </div>
                            {numericErrors.length > 0 && (
                                <div
                                    role="alert"
                                    className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-[10px] leading-5 text-destructive"
                                >
                                    <div className="font-semibold">
                                        Fix configuration values before
                                        downloading.
                                    </div>
                                    <ul className="mt-1 list-disc pl-4">
                                        {numericErrors.map((error) => (
                                            <li key={error}>{error}</li>
                                        ))}
                                    </ul>
                                </div>
                            )}
                            <DownloadConfiguration
                                values={values}
                                backendUrl={backendUrl}
                                disabled={numericErrors.length > 0}
                                onDownloaded={() => setDownloaded(true)}
                            />
                            {downloaded && (
                                <p
                                    role="status"
                                    className="text-[10px] text-emerald-700"
                                >
                                    Download started. Secret fields are blank in
                                    generated files; fill credentials locally in
                                    a private file.
                                </p>
                            )}
                            <div className="grid gap-4 lg:grid-cols-2">
                                <div className="rounded-lg bg-muted/50 p-4">
                                    <div className="eyebrow mb-2">
                                        Start the compose profile
                                    </div>
                                    <div className="flex items-center justify-between">
                                        <span className="eyebrow">
                                            Start the local stack
                                        </span>
                                        <CopyCommand
                                            command={`cp .env.client.example .env.client\n# Set a unique RAG_API_KEY in .env.client\ndocker compose -f compose.client.yml --env-file .env.client up --build -d`}
                                        />
                                    </div>
                                    <pre className="mono mt-2 overflow-x-auto whitespace-pre rounded border bg-card p-3 text-[10px] leading-5">{`cp .env.client.example .env.client\n# Set a unique RAG_API_KEY in .env.client\ndocker compose -f compose.client.yml --env-file .env.client up --build -d`}</pre>
                                    <p className="text-muted mt-2 text-[10px] leading-4">
                                        Or download the environment template and
                                        compose file, move them to the
                                        repository root, then add the key
                                        locally. MongoDB and Elasticsearch use
                                        persistent volumes and stay private on
                                        the Compose network.
                                    </p>
                                </div>
                                <div className="rounded-lg bg-muted/50 p-4">
                                    <div className="eyebrow mb-2">
                                        External storage alternatives
                                    </div>
                                    <p className="text-muted text-[10px] leading-5">
                                        Optional LocalStack and Azurite services
                                        start only with their Compose profiles.
                                        Set the corresponding profile and
                                        container-reachable endpoint in your
                                        private environment file.
                                    </p>
                                    <div className="mt-2 flex flex-wrap gap-1.5">
                                        <Badge
                                            variant="outline"
                                            className="text-[9px]"
                                        >
                                            S3_ENDPOINT
                                        </Badge>
                                        <Badge
                                            variant="outline"
                                            className="text-[9px]"
                                        >
                                            AZURE_BLOB_ENDPOINT
                                        </Badge>
                                    </div>
                                    <div className="mt-3 grid gap-3 text-[9px] sm:grid-cols-2">
                                        <div>
                                            <div className="mb-1 flex items-center justify-between">
                                                <span className="font-medium">
                                                    LocalStack · S3
                                                </span>
                                                <Button
                                                    type="button"
                                                    size="sm"
                                                    variant="ghost"
                                                    className="h-6 px-1.5 text-[9px]"
                                                    onClick={() =>
                                                        enableEmulator(
                                                            "s3-emulator",
                                                        )
                                                    }
                                                >
                                                    Enable profile
                                                </Button>
                                                <CopyCommand
                                                    command={`COMPOSE_PROFILES=${storageTopology === "local" ? "local-storage,s3-emulator" : "s3-emulator"}\nS3_ENDPOINT=http://localstack:4566\nAWS_ACCESS_KEY_ID=test\nAWS_SECRET_ACCESS_KEY=test`}
                                                />
                                            </div>
                                            <code className="mono">
                                                COMPOSE_PROFILES=s3-emulator
                                            </code>
                                            <br />
                                            <code className="mono">
                                                S3_ENDPOINT=http://localstack:4566
                                            </code>
                                        </div>
                                        <div>
                                            <div className="mb-1 flex items-center justify-between">
                                                <span className="font-medium">
                                                    Azurite · Azure Blob
                                                </span>
                                                <Button
                                                    type="button"
                                                    size="sm"
                                                    variant="ghost"
                                                    className="h-6 px-1.5 text-[9px]"
                                                    onClick={() =>
                                                        enableEmulator(
                                                            "azure-emulator",
                                                        )
                                                    }
                                                >
                                                    Enable profile
                                                </Button>
                                                <CopyCommand
                                                    command={`COMPOSE_PROFILES=${storageTopology === "local" ? "local-storage,azure-emulator" : "azure-emulator"}\nAZURE_BLOB_CONNECTION_STRING=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;`}
                                                />
                                            </div>
                                            <code className="mono">
                                                COMPOSE_PROFILES=azure-emulator
                                            </code>
                                            <br />
                                            <code className="mono">
                                                AZURE_BLOB_SERVICE_URI=http://azurite:10000/devstoreaccount1
                                            </code>
                                            <p className="text-muted mt-1">
                                                Azurite example uses its public
                                                emulator-only development
                                                credential.
                                            </p>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    ) : (
                        <div className="space-y-6">
                            {fields.map((group) => (
                                <fieldset key={group.id} className="min-w-0">
                                    <legend className="text-xs font-semibold">
                                        {group.title}
                                    </legend>
                                    <p className="text-muted mt-1 text-[10px] leading-4">
                                        {group.description}
                                    </p>
                                    {group.id === "runtime" && (
                                        <div className="mt-4 flex flex-wrap items-center gap-2 rounded-md border bg-muted/30 p-3">
                                            <span className="text-[10px] font-medium">
                                                Server-side proxy secret
                                            </span>
                                            <Badge
                                                variant="outline"
                                                className="text-[9px]"
                                            >
                                                RAG_API_KEY is never sent by
                                                browser JavaScript
                                            </Badge>
                                            <span className="text-muted text-[9px]">
                                                Set it in the web container
                                                environment to match the API
                                                setting.
                                            </span>
                                        </div>
                                    )}
                                    <div className="mt-4 grid gap-x-5 gap-y-4 md:grid-cols-2">
                                        {group.id === "storage" && (
                                            <div className="rounded-lg border bg-muted/20 p-3">
                                                <div className="mb-2 text-[10px] font-semibold">
                                                    Service topology
                                                </div>
                                                <div className="flex flex-wrap gap-2">
                                                    <Button
                                                        type="button"
                                                        size="sm"
                                                        variant={
                                                            storageTopology ===
                                                            "local"
                                                                ? "secondary"
                                                                : "outline"
                                                        }
                                                        onClick={() =>
                                                            selectStorageTopology(
                                                                "local",
                                                            )
                                                        }
                                                        className="h-7 text-[9px]"
                                                    >
                                                        Local Compose services
                                                    </Button>
                                                    <Button
                                                        type="button"
                                                        size="sm"
                                                        variant={
                                                            storageTopology ===
                                                            "external"
                                                                ? "secondary"
                                                                : "outline"
                                                        }
                                                        onClick={() =>
                                                            selectStorageTopology(
                                                                "external",
                                                            )
                                                        }
                                                        className="h-7 text-[9px]"
                                                    >
                                                        External services
                                                    </Button>
                                                </div>
                                                <p className="text-muted mt-2 text-[10px] leading-4">
                                                    {storageTopology === "local"
                                                        ? "Downloads target the private MongoDB and Elasticsearch containers. Change provider values below only when you intend to use another backend."
                                                        : "Choose external providers below and enter endpoints or connection names. The generated env file passes these choices to the API; the local Compose dependencies remain available but unused."}
                                                </p>
                                            </div>
                                        )}
                                        {group.fields.map((field) => (
                                            <div key={field.id}>
                                                <ConfigurationInput
                                                    field={field}
                                                    value={
                                                        values[field.id] ??
                                                        String(
                                                            field.defaultValue,
                                                        )
                                                    }
                                                    onChange={handleFieldChange}
                                                />
                                            </div>
                                        ))}
                                    </div>
                                </fieldset>
                            ))}
                            <div className="border-t pt-4 text-[10px] leading-5 text-muted-foreground">
                                <p>
                                    <strong className="text-foreground">
                                        Environment variables:
                                    </strong>{" "}
                                    {fields
                                        .flatMap((group) => group.fields)
                                        .flatMap((field) =>
                                            field.environmentAliases.slice(
                                                0,
                                                1,
                                            ),
                                        )
                                        .join(" · ") || "The launch step only."}
                                </p>
                                {fields.flatMap((group) => group.fields)
                                    .length > 0 && (
                                    <p className="mt-1">
                                        <strong className="text-foreground">
                                            Applies to:
                                        </strong>{" "}
                                        {fields
                                            .flatMap((group) => group.fields)
                                            .map((field) => field.appliesTo)
                                            .filter(
                                                (value, position, values) =>
                                                    values.indexOf(value) ===
                                                    position,
                                            )
                                            .join("; ")}
                                        . Values take effect on{" "}
                                        {fields
                                            .flatMap((group) => group.fields)
                                            .map((field) => field.changeEffect)
                                            .filter(
                                                (value, position, values) =>
                                                    values.indexOf(value) ===
                                                    position,
                                            )
                                            .join(", ")}
                                        .
                                    </p>
                                )}
                                {activeStep.id === "models" && (
                                    <p className="mt-1">
                                        The deterministic provider runs without
                                        external API credentials. Secret fields
                                        stay blank in downloads; add credentials
                                        directly to a private environment file.
                                    </p>
                                )}
                                {activeStep.id === "storage" && (
                                    <p className="mt-1">
                                        The local Compose profile selects
                                        persistent MongoDB document/job storage
                                        and Elasticsearch vector storage. Memory
                                        and file stores remain available for
                                        other deployments.
                                    </p>
                                )}
                                {activeStep.id === "retrieval" && (
                                    <p className="mt-1">
                                        Query defaults are configured on the API
                                        and the client query request supports
                                        retrieval controls. Demo-only reranking
                                        experiments remain local simulations.
                                    </p>
                                )}
                            </div>
                        </div>
                    )}

                    <div className="mt-6 flex items-center justify-between border-t pt-4">
                        <Button
                            type="button"
                            variant="ghost"
                            size="sm"
                            onClick={() =>
                                setStepId(previousStep?.id ?? activeStep.id)
                            }
                            disabled={index === 0}
                            className="gap-1.5 text-[10px]"
                        >
                            <ArrowLeft size={12} />
                            Previous
                        </Button>
                        <span className="mono text-[9px] text-muted-foreground">
                            Step {index + 1} of {steps.length}
                        </span>
                        <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            onClick={() =>
                                setStepId(nextStep?.id ?? activeStep.id)
                            }
                            disabled={index === steps.length - 1}
                            className="gap-1.5 text-[10px]"
                        >
                            Next
                            <ArrowRight size={12} />
                        </Button>
                    </div>
                </section>
            </Panel>

            <Panel
                title="Configuration source of truth"
                icon={<CircleHelp size={14} className="text-primary" />}
                bodyClassName="flex flex-wrap items-center justify-between gap-3"
            >
                <p className="text-muted text-[10px] leading-5">
                    The same catalog defines .NET option sections, environment
                    aliases, and secret redaction rules. Runtime status values
                    are fetched read-only from the backend; wizard values only
                    affect downloaded files.
                </p>
                <Badge variant="outline" className="mono text-[9px]">
                    {configCatalog.sourceOfTruth}
                </Badge>
            </Panel>

            <DataWorkspace />
        </div>
    );
}
