import { readFile, writeFile } from "node:fs/promises";

// Run from Rag.Playground. Root artifacts are authoritative; never edit their UI copies.
const files = [
  ["../../compose.client.yml", "public/compose.client.yml"],
  ["../../evals/results/latest.json", "lib/evaluation/fixtures/latest.json"],
  ["../../evals/Rag.Evals/Data/golden.json", "lib/evaluation/fixtures/golden.json"],
];
for (const [source, target] of files) {
  const expected = await readFile(source);
  if (process.argv.includes("--write")) await writeFile(target, expected);
  else if (!(await readFile(target)).equals(expected)) throw new Error(`${target} is stale; run npm run fixtures:write`);
}
