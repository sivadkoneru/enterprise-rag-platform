"use client";
import {
    Bar,
    BarChart,
    CartesianGrid,
    Cell,
    Line,
    LineChart,
    ResponsiveContainer,
    Scatter,
    ScatterChart,
    Tooltip,
    XAxis,
    YAxis,
    ZAxis,
} from "recharts";
import type { StrategyBenchmark } from "@/lib/contracts";
import { STRATEGY_COLORS, STRATEGY_LABELS } from "@/lib/constants";
import { ChartFrame } from "@/components/charts/chart-frame";
import { latencyProjection, percent } from "@/lib/evaluation/data";

const axis = { fontSize: 10, fill: "var(--muted-foreground)" };
const tooltip = {
    background: "var(--card)",
    border: "1px solid var(--border)",
    borderRadius: 8,
    fontSize: 11,
    color: "var(--foreground)",
};
export function BenchmarkBars({
    strategies,
    selected,
}: {
    strategies: StrategyBenchmark[];
    selected: string;
}) {
    const data = strategies.map((s) => ({
        ...s,
        label: STRATEGY_LABELS[s.strategy],
    }));
    return (
        <div className="grid min-w-0 gap-6 lg:grid-cols-3">
            {(
                [
                    {
                        key: "recall5",
                        title: "Evidence recovered",
                        unit: "Recall @5",
                        max: 1,
                    },
                    {
                        key: "indexEmbedCalls",
                        title: "Indexing effort",
                        unit: "Embedding calls",
                        max: undefined,
                    },
                    {
                        key: "averageContextTokens",
                        title: "Context footprint",
                        unit: "Approx. tokens / query",
                        max: undefined,
                    },
                ] as const
            ).map((metric) => (
                <ChartFrame
                    key={metric.key}
                    title={metric.title}
                    description={metric.unit}
                    columns={["Strategy", metric.unit]}
                    rows={data.map((s) => [
                        s.label,
                        metric.key === "recall5"
                            ? percent(s[metric.key])
                            : s[metric.key],
                    ])}
                    height={180}
                >
                    <ResponsiveContainer
                        width="100%"
                        height="100%"
                        minWidth={0}
                    >
                        <BarChart
                            data={data}
                            margin={{ top: 6, right: 8, left: -25, bottom: 22 }}
                        >
                            <CartesianGrid
                                stroke="var(--border)"
                                vertical={false}
                            />
                            <XAxis
                                dataKey="label"
                                tick={axis}
                                interval={0}
                                angle={-16}
                                textAnchor="end"
                                axisLine={false}
                                tickLine={false}
                            />
                            <YAxis
                                tick={axis}
                                domain={[0, metric.max ?? "auto"]}
                                tickFormatter={(v) =>
                                    metric.key === "recall5"
                                        ? `${Math.round(Number(v) * 100)}%`
                                        : String(v)
                                }
                                axisLine={false}
                                tickLine={false}
                            />
                            <Tooltip
                                contentStyle={tooltip}
                                cursor={{ fill: "var(--muted)", opacity: 0.3 }}
                            />
                            <Bar
                                isAnimationActive={false}
                                dataKey={metric.key}
                                name={metric.unit}
                                radius={[4, 4, 0, 0]}
                                maxBarSize={42}
                            >
                                {data.map((s) => (
                                    <Cell
                                        key={s.strategy}
                                        fill={STRATEGY_COLORS[s.strategy]}
                                        opacity={
                                            s.strategy === selected ? 1 : 0.5
                                        }
                                    />
                                ))}
                            </Bar>
                        </BarChart>
                    </ResponsiveContainer>
                </ChartFrame>
            ))}
        </div>
    );
}

export function TradeoffScatter({
    strategies,
    resource,
}: {
    strategies: StrategyBenchmark[];
    resource: "indexEmbedCalls" | "averageContextTokens";
}) {
    const label =
        resource === "indexEmbedCalls"
            ? "Index embedding calls"
            : "Average context tokens";
    return (
        <ChartFrame
            title="Quality × resources"
            description="Upper left: more evidence with less resource use. Published values."
            columns={["Strategy", label, "Recall @5"]}
            rows={strategies.map((s) => [
                STRATEGY_LABELS[s.strategy],
                s[resource],
                percent(s.recall5),
            ])}
        >
            <ResponsiveContainer width="100%" height="100%" minWidth={0}>
                <ScatterChart
                    margin={{ top: 14, right: 24, bottom: 22, left: 0 }}
                >
                    <CartesianGrid
                        stroke="var(--border)"
                        strokeDasharray="3 3"
                    />
                    <XAxis
                        type="number"
                        dataKey={resource}
                        name={label}
                        tick={axis}
                        label={{
                            value: label,
                            position: "bottom",
                            fill: "var(--muted-foreground)",
                            fontSize: 10,
                        }}
                        domain={[0, "auto"]}
                    />
                    <YAxis
                        type="number"
                        dataKey="recall5"
                        name="Recall @5"
                        tick={axis}
                        tickFormatter={(v) => `${Math.round(Number(v) * 100)}%`}
                        domain={[0, 1]}
                    />
                    <ZAxis range={[130, 130]} />
                    <Tooltip
                        contentStyle={tooltip}
                        cursor={{ strokeDasharray: "3 3" }}
                    />
                    {strategies.map((s) => (
                        <Scatter
                            isAnimationActive={false}
                            key={s.strategy}
                            name={STRATEGY_LABELS[s.strategy]}
                            data={[s]}
                            fill={STRATEGY_COLORS[s.strategy]}
                        />
                    ))}
                </ScatterChart>
            </ResponsiveContainer>
        </ChartFrame>
    );
}

export function LatencyChart({
    strategies,
    topK,
}: {
    strategies: StrategyBenchmark[];
    topK: number;
}) {
    const data = Array.from({ length: topK }, (_, index) => {
        const k = index + 1;
        return {
            topK: k,
            ...Object.fromEntries(
                strategies.map((s) => [s.strategy, latencyProjection(s, k)]),
            ),
        };
    });
    return (
        <ChartFrame
            title="Latency × retrieval depth"
            description="Illustrative model in ms; no measured latency or API calls."
            columns={[
                "Top K",
                ...strategies.map((s) => STRATEGY_LABELS[s.strategy]),
            ]}
            rows={data.map((row) => [
                row.topK,
                ...strategies.map((s) => latencyProjection(s, row.topK)),
            ])}
        >
            <ResponsiveContainer width="100%" height="100%" minWidth={0}>
                <LineChart
                    data={data}
                    margin={{ top: 14, right: 18, bottom: 22, left: -12 }}
                >
                    <CartesianGrid
                        stroke="var(--border)"
                        strokeDasharray="3 3"
                    />
                    <XAxis
                        dataKey="topK"
                        tick={axis}
                        label={{
                            value: "Top K",
                            position: "bottom",
                            fontSize: 10,
                            fill: "var(--muted-foreground)",
                        }}
                        allowDecimals={false}
                    />
                    <YAxis tick={axis} unit=" ms" />
                    <Tooltip contentStyle={tooltip} />
                    {strategies.map((s) => (
                        <Line
                            isAnimationActive={false}
                            key={s.strategy}
                            dataKey={s.strategy}
                            name={STRATEGY_LABELS[s.strategy]}
                            stroke={STRATEGY_COLORS[s.strategy]}
                            strokeWidth={2}
                            dot={false}
                        />
                    ))}
                </LineChart>
            </ResponsiveContainer>
        </ChartFrame>
    );
}
