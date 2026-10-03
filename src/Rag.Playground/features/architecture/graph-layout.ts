import type { DiagramNode } from "./architecture-data";
export const nodeWidth = 158;
export const nodeHeight = 78;
export function connectorPath(from: DiagramNode, to: DiagramNode) {
    const dx = to.x - from.x;
    const dy = to.y - from.y;
    const edgeOffset = (x: number, y: number) => {
        const scale = Math.min(
            (nodeWidth / 2 + 2) / Math.max(Math.abs(x), 0.001),
            (nodeHeight / 2 + 2) / Math.max(Math.abs(y), 0.001),
        );
        return [x * scale, y * scale] as const;
    };
    const [fromOffsetX, fromOffsetY] = edgeOffset(dx, dy);
    const [toOffsetX, toOffsetY] = edgeOffset(-dx, -dy);
    const fromX = from.x + fromOffsetX;
    const fromY = from.y + fromOffsetY;
    const toX = to.x + toOffsetX;
    const toY = to.y + toOffsetY;
    const bendX = (fromX + toX) / 2;
    return `M ${fromX} ${fromY} C ${bendX} ${fromY}, ${bendX} ${toY}, ${toX} ${toY}`;
}
