"use client";

import { useMemo, useState } from "react";
import { Activity, ArrowDownToLine, ArrowLeft, ArrowRight, Check, CircleAlert, CircleHelp, CircleOff, Cloud, Copy, Database, Download, KeyRound, LoaderCircle, LockKeyhole, RefreshCw, Server, Settings2, ShieldCheck, Sparkles } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Panel, PageHeading } from "@/components/shared/panel";
import { useEnvironment } from "@/components/shell/environment";
import { DataWorkspace } from "@/features/live/data-workspace";
import { addComposeProfile, applyStorageTopology as applyStorageTopologyToValues, configCatalog, createAppSettings, createDefaultFormValues, createEnvironmentFile, makeDownload, validateConfiguration, type ConfigurationField, type FormValues } from "@/lib/integrations/catalog";
import { checkClientStatus, testConfiguredProvider, type ClientStatus, type EndpointResult, type IntegrationStatus } from "@/lib/integrations/runtime";

const steps = [
  { id: "runtime", label: "Environment", icon: Server, groups: ["runtime"] },
  { id: "models", label: "Models", icon: Sparkles, groups: ["models"] },
  { id: "storage", label: "Storage", icon: Database, groups: ["storage", "vectors"] },
  { id: "retrieval", label: "Retrieval", icon: Settings2, groups: ["retrieval", "query"] },
  { id: "sources", label: "Cloud sources", icon: Cloud, groups: ["sources"] },
  { id: "launch", label: "Launch", icon: ArrowDownToLine, groups: [] },
];

const composeValues: FormValues = {
  ...createDefaultFormValues(),
  "document-provider": "mongo",
  "document-connection": "mongodb://mongo:27017",
  "document-database": "rag",
  "job-provider": "mongo",
  "job-connection": "mongodb://mongo:27017",
  "job-database": "rag",
  "vector-provider": "elasticsearch",
  "vector-endpoint": "http://elasticsearch:9200",
  "vector-index": "rag-chunks",
  "vector-dimensions": "1536",
  "embedding-dimensions": "1536",
};

function fieldLabel(field: ConfigurationField): string {
  const rawLabel = field.option ?? field.key;
  return rawLabel.replace(/([a-z])([A-Z])/g, "$1 $2").replaceAll(":", " · ").replaceAll("_", " ");
}

function statusBadge(result: EndpointResult<unknown> | undefined) {
  if (!result) return <Badge variant="outline" className="gap-1.5"><CircleHelp size={11}/>Not checked</Badge>;
  if (result.status === "error") return <Badge variant="outline" className="gap-1.5 border-destructive/30 text-destructive"><CircleAlert size={11}/>Unavailable</Badge>;
  return <Badge variant="outline" className="gap-1.5 border-emerald-600/30 text-emerald-700"><Check size={11}/>Connected</Badge>;
}

function displayValue(field: ConfigurationField, value: string | number | boolean | null): string {
  if (value === null) return "Not set";
  let formatted = String(value);
  if (field.type === "url") {
    try {
      const url = new URL(formatted);
      url.username = "";
      url.password = "";
      for (const key of [...url.searchParams.keys()]) if (/(key|token|secret|password|credential|sig)/i.test(key)) url.searchParams.set(key, "[redacted]");
      formatted = url.toString();
    } catch {
      formatted = "Configured";
    }
  }
  return formatted.length > 90 ? `${formatted.slice(0, 87)}…` : formatted;
}

function CopyCommand({ command }: { command: string }) {
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
  return <Button type="button" variant="outline" size="sm" onClick={() => void copy()} className="h-7 gap-1 text-[9px]">{copied ? <Check size={11}/> : <Copy size={11}/>} {copied ? "Copied" : "Copy"}</Button>;
}

