"use client";
import { useMemo, useState } from "react";
import {
    ArrowDownToLine,
    Cloud,
    Database,
    Server,
    Settings2,
    Sparkles,
} from "lucide-react";
import { useEnvironment } from "@/components/shell/environment";
import {
    addComposeProfile,
    applyStorageTopology as applyStorageTopologyToValues,
    configCatalog,
    createDefaultFormValues,
    validateConfiguration,
    type FormValues,
} from "@/lib/integrations/catalog";
import {
    checkClientStatus,
    testConfiguredProvider,
    type ClientStatus,
    type IntegrationStatus,
} from "@/lib/integrations/runtime";
export const steps = [
    { id: "runtime", label: "Environment", icon: Server, groups: ["runtime"] },
    { id: "models", label: "Models", icon: Sparkles, groups: ["models"] },
    {
        id: "storage",
        label: "Storage",
        icon: Database,
        groups: ["storage", "vectors"],
    },
    {
        id: "retrieval",
        label: "Retrieval",
        icon: Settings2,
        groups: ["retrieval", "query"],
    },
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

export function useIntegrationSetup() {
    const { mode, setMode, allowClient } = useEnvironment();
    const [stepId, setStepId] = useState(steps[0]?.id ?? "runtime");
    const [values, setValues] = useState<FormValues>(composeValues);
    const [backendUrl, setBackendUrl] = useState("http://api:8080");
    const [status, setStatus] = useState<ClientStatus | null>(null);
    const [checkingStatus, setCheckingStatus] = useState(false);
    const [providerChecks, setProviderChecks] = useState<
        Partial<Record<IntegrationStatus["name"], IntegrationStatus | string>>
    >({});
    const [checkingProvider, setCheckingProvider] = useState<
        IntegrationStatus["name"] | null
    >(null);
    const [downloaded, setDownloaded] = useState(false);
    const [storageTopology, setStorageTopology] = useState<
        "local" | "external"
    >("local");
    const activeStep = steps.find((step) => step.id === stepId) ?? steps[0]!;
    const fields = useMemo(
        () =>
            configCatalog.groups.filter((group) =>
                activeStep.groups.includes(group.id),
            ),
        [activeStep],
    );
    const handleFieldChange = (id: string, value: string) =>
        setValues((current) => ({ ...current, [id]: value }));
    const selectStorageTopology = (topology: "local" | "external") => {
        setStorageTopology(topology);
        setValues((current) => applyStorageTopologyToValues(current, topology));
    };
    const enableEmulator = (profile: "s3-emulator" | "azure-emulator") =>
        setValues((current) =>
            addComposeProfile(current, profile, storageTopology === "local"),
        );
    const numericErrors = useMemo(
        () => validateConfiguration(values),
        [values],
    );
    const checkStatus = async () => {
        if (mode !== "client") return;
        setCheckingStatus(true);
        try {
            setStatus(await checkClientStatus());
        } finally {
            setCheckingStatus(false);
        }
    };
    const runProviderTest = async (provider: IntegrationStatus["name"]) => {
        if (mode !== "client") return;
        setCheckingProvider(provider);
        setProviderChecks((current) => ({ ...current, [provider]: undefined }));
        try {
            const result = await testConfiguredProvider(provider);
            setProviderChecks((current) => ({
                ...current,
                [provider]: result,
            }));
        } catch (error) {
            setProviderChecks((current) => ({
                ...current,
                [provider]:
                    error instanceof Error
                        ? error.message
                        : "Provider check failed.",
            }));
        } finally {
            setCheckingProvider(null);
        }
    };
    const index = steps.findIndex((step) => step.id === stepId);
    const nextStep = steps[Math.min(index + 1, steps.length - 1)];
    const previousStep = steps[Math.max(index - 1, 0)];

    return {
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
    };
}
