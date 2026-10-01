"use client";
import { ChevronDown, SlidersHorizontal } from "lucide-react";
import type { RetrievalConfig } from "@/lib/contracts";
import { STRATEGY_LABELS } from "@/lib/constants";
import { Panel } from "@/components/shared/panel";
import { Hint } from "@/components/shared/hint";
import { Switch } from "@/components/ui/switch";
import { Slider } from "@/components/ui/slider";

export function RetrievalSettings({
    config,
    onChange,
    disabled = false,
    compact = false,
}: {
    config: RetrievalConfig;
    onChange: (config: RetrievalConfig) => void;
    disabled?: boolean;
    compact?: boolean;
}) {
    function update<K extends keyof RetrievalConfig>(
        key: K,
        value: RetrievalConfig[K],
    ) {
        onChange({ ...config, [key]: value });
    }
    return (
        <Panel
            title="Retrieval configuration"
            icon={
                <SlidersHorizontal
                    size={14}
                    className="text-muted-foreground"
                />
            }
            action={
                <Hint text="All settings select a deterministic demo profile. No embedding model or external search service is called." />
            }
        >
            <fieldset
                disabled={disabled}
                className="space-y-5 disabled:opacity-60"
            >
                <div className="grid grid-cols-2 gap-4">
                    <div>
                        <label className="field-label" htmlFor="strategy">
                            Chunking strategy
                        </label>
                        <select
                            id="strategy"
                            className="field"
                            value={config.strategy}
                            onChange={(e) =>
                                update(
                                    "strategy",
                                    e.target
                                        .value as RetrievalConfig["strategy"],
                                )
                            }
                        >
                            {Object.entries(STRATEGY_LABELS).map(
                                ([value, label]) => (
                                    <option key={value} value={value}>
                                        {label}
                                    </option>
                                ),
                            )}
                        </select>
                    </div>
                    <div>
                        <label className="field-label" htmlFor="retrieval-mode">
                            Retrieval mode
                        </label>
                        <select
                            id="retrieval-mode"
                            className="field"
                            value={config.mode}
                            onChange={(e) =>
                                update(
                                    "mode",
                                    e.target.value as RetrievalConfig["mode"],
                                )
                            }
                        >
                            <option value="vector">Vector</option>
                            <option value="hybrid">Hybrid</option>
                        </select>
                    </div>
                </div>
                <div>
                    <label className="field-label" htmlFor="top-k">
                        Top K{" "}
                        <span className="mono rounded border bg-muted px-2 py-0.5 text-[11px]">
                            {config.topK}
                        </span>
                    </label>
                    <Slider
                        id="top-k"
                        aria-label="Top K"
                        min={1}
                        max={20}
                        step={1}
                        value={[config.topK]}
                        onValueChange={([value]) => update("topK", value)}
                        disabled={disabled}
                    />
                    <div className="mt-2 flex justify-between text-[10px] text-muted-foreground">
                        <span>1 chunk</span>
                        <span>20 chunks</span>
                    </div>
                </div>
                <div>
                    <label className="field-label" htmlFor="min-relevance">
                        Minimum relevance{" "}
                        <span className="mono rounded border bg-muted px-2 py-0.5 text-[11px]">
                            {config.minRelevance.toFixed(2)}
                        </span>
                    </label>
                    <Slider
                        id="min-relevance"
                        aria-label="Minimum relevance"
                        min={0}
                        max={1}
                        step={0.05}
                        value={[config.minRelevance]}
                        onValueChange={([value]) =>
                            update("minRelevance", Number(value.toFixed(2)))
                        }
                        disabled={disabled}
                    />
                    <div className="mt-2 flex justify-between text-[10px] text-muted-foreground">
                        <span>Broader context</span>
                        <span>Higher relevance</span>
                    </div>
                </div>
                <div className="space-y-4 border-t pt-4">
                    <div className="flex items-center justify-between">
                        <div>
                            <label
                                className="text-xs font-medium"
                                htmlFor="reranker"
                            >
                                Reranker
                            </label>
                            <p className="mt-1 text-[10px] text-muted-foreground">
                                Refine candidate order by relevance
                            </p>
                        </div>
                        <Switch
                            id="reranker"
                            checked={config.reranker}
                            onCheckedChange={(value) =>
                                update("reranker", value)
                            }
                            disabled={disabled}
                        />
                    </div>
                    <div className="flex items-center justify-between">
                        <div>
                            <label
                                className="text-xs font-medium"
                                htmlFor="neighbors"
                            >
                                Include neighboring chunks
                            </label>
                            <p className="mt-1 text-[10px] text-muted-foreground">
                                Preserve surrounding document context
                            </p>
                        </div>
                        <Switch
                            id="neighbors"
                            checked={config.neighbors}
                            onCheckedChange={(value) =>
                                update("neighbors", value)
                            }
                            disabled={disabled}
                        />
                    </div>
                </div>
                {!compact && (
                    <details className="border-t pt-4">
                        <summary className="flex list-none items-center justify-between text-xs font-medium text-muted-foreground">
                            Advanced Settings
                            <ChevronDown size={14} />
                        </summary>
                        <p className="mt-3 text-[10px] leading-5 text-muted-foreground">
                            Simulated indexing profile. Token counts use a
                            characters ÷ 4 estimate.
                        </p>
                        <div className="mt-3 grid grid-cols-2 gap-3">
                            <div>
                                <label
                                    className="field-label"
                                    htmlFor="chunk-size"
                                >
                                    Chunk size (chars)
                                </label>
                                <input
                                    id="chunk-size"
                                    className="field mono"
                                    type="number"
                                    min={200}
                                    max={1600}
                                    step={100}
                                    value={config.chunkSize}
                                    onChange={(e) =>
                                        update(
                                            "chunkSize",
                                            Number(e.target.value),
                                        )
                                    }
                                />
                            </div>
                            <div>
                                <label
                                    className="field-label"
                                    htmlFor="chunk-overlap"
                                >
                                    Overlap (chars)
                                </label>
                                <input
                                    id="chunk-overlap"
                                    className="field mono"
                                    type="number"
                                    min={0}
                                    max={config.chunkSize - 1}
                                    step={20}
                                    value={config.chunkOverlap}
                                    onChange={(e) =>
                                        update(
                                            "chunkOverlap",
                                            Number(e.target.value),
                                        )
                                    }
                                />
                            </div>
                            <div>
                                <label
                                    className="field-label"
                                    htmlFor="dimensions"
                                >
                                    Embedding dimensions
                                </label>
                                <select
                                    id="dimensions"
                                    className="field mono"
                                    value={config.embeddingDimensions}
                                    onChange={(e) =>
                                        update(
                                            "embeddingDimensions",
                                            Number(e.target.value),
                                        )
                                    }
                                >
                                    {[384, 768, 1536].map((n) => (
                                        <option key={n} value={n}>
                                            {n.toLocaleString()}
                                        </option>
                                    ))}
                                </select>
                            </div>
                            <div>
                                <label
                                    className="field-label"
                                    htmlFor="context-budget"
                                >
                                    Max context tokens
                                </label>
                                <input
                                    id="context-budget"
                                    className="field mono"
                                    type="number"
                                    min={128}
                                    max={8192}
                                    step={128}
                                    value={config.maxContextTokens}
                                    onChange={(e) =>
                                        update(
                                            "maxContextTokens",
                                            Number(e.target.value),
                                        )
                                    }
                                />
                            </div>
                        </div>
                    </details>
                )}
            </fieldset>
        </Panel>
    );
}
