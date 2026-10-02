"use client";
import { Slider } from "@/components/ui/slider";
import { Switch } from "@/components/ui/switch";
import { Hint } from "@/components/shared/hint";
import type { EnvironmentCapabilities, LiveQueryRequest } from "@/lib/live/contracts";

export type QuerySettings = Pick<LiveQueryRequest, "topK" | "mode" | "reranker" | "minRelevance" | "neighbors" | "maxContextTokens">;
export const initialQuerySettings: QuerySettings = { topK: 5, mode: "vector", reranker: false, minRelevance: 0.7, neighbors: false, maxContextTokens: 4096 };

export function QueryControls({ settings, onChange, capabilities, disabled, fixedTopK = false }: { settings: QuerySettings; onChange: (settings: QuerySettings) => void; capabilities: EnvironmentCapabilities | null; disabled?: boolean; fixedTopK?: boolean }) {
    return <fieldset className="space-y-5" disabled={disabled}>
        <label className="block"><span className="field-label">Retrieval mode</span><select className="field" value={settings.mode} onChange={e => onChange({ ...settings, mode: e.target.value === "hybrid" ? "hybrid" : "vector" })}><option value="vector">Vector similarity</option><option value="hybrid" disabled={!capabilities?.hybrid}>Hybrid · reciprocal-rank fusion</option></select></label>
        {fixedTopK ? <p className="text-xs text-muted-foreground">Top K: <strong>5</strong> · fixed for Recall@5 / MRR@5</p> : <div><label htmlFor="live-topk" className="field-label">Top K<span className="mono">{settings.topK}</span></label><Slider id="live-topk" aria-label="Top K" min={1} max={20} step={1} value={[settings.topK]} disabled={disabled} onValueChange={([topK]) => onChange({ ...settings, topK })} /></div>}
        <div><label htmlFor="live-threshold" className="field-label">Minimum relevance<span className="mono">{settings.minRelevance.toFixed(2)}</span></label><Slider id="live-threshold" aria-label="Minimum relevance" min={0} max={1} step={0.05} value={[settings.minRelevance]} disabled={disabled} onValueChange={([minRelevance]) => onChange({ ...settings, minRelevance })} /><p className="mt-2 text-[10px] leading-5 text-muted-foreground">Threshold on normalized cosine similarity, (cosine + 1) / 2. This is not a probability.</p></div>
        <div className="flex items-center justify-between gap-3 border-t pt-4"><div><label htmlFor="live-rerank" className="text-xs font-medium">HTTP reranker</label><p className="mt-1 text-[10px] text-muted-foreground">{capabilities?.reranker ? "Uses your configured endpoint" : "Configure an endpoint to enable"}</p></div><Switch id="live-rerank" checked={settings.reranker && !!capabilities?.reranker} disabled={disabled || !capabilities?.reranker} onCheckedChange={reranker => onChange({ ...settings, reranker })} /></div>
        <div className="flex items-center justify-between gap-3"><label htmlFor="live-neighbors" className="text-xs font-medium">Include neighboring chunks</label><Switch id="live-neighbors" checked={settings.neighbors} disabled={disabled} onCheckedChange={neighbors => onChange({ ...settings, neighbors })} /></div>
        <label className="block"><span className="field-label">Max context tokens<Hint text="Context budgeting uses ceil(characters / 4) per chunk. Model-reported output usage is shown when available." /></span><input className="field mono" required type="number" min={128} max={65536} value={settings.maxContextTokens} onChange={e => onChange({ ...settings, maxContextTokens: Number(e.target.value) })} /></label>
    </fieldset>;
}