function DownloadConfiguration({ values, backendUrl, disabled, onDownloaded }: { values: FormValues; backendUrl: string; disabled: boolean; onDownloaded: () => void }) {
  const downloadEnvironment = () => {
    makeDownload("generated.env.client", createEnvironmentFile(values, backendUrl), "text/plain;charset=utf-8");
    onDownloaded();
  };
  const downloadSettings = () => {
    makeDownload("appsettings.Client.json", createAppSettings(values), "application/json;charset=utf-8");
    onDownloaded();
  };

  return (
    <div className="grid gap-3 sm:grid-cols-3">
      <button type="button" onClick={downloadEnvironment} disabled={disabled} className="group rounded-lg border p-4 text-left transition hover:border-primary hover:bg-accent/30 disabled:cursor-not-allowed disabled:opacity-50">
        <span className="flex items-center justify-between"><span className="text-xs font-semibold">generated.env.client</span><Download size={14} className="text-primary transition group-hover:translate-y-0.5"/></span>
        <span className="text-muted mt-1 block text-[10px] leading-4">Uses wizard values. Secret fields remain blank and must be filled locally in a private file.</span>
      </button>
      <button type="button" onClick={downloadSettings} disabled={disabled} className="group rounded-lg border p-4 text-left transition hover:border-primary hover:bg-accent/30 disabled:cursor-not-allowed disabled:opacity-50">
        <span className="flex items-center justify-between"><span className="text-xs font-semibold">appsettings.Client.json</span><Download size={14} className="text-primary transition group-hover:translate-y-0.5"/></span>
        <span className="text-muted mt-1 block text-[10px] leading-4">Produces section-based .NET configuration. Secret fields are omitted for environment-only injection.</span>
      </button>
      <a href="/compose.client.yml" download="compose.client.yml" aria-disabled={disabled} className={`group rounded-lg border p-4 text-left transition hover:border-primary hover:bg-accent/30 ${disabled ? "pointer-events-none opacity-50" : ""}`}>
        <span className="flex items-center justify-between"><span className="text-xs font-semibold">compose.client.yml</span><Download size={14} className="text-primary transition group-hover:translate-y-0.5"/></span>
        <span className="text-muted mt-1 block text-[10px] leading-4">Works with the selected providers in generated.env.client; includes private stores and optional emulators.</span>
      </a>
    </div>
  );
}

