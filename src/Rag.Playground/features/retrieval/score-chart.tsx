"use client";
import { ArrowDownUp } from "lucide-react";
import {
    Bar,
    BarChart,
    CartesianGrid,
    Cell,
    ReferenceLine,
    ResponsiveContainer,
    Tooltip,
    XAxis,
    YAxis,
} from "recharts";
import { Panel } from "@/components/shared/panel";
import { Hint } from "@/components/shared/hint";
import { ChartFrame } from "@/components/charts/chart-frame";
import { STRATEGY_LABELS } from "@/lib/constants";
import type { RetrievedChunk, RetrievalConfig } from "@/lib/contracts";
export function RetrievalScoreChart({
    candidates,
    config,
    selectedId,
    onSelect,
}: {
    candidates: RetrievedChunk[];
    config: RetrievalConfig;
    selectedId?: string;
    onSelect: (id: string) => void;
}) {
    const chartData = candidates.map((chunk) => ({
        name: `#${chunk.index}`,
        score: chunk.score,
        id: chunk.id,
        inContext: chunk.inContext,
    }));
    return (
        <Panel
            title="Retrieval Score Distribution"
            icon={<ArrowDownUp size={15} className="text-primary" />}
            action={
                <Hint text="Scores are deterministic demo values, not calibrated probabilities. The threshold applies to the final score: reranker score when enabled, otherwise retrieval score." />
            }
        >
            <ChartFrame
                title={`${config.reranker ? "Reranked" : "Retrieved"} candidates`}
                description={`${STRATEGY_LABELS[config.strategy]} · ${config.mode} · illustrative scores · select a bar to inspect its evidence`}
                height={235}
                columns={["Chunk", "Score", "Context"]}
                rows={candidates.map((chunk) => [
                    `Chunk ${chunk.index}`,
                    chunk.score.toFixed(3),
                    chunk.inContext
                        ? "Included"
                        : (chunk.exclusionReason ?? "Excluded"),
                ])}
            >
                <ResponsiveContainer width="100%" height="100%" minWidth={0}>
                    <BarChart
                        data={chartData}
                        margin={{
                            top: 20,
                            right: 15,
                            left: -25,
                            bottom: 2,
                        }}
                    >
                        <CartesianGrid vertical={false} strokeDasharray="3 3" />
                        <XAxis
                            dataKey="name"
                            axisLine={false}
                            tickLine={false}
                            dy={7}
                        />
                        <YAxis
                            domain={[0, 1]}
                            tickCount={6}
                            axisLine={false}
                            tickLine={false}
                        />
                        <Tooltip
                            cursor={{
                                fill: "var(--muted)",
                            }}
                            contentStyle={{
                                background: "var(--popover)",
                                border: "1px solid var(--border)",
                                borderRadius: 8,
                                fontSize: 11,
                                color: "var(--foreground)",
                            }}
                            formatter={(value) => [
                                Number(value).toFixed(3),
                                "Final score",
                            ]}
                        />
                        <ReferenceLine
                            y={config.minRelevance}
                            stroke="var(--warning)"
                            strokeDasharray="4 4"
                            label={{
                                value: `Threshold ${config.minRelevance.toFixed(2)}`,
                                position: "insideTopRight",
                                fill: "var(--warning)",
                                fontSize: 10,
                            }}
                        />
                        <Bar
                            isAnimationActive={false}
                            dataKey="score"
                            maxBarSize={45}
                            radius={[4, 4, 0, 0]}
                            onClick={(_, index) =>
                                onSelect(candidates[index].id)
                            }
                            cursor="pointer"
                        >
                            {chartData.map((item) => (
                                <Cell
                                    key={item.id}
                                    fill={
                                        item.id === selectedId
                                            ? "#6465e9"
                                            : item.inContext
                                              ? "#aaa8e5"
                                              : "#b7bac5"
                                    }
                                />
                            ))}
                        </Bar>
                    </BarChart>
                </ResponsiveContainer>
            </ChartFrame>
            <div className="mt-4 flex flex-wrap items-center justify-between gap-3 border-t pt-4 text-[10px] text-muted-foreground">
                <span className="flex items-center gap-1.5">
                    <span className="h-2 w-2 rounded-sm bg-primary" />
                    Selected candidate
                </span>
                <span>Click a bar or choose a candidate below.</span>
            </div>
        </Panel>
    );
}
