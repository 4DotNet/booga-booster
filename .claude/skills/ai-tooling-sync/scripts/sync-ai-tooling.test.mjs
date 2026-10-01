// node --test .claude/skills/ai-tooling-sync/scripts/sync-ai-tooling.test.mjs
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { frontmatterValue, parseTools, run, splitFrontmatter } from "./sync-ai-tooling.mjs";

const SERVERS = { primeng: { type: "stdio", command: "npx", args: ["-y", "@primeng/mcp"] } };

function repo(files) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "ai-sync-"));
  const all = {
    ".mcp.json": JSON.stringify({ mcpServers: SERVERS }),
    "src/.mcp.json": JSON.stringify({ servers: SERVERS }, null, 2) + "\n",
    "CLAUDE.md": "primeng",
    ".github/copilot-instructions.md": "primeng",
    ...files,
  };
  for (const [rel, text] of Object.entries(all)) {
    if (text === null) continue;
    fs.mkdirSync(path.dirname(path.join(root, rel)), { recursive: true });
    fs.writeFileSync(path.join(root, rel), text);
  }
  return root;
}

const errors = (result) => result.findings.filter((f) => f.severity === "error");
const read = (root, rel) => fs.readFileSync(path.join(root, rel), "utf8");

test("a clean repo has no findings", () => {
  assert.deepEqual(run({ root: repo({}) }).findings, []);
});

test("a command without a prompt is reported and --fix generates it", () => {
  const root = repo({ ".claude/commands/opsx/apply.md": "---\nname: x\ndescription: Do it\n---\n\nBody\n" });
  assert.match(errors(run({ root }))[0].message, /missing Copilot prompt/);

  run({ root, fix: true });
  assert.equal(read(root, ".github/prompts/opsx-apply.prompt.md"), "---\ndescription: Do it\n---\n\nBody\n");
  assert.deepEqual(errors(run({ root })), []);
});

test("--fix keeps Copilot-only prompt keys and ignores line endings", () => {
  const root = repo({
    ".claude/commands/opsx/apply.md": "---\r\ndescription: New\r\n---\r\n\r\nBody\r\n",
    ".github/prompts/opsx-apply.prompt.md": "---\ndescription: Old\nagent: agent\n---\n\nBody\n",
  });
  assert.match(errors(run({ root }))[0].message, /description differs/);
  run({ root, fix: true });
  assert.equal(read(root, ".github/prompts/opsx-apply.prompt.md"), "---\ndescription: New\nagent: agent\n---\n\nBody\n");
});

test("an orphan prompt is an error that --fix does not delete", () => {
  const root = repo({ ".github/prompts/ns-orphan.prompt.md": "---\ndescription: x\n---\n" });
  run({ root, fix: true });
  assert.match(errors(run({ root }))[0].message, /no Claude command/);
  assert.ok(fs.existsSync(path.join(root, ".github/prompts/ns-orphan.prompt.md")));
});

test("the Visual Studio MCP mirror is regenerated from the root", () => {
  const root = repo({ "src/.mcp.json": null });
  assert.match(errors(run({ root }))[0].message, /missing Visual Studio mirror/);
  run({ root, fix: true });
  assert.deepEqual(JSON.parse(read(root, "src/.mcp.json")), { servers: SERVERS });
});

test("agents must grant each MCP server in both vocabularies", () => {
  const agent = (tools) => ({ ".claude/agents/a.md": `---\nname: a\ndescription: d\ntools: ${tools}\n---\n` });
  assert.deepEqual(errors(run({ root: repo(agent("Read, mcp__primeng__search, read, primeng/*")) })), []);
  assert.match(errors(run({ root: repo(agent("Read, mcp__primeng__search")) }))[0].message, /no primeng\/\* entry to Copilot/);
  assert.match(errors(run({ root: repo(agent("Read, primeng/*")) }))[0].message, /no mcp__primeng__<tool> entries/);
});

test("agents referencing a personal plugin's server get pointed at the project server", () => {
  const root = repo({
    ".claude/agents/a.md": "---\nname: a\ndescription: d\ntools: mcp__plugin_ui_primeng__search, primeng/*\n---\n",
  });
  assert.ok(errors(run({ root })).some((f) => /use the project server "primeng"/.test(f.message)));
});

test("skill mirrors and plugin twins are re-copied, never invented", () => {
  const skill = "---\nname: s\ndescription: d\n---\nnew\n";
  const root = repo({
    ".claude/skills/s/SKILL.md": skill,
    ".github/skills/s/SKILL.md": "---\nname: s\ndescription: d\n---\nold\n",
    ".github/skills/s/stale.md": "gone",
    ".github/skills/only-here/SKILL.md": "x",
  });
  run({ root, fix: true });
  assert.equal(read(root, ".github/skills/s/SKILL.md"), skill);
  assert.ok(!fs.existsSync(path.join(root, ".github/skills/s/stale.md")));
  assert.match(errors(run({ root }))[0].message, /exists only under .github\/skills/);
});

test("a skill whose name does not match its folder is an error", () => {
  const root = repo({ ".claude/skills/s/SKILL.md": "---\nname: other\ndescription: d\n---\n" });
  assert.match(errors(run({ root }))[0].message, /name: is "other"/);
});

test("instruction files are warned about unnamed assets", () => {
  const root = repo({ ".github/copilot-instructions.md": "nothing" });
  const [finding] = run({ root }).findings;
  assert.equal(finding.severity, "warning");
  assert.match(finding.message, /MCP server "primeng"/);
});

test("frontmatter helpers read folded scalars and tool lists", () => {
  const { frontmatter, body } = splitFrontmatter("---\nname: a\ndescription: >-\n  one\n  two\ntools: Read, 'x/*'\n---\nbody");
  assert.equal(frontmatterValue(frontmatter, "description"), "one two");
  assert.deepEqual(parseTools(frontmatterValue(frontmatter, "tools")), ["Read", "x/*"]);
  assert.equal(body, "body");
});