function ConfigurationInput({ field, value, onChange }: { field: ConfigurationField; value: string; onChange: (id: string, value: string) => void }) {
  const id = `client-config-${field.id}`;
  const label = fieldLabel(field);
  const helpId = `${id}-help`;
  const sharedProps = { id, "aria-describedby": helpId, value, onChange: (event: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => onChange(field.id, event.target.value) };

  if (field.secret) return <div className="min-w-0"><span className="field-label text-xs">{label}<span className="flex items-center gap-1 text-[9px] text-muted-foreground"><LockKeyhole size={10}/>Secret</span></span><div id={id} aria-describedby={helpId} className="flex min-h-9 items-center gap-2 rounded-md border bg-muted/30 px-3 text-[10px] text-muted-foreground"><LockKeyhole size={12}/><span>Set locally in a private environment file</span></div><p id={helpId} className="text-muted mt-1.5 text-[10px] leading-4">{field.help} The downloaded template leaves this value blank. Applies to {field.appliesTo}; change takes effect on {field.changeEffect}.</p></div>;

  return (
    <div className="min-w-0">
      <label htmlFor={id} className="field-label text-xs">{label}{field.secret && <span className="flex items-center gap-1 text-[9px] text-muted-foreground"><LockKeyhole size={10}/>Secret</span>}</label>
      {field.type === "select" ? (
        <select {...sharedProps} className="field">
          {(field.choices ?? []).map((choice) => <option key={choice} value={choice}>{choice}</option>)}
        </select>
      ) : field.type === "textarea" || field.type === "multiline" ? (
        <textarea {...sharedProps} rows={field.type === "textarea" ? 3 : 2} placeholder={field.placeholder} className="field resize-y leading-5"/>
      ) : field.type === "boolean" ? (
        <select {...sharedProps} className="field"><option value="true">Enabled</option><option value="false">Disabled</option></select>
      ) : (
        <div className="relative">
          <input {...sharedProps} type={field.type === "number" ? "number" : field.type === "url" ? "url" : "text"} inputMode={field.type === "number" ? "decimal" : undefined} min={field.minimum} max={field.maximum} step={field.type === "number" && typeof field.defaultValue === "number" && !Number.isInteger(field.defaultValue) ? "any" : undefined} placeholder={field.placeholder} className="field"/>
        </div>
      )}
      <p id={helpId} className="text-muted mt-1.5 text-[10px] leading-4">{field.help} {field.validation} Applies to {field.appliesTo}; change takes effect on {field.changeEffect}.</p>
    </div>
  );
}

function RuntimeStatus({ status, isChecking, onCheck, clientMode, checks, checkingProvider, onTestProvider }: { status: ClientStatus | null; isChecking: boolean; onCheck: () => void; clientMode: boolean; checks: Partial<Record<IntegrationStatus["name"], IntegrationStatus | string>>; checkingProvider: IntegrationStatus["name"] | null; onTestProvider: (provider: IntegrationStatus["name"]) => void }) {
  const ready = status?.readiness.data?.ready;
  const readinessError = status?.readiness.message;
  const capabilities = status?.capabilities.data;
  const config = status?.configuration.data;
  const configuredFields = useMemo(() => config?.groups.flatMap((group) => group.fields.filter((field) => field.configured).map((field) => ({ group: group.title, field }))) ?? [], [config]);

  return (
    <Panel title="Runtime connection" icon={<Activity size={14} className="text-primary"/>} action={<Button size="sm" variant="outline" onClick={onCheck} disabled={!clientMode || isChecking} className="h-8 gap-1.5 text-[10px]">{isChecking ? <LoaderCircle size={12} className="animate-spin"/> : <RefreshCw size={12}/>}Check status</Button>}>
      {!clientMode && <p className="text-muted mb-3 text-[10px] leading-5">Runtime checks are paused in Demo Environment. Switch to Client Environment to check your backend.</p>}
      <div className="flex flex-wrap items-center gap-2">
        {statusBadge(status?.readiness)}
        {statusBadge(status?.capabilities)}
        {statusBadge(status?.configuration)}
        {typeof ready === "boolean" && <Badge variant={ready ? "secondary" : "outline"}>{ready ? "Ready" : "Needs attention"}</Badge>}
      </div>
      {status && <p className="text-muted mt-2 text-[10px]">Last checked {new Date(status.checkedAt).toLocaleTimeString()}</p>}

      {readinessError && <p className="mt-3 rounded-md border border-destructive/20 bg-destructive/5 p-3 text-[11px] leading-5 text-destructive">{readinessError}</p>}
      {status?.readiness.data?.checks && <ul className="mt-4 grid gap-2 sm:grid-cols-2">
        {status.readiness.data.checks.map((check) => <li key={check.id} className="flex items-start gap-2 rounded-md border px-3 py-2.5">
          {check.status === "ready" ? <Check size={12} className="mt-0.5 shrink-0 text-emerald-600"/> : check.status === "unconfigured" ? <CircleHelp size={12} className="mt-0.5 shrink-0 text-[var(--warning)]"/> : <CircleOff size={12} className="mt-0.5 shrink-0 text-destructive"/>}
          <span><span className="block text-[10px] font-medium">{check.id}</span><span className="text-muted mt-0.5 block text-[10px] leading-4">{check.detail}</span></span>
        </li>)}
      </ul>}

      {capabilities && <div className="mt-4 rounded-md bg-muted/50 p-3">
        <div className="eyebrow mb-2">Active backend capabilities</div>
        <div className="flex flex-wrap gap-1.5">
          {[capabilities.llmProvider, capabilities.documentStore, capabilities.vectorStore, `${capabilities.embeddingDimensions} dimensions`, capabilities.hybrid ? capabilities.lexicalAlgorithm : "Vector only", capabilities.reranker ? "Reranker enabled" : "No reranker", ...capabilities.supportedStrategies].map((item) => <Badge key={item} variant="secondary" className="text-[9px]">{item}</Badge>)}
          {[`topK ${capabilities.defaultQuery.topK}`, `mode ${capabilities.defaultQuery.mode}`, `min relevance ${capabilities.defaultQuery.minRelevance}`, `context ${capabilities.defaultQuery.maxContextTokens} tokens`, capabilities.defaultQuery.reranker ? "default reranker on" : "default reranker off", capabilities.defaultQuery.neighbors ? "neighbors on" : "neighbors off"].map((item) => <Badge key={item} variant="outline" className="text-[9px]">{item}</Badge>)}
        </div>
      </div>}

      {status?.configuration.status === "connected" && <div className="mt-4">
        <div className="eyebrow mb-2">Configured server values · secrets redacted</div>
        {configuredFields.length === 0 ? <p className="text-muted text-[10px]">No optional values are configured.</p> : <div className="grid gap-1.5 sm:grid-cols-2">{configuredFields.map(({ group, field }) => <div key={field.id} className="flex min-w-0 items-center justify-between gap-3 rounded border bg-card px-2.5 py-2 text-[9px]"><span className="truncate"><span className="text-muted">{group} · </span>{field.key}</span><span className="mono max-w-[60%] truncate text-right">{field.secret ? "•••••• · set" : displayValue(field, field.value)}</span></div>)}</div>}
      </div>}

      <div className="mt-4 border-t pt-4">
        <div className="eyebrow mb-2">Test configured model endpoints</div>
        <p className="text-muted mb-3 text-[10px] leading-4">Tests use server-side provider credentials and do not accept credentials or endpoint overrides from the browser.</p>
        <div className="flex flex-wrap gap-2">{(["embedding", "chat", "reranker"] as const).map((provider) => {
          const result = checks[provider];
          const running = checkingProvider === provider;
          return <div key={provider} className="flex items-center gap-2 rounded-md border px-2 py-1.5">
            <Button type="button" variant="outline" size="sm" className="h-7 text-[9px] capitalize" disabled={!clientMode || running || checkingProvider !== null} onClick={() => onTestProvider(provider)}>{running && <LoaderCircle size={11} className="animate-spin"/>}Test {provider}</Button>
            {result && <span role="status" className={`text-[9px] ${typeof result === "string" || result.status === "failed" ? "text-destructive" : result.status === "healthy" ? "text-emerald-700" : "text-muted-foreground"}`}>{typeof result === "string" ? result : result.status === "healthy" ? `Healthy · ${result.latencyMs} ms` : result.status === "failed" ? "Check failed; inspect server status." : "Not configured"}</span>}
          </div>;
        })}</div>
        {!clientMode && <p className="text-muted mt-2 text-[10px]">Switch to Client Environment to run provider checks.</p>}
      </div>
    </Panel>
  );
}

export function IntegrationsPage() {
  const { mode, setMode } = useEnvironment();
  const [stepId, setStepId] = useState(steps[0]?.id ?? "runtime");
  const [values, setValues] = useState<FormValues>(composeValues);
  const [backendUrl, setBackendUrl] = useState("http://api:8080");
  const [status, setStatus] = useState<ClientStatus | null>(null);
  const [checkingStatus, setCheckingStatus] = useState(false);
  const [providerChecks, setProviderChecks] = useState<Partial<Record<IntegrationStatus["name"], IntegrationStatus | string>>>({});
  const [checkingProvider, setCheckingProvider] = useState<IntegrationStatus["name"] | null>(null);
  const [downloaded, setDownloaded] = useState(false);
  const [storageTopology, setStorageTopology] = useState<"local" | "external">("local");
  const activeStep = steps.find((step) => step.id === stepId) ?? steps[0]!;
  const fields = useMemo(() => configCatalog.groups.filter((group) => activeStep.groups.includes(group.id)), [activeStep]);
  const handleFieldChange = (id: string, value: string) => setValues((current) => ({ ...current, [id]: value }));
  const selectStorageTopology = (topology: "local" | "external") => {
    setStorageTopology(topology);
    setValues((current) => applyStorageTopologyToValues(current, topology));
  };
  const enableEmulator = (profile: "s3-emulator" | "azure-emulator") => setValues((current) => addComposeProfile(current, profile, storageTopology === "local"));
  const numericErrors = useMemo(() => validateConfiguration(values), [values]);
  const checkStatus = async () => {
    if (mode !== "client") return;
    setCheckingStatus(true);
    try { setStatus(await checkClientStatus()); }
    finally { setCheckingStatus(false); }
  };
  const runProviderTest = async (provider: IntegrationStatus["name"]) => {
    if (mode !== "client") return;
    setCheckingProvider(provider);
    setProviderChecks((current) => ({ ...current, [provider]: undefined }));
    try {
      const result = await testConfiguredProvider(provider);
      setProviderChecks((current) => ({ ...current, [provider]: result }));
    }
    catch (error) { setProviderChecks((current) => ({ ...current, [provider]: error instanceof Error ? error.message : "Provider check failed." })); }
    finally { setCheckingProvider(null); }
  };
  const index = steps.findIndex((step) => step.id === stepId);
  const nextStep = steps[Math.min(index + 1, steps.length - 1)];
  const previousStep = steps[Math.max(index - 1, 0)];

  return (
    <div className="space-y-5">
      <PageHeading eyebrow="Connect your backend" title="Integration setup" description="Configure the .NET runtime, inspect active server settings, and download client files for deployment." action={<Badge variant="outline" className="gap-1.5"><ShieldCheck size={12} className="text-primary"/>Secret values stay local</Badge>}/>

      <Panel title="Environment" icon={<Server size={14} className="text-primary"/>} action={<Button size="sm" variant={mode === "client" ? "secondary" : "outline"} onClick={() => setMode(mode === "client" ? "demo" : "client")} className="h-8 gap-1.5 text-[10px]">{mode === "client" ? "Client Environment · connected mode" : "Use Client Environment"}</Button>}>
        <p className="text-muted text-[10px] leading-5">{mode === "client" ? "Client mode enables same-origin requests to your API through the server-side proxy. Your API key stays on the server." : "Demo mode is fully local and makes no runtime API requests. Switch to Client Environment after starting your backend."}</p>
      </Panel>

      <RuntimeStatus status={status} isChecking={checkingStatus} onCheck={checkStatus} clientMode={mode === "client"} checks={providerChecks} checkingProvider={checkingProvider} onTestProvider={runProviderTest}/>

      <Panel className="overflow-hidden" bodyClassName="p-0">
        <div className="grid border-b bg-muted/30 sm:grid-cols-3 lg:grid-cols-6" role="tablist" aria-label="Integration setup steps">
          {steps.map(({ id, label, icon: Icon }, stepIndex) => <button key={id} type="button" role="tab" id={`setup-tab-${id}`} aria-selected={stepId === id} aria-controls="setup-step-panel" onClick={() => setStepId(id)} className={`flex min-w-0 items-center gap-2 border-b-2 px-3 py-3 text-left text-[10px] font-medium transition ${stepId === id ? "border-primary bg-card text-foreground" : "border-transparent text-muted-foreground hover:bg-muted/60"}`}><span className="mono text-[9px] opacity-60">{String(stepIndex + 1).padStart(2, "0")}</span><Icon size={13}/><span className="truncate">{label}</span></button>)}
        </div>
        <section id="setup-step-panel" role="tabpanel" aria-labelledby={`setup-tab-${activeStep.id}`} className="p-5">
          <div className="mb-5">
            <div className="text-sm font-semibold">{activeStep.label}</div>
            <p className="text-muted mt-1 text-[11px] leading-5">{activeStep.id === "launch" ? "Download an environment file or .NET settings document, then start the local stack." : "These values build a local config file; they do not edit or persist changes to the running backend."}</p>
          </div>

          {activeStep.id === "launch" ? (
            <div className="space-y-5">
              <div className="rounded-lg border border-primary/15 bg-accent/25 p-4">
                <div className="flex items-center gap-2 text-xs font-semibold"><KeyRound size={13} className="text-primary"/>Create the API key</div>
                <p className="text-muted mt-1 text-[10px] leading-4">Compose requires a non-empty RAG_API_KEY and shares it with the API and server-side client proxy. Generate it locally with <code className="mono">openssl rand -hex 32</code>, then add it to your private <code className="mono">.env.client</code>. Credentials are not entered or stored in this browser.</p>
              </div>
              <div className="max-w-xl">
                <label htmlFor="client-proxy-api-url" className="field-label text-xs">Backend URL for the server-side proxy</label>
                <input id="client-proxy-api-url" type="url" value={backendUrl} onChange={(event) => setBackendUrl(event.target.value)} placeholder="http://api:8080" className="field"/>
                <p className="text-muted mt-1.5 text-[10px] leading-4">Used in the generated env file as RAG_API_URL. Browser requests still go to the same-origin /api/client proxy.</p>
              </div>
              {numericErrors.length > 0 && <div role="alert" className="rounded-md border border-destructive/30 bg-destructive/5 p-3 text-[10px] leading-5 text-destructive"><div className="font-semibold">Fix configuration values before downloading.</div><ul className="mt-1 list-disc pl-4">{numericErrors.map((error) => <li key={error}>{error}</li>)}</ul></div>}
              <DownloadConfiguration values={values} backendUrl={backendUrl} disabled={numericErrors.length > 0} onDownloaded={() => setDownloaded(true)}/>
              {downloaded && <p role="status" className="text-[10px] text-emerald-700">Download started. Secret fields are blank in generated files; fill credentials locally in a private file.</p>}
              <div className="grid gap-4 lg:grid-cols-2">
                <div className="rounded-lg bg-muted/50 p-4">
                  <div className="eyebrow mb-2">Start the compose profile</div>
                  <div className="flex items-center justify-between"><span className="eyebrow">Start the local stack</span><CopyCommand command={`cp .env.client.example .env.client\n# Set a unique RAG_API_KEY in .env.client\ndocker compose -f compose.client.yml --env-file .env.client up --build -d`}/></div>
                  <pre className="mono mt-2 overflow-x-auto whitespace-pre rounded border bg-card p-3 text-[10px] leading-5">{`cp .env.client.example .env.client\n# Set a unique RAG_API_KEY in .env.client\ndocker compose -f compose.client.yml --env-file .env.client up --build -d`}</pre>
                  <p className="text-muted mt-2 text-[10px] leading-4">Or download the environment template and compose file, move them to the repository root, then add the key locally. MongoDB and Elasticsearch use persistent volumes and stay private on the Compose network.</p>
                </div>
                <div className="rounded-lg bg-muted/50 p-4">
                  <div className="eyebrow mb-2">External storage alternatives</div>
                  <p className="text-muted text-[10px] leading-5">Optional LocalStack and Azurite services start only with their Compose profiles. Set the corresponding profile and container-reachable endpoint in your private environment file.</p>
                  <div className="mt-2 flex flex-wrap gap-1.5"><Badge variant="outline" className="text-[9px]">S3_ENDPOINT</Badge><Badge variant="outline" className="text-[9px]">AZURE_BLOB_ENDPOINT</Badge></div>
                  <div className="mt-3 grid gap-3 text-[9px] sm:grid-cols-2"><div><div className="mb-1 flex items-center justify-between"><span className="font-medium">LocalStack · S3</span><Button type="button" size="sm" variant="ghost" className="h-6 px-1.5 text-[9px]" onClick={() => enableEmulator("s3-emulator")}>Enable profile</Button><CopyCommand command={`COMPOSE_PROFILES=${storageTopology === "local" ? "local-storage,s3-emulator" : "s3-emulator"}\nS3_ENDPOINT=http://localstack:4566\nAWS_ACCESS_KEY_ID=test\nAWS_SECRET_ACCESS_KEY=test`}/></div><code className="mono">COMPOSE_PROFILES=s3-emulator</code><br/><code className="mono">S3_ENDPOINT=http://localstack:4566</code></div><div><div className="mb-1 flex items-center justify-between"><span className="font-medium">Azurite · Azure Blob</span><Button type="button" size="sm" variant="ghost" className="h-6 px-1.5 text-[9px]" onClick={() => enableEmulator("azure-emulator")}>Enable profile</Button><CopyCommand command={`COMPOSE_PROFILES=${storageTopology === "local" ? "local-storage,azure-emulator" : "azure-emulator"}\nAZURE_BLOB_CONNECTION_STRING=DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;`}/></div><code className="mono">COMPOSE_PROFILES=azure-emulator</code><br/><code className="mono">AZURE_BLOB_SERVICE_URI=http://azurite:10000/devstoreaccount1</code><p className="text-muted mt-1">Azurite example uses its public emulator-only development credential.</p></div></div>
                </div>
              </div>
            </div>
          ) : (
            <div className="space-y-6">
              {fields.map((group) => <fieldset key={group.id} className="min-w-0">
                <legend className="text-xs font-semibold">{group.title}</legend>
                <p className="text-muted mt-1 text-[10px] leading-4">{group.description}</p>
                {group.id === "runtime" && <div className="mt-4 flex flex-wrap items-center gap-2 rounded-md border bg-muted/30 p-3">
                  <span className="text-[10px] font-medium">Server-side proxy secret</span>
                  <Badge variant="outline" className="text-[9px]">RAG_API_KEY is never sent by browser JavaScript</Badge>
                  <span className="text-muted text-[9px]">Set it in the web container environment to match the API setting.</span>
                </div>}
                <div className="mt-4 grid gap-x-5 gap-y-4 md:grid-cols-2">
              {group.id === "storage" && <div className="rounded-lg border bg-muted/20 p-3"><div className="mb-2 text-[10px] font-semibold">Service topology</div><div className="flex flex-wrap gap-2"><Button type="button" size="sm" variant={storageTopology === "local" ? "secondary" : "outline"} onClick={() => selectStorageTopology("local")} className="h-7 text-[9px]">Local Compose services</Button><Button type="button" size="sm" variant={storageTopology === "external" ? "secondary" : "outline"} onClick={() => selectStorageTopology("external")} className="h-7 text-[9px]">External services</Button></div><p className="text-muted mt-2 text-[10px] leading-4">{storageTopology === "local" ? "Downloads target the private MongoDB and Elasticsearch containers. Change provider values below only when you intend to use another backend." : "Choose external providers below and enter endpoints or connection names. The generated env file passes these choices to the API; the local Compose dependencies remain available but unused."}</p></div>}
              {group.fields.map((field) => <div key={field.id}>
                    <ConfigurationInput field={field} value={values[field.id] ?? String(field.defaultValue)} onChange={handleFieldChange}/>
                  </div>)}
                </div>
              </fieldset>)}
              <div className="border-t pt-4 text-[10px] leading-5 text-muted-foreground">
                <p><strong className="text-foreground">Environment variables:</strong> {fields.flatMap((group) => group.fields).flatMap((field) => field.environmentAliases.slice(0, 1)).join(" · ") || "The launch step only."}</p>
                {fields.flatMap((group) => group.fields).length > 0 && <p className="mt-1"><strong className="text-foreground">Applies to:</strong> {fields.flatMap((group) => group.fields).map((field) => field.appliesTo).filter((value, position, values) => values.indexOf(value) === position).join("; ")}. Values take effect on {fields.flatMap((group) => group.fields).map((field) => field.changeEffect).filter((value, position, values) => values.indexOf(value) === position).join(", ")}.</p>}
                {activeStep.id === "models" && <p className="mt-1">The deterministic provider runs without external API credentials. Secret fields stay blank in downloads; add credentials directly to a private environment file.</p>}
                {activeStep.id === "storage" && <p className="mt-1">The local Compose profile selects persistent MongoDB document/job storage and Elasticsearch vector storage. Memory and file stores remain available for other deployments.</p>}
                {activeStep.id === "retrieval" && <p className="mt-1">Query defaults are configured on the API and the client query request supports retrieval controls. Demo-only reranking experiments remain local simulations.</p>}
              </div>
            </div>
          )}

          <div className="mt-6 flex items-center justify-between border-t pt-4">
            <Button type="button" variant="ghost" size="sm" onClick={() => setStepId(previousStep?.id ?? activeStep.id)} disabled={index === 0} className="gap-1.5 text-[10px]"><ArrowLeft size={12}/>Previous</Button>
            <span className="mono text-[9px] text-muted-foreground">Step {index + 1} of {steps.length}</span>
            <Button type="button" variant="outline" size="sm" onClick={() => setStepId(nextStep?.id ?? activeStep.id)} disabled={index === steps.length - 1} className="gap-1.5 text-[10px]">Next<ArrowRight size={12}/></Button>
          </div>
        </section>
      </Panel>

      <Panel title="Configuration source of truth" icon={<CircleHelp size={14} className="text-primary"/>} bodyClassName="flex flex-wrap items-center justify-between gap-3">
        <p className="text-muted text-[10px] leading-5">The same catalog defines .NET option sections, environment aliases, and secret redaction rules. Runtime status values are fetched read-only from the backend; wizard values only affect downloaded files.</p>
        <Badge variant="outline" className="mono text-[9px]">{configCatalog.sourceOfTruth}</Badge>
      </Panel>

      <DataWorkspace />
    </div>
  );
}
