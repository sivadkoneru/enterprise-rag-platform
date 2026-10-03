"use client";
import { useEffect, useState } from "react";
import type { ClientCorpus, EnvironmentCapabilities, IndexProfile } from "@/lib/live/contracts";
import { liveGateway } from "@/lib/live/gateway";

export function useClientData() {
    const [corpora, setCorpora] = useState<ClientCorpus[]>([]);
    const [profiles, setProfiles] = useState<IndexProfile[]>([]);
    const [capabilities, setCapabilities] = useState<EnvironmentCapabilities | null>(null);
    const [corpusId, setCorpusId] = useState("");
    const [profileId, setProfileId] = useState("");
    const [error, setError] = useState("");
    const [loading, setLoading] = useState(true);
    const [revision, setRevision] = useState(0);
    useEffect(() => {
        const controller = new AbortController();
        Promise.all([liveGateway.listCorpora(controller.signal), liveGateway.capabilities(controller.signal)])
            .then(([items, caps]) => {
                if (controller.signal.aborted) return;
                setCorpora(items); setCapabilities(caps); setError("");
                setCorpusId(current => items.some(item => item.id === current) ? current : items[0]?.id ?? "");
            }).catch(err => { if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "Client data could not be loaded."); })
            .finally(() => { if (!controller.signal.aborted) setLoading(false); });
        return () => controller.abort();
    }, [revision]);
    useEffect(() => {
        if (!corpusId) return;
        const controller = new AbortController();
        liveGateway.listProfiles(corpusId, controller.signal).then(items => {
            if (controller.signal.aborted) return;
            setProfiles(items);
            setProfileId(current => items.some(item => item.id === current) ? current : items[0]?.id ?? "");
        }).catch(err => { if (!controller.signal.aborted) setError(err instanceof Error ? err.message : "Profiles could not be loaded."); });
        return () => controller.abort();
    }, [corpusId, revision]);
    const scopedProfiles = profiles.filter(profile => profile.corpusId === corpusId);
    return { corpora, profiles: scopedProfiles, capabilities, corpusId, profileId, setCorpusId, setProfileId, error, loading, refresh: () => setRevision(value => value + 1) };
}
