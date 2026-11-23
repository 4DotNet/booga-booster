// node --test .claude/skills/ai-tooling-sync/scripts/sync-ai-tooling.test.mjs
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { test } from "node:test";
import { copilotTools, frontmatterValue, parseTools, run, splitFrontmatter } from "./sync-ai-tooling.mjs";

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
  const agentErrors = (root) => errors(run({ root })).filter((f) => f.area === "agents");
  assert.deepEqual(agentErrors(repo(agent("Read, mcp__primeng__search, read, primeng/*"))), []);
  assert.match(agentErrors(repo(agent("Read, mcp__primeng__search")))[0].message, /no primeng\/\* entry to Copilot/);
  assert.match(agentErrors(repo(agent("Read, primeng/*")))[0].message, /no mcp__primeng__<tool> entries/);
});

test("every agent gets a generated .github/agents twin in Copilot's vocabulary", () => {
  const root = repo({
    ".claude/agents/a.md":
      "---\nname: a\ndescription: >-\n  Does a\n  thing.\ntools: Read, Write, Grep, Bash, mcp__primeng__search, read, edit, primeng/*\nmodel: sonnet\n---\n\nYou are a.\n",
  });
  assert.match(errors(run({ root }))[0].message, /missing Copilot mirror of .claude\/agents\/a.md/);

  run({ root, fix: true });
  assert.equal(
    read(root, ".github/agents/a.agent.md"),
    '---\nname: a\ndescription: >-\n  Does a\n  thing.\ntools: ["read", "edit", "search", "execute", "primeng/*"]\n---\n\n' +
      "<!-- Generated from .claude/agents/a.md by the ai-tooling-sync skill. Edit that file, then run sync-ai-tooling.mjs --fix. -->\n\nYou are a.\n",
  );
  assert.deepEqual(errors(run({ root })), []);

  fs.writeFileSync(path.join(root, ".github/agents/a.agent.md"), "hand edit");
  assert.match(errors(run({ root }))[0].message, /out of step/);
});

test("a Copilot-only agent is an error that --fix does not delete", () => {
  const root = repo({ ".github/agents/orphan.agent.md": "---\nname: orphan\ndescription: d\n---\n" });
  run({ root, fix: true });
  assert.match(errors(run({ root }))[0].message, /has no Claude twin/);
  assert.ok(fs.existsSync(path.join(root, ".github/agents/orphan.agent.md")));
});

test("agents referencing a personal plugin's server get pointed at the project server", () => {
  const root = repo({
    ".claude/agents/a.md": "---\nname: a\ndescription: d\ntools: mcp__plugin_ui_primeng__search, primeng/*\n---\n",
  });
  assert.ok(errors(run({ root })).some((f) => /use the project server "primeng"/.test(f.message)));
});

test("every skill is mirrored to .github/skills; Copilot-only skills are reported, not deleted", () => {
  const skill = "---\nname: s\ndescription: d\n---\nnew\n";
  const fresh = "---\nname: t\ndescription: d\n---\nbody\n";
  const root = repo({
    ".claude/skills/s/SKILL.md": skill,
    ".claude/skills/t/SKILL.md": fresh,
    ".claude/skills/t/scripts/x.mjs": "code",
    ".github/skills/s/SKILL.md": "---\nname: s\ndescription: d\n---\nold\n",
    ".github/skills/s/stale.md": "gone",
    ".github/skills/only-here/SKILL.md": "x",
  });
  assert.ok(errors(run({ root })).some((f) => /missing Copilot mirror of .claude\/skills\/t/.test(f.message)));
  run({ root, fix: true });
  assert.equal(read(root, ".github/skills/s/SKILL.md"), skill);
  assert.ok(!fs.existsSync(path.join(root, ".github/skills/s/stale.md")));
  assert.equal(read(root, ".github/skills/t/SKILL.md"), fresh);
  assert.equal(read(root, ".github/skills/t/scripts/x.mjs"), "code");
  const remaining = errors(run({ root }));
  assert.equal(remaining.length, 1);
  assert.match(remaining[0].message, /exists only under .github\/skills/);
  assert.ok(fs.existsSync(path.join(root, ".github/skills/only-here/SKILL.md")));
});

test("a skill whose name does not match its folder is an error", () => {
  const root = repo({ ".claude/skills/s/SKILL.md": "---\nname: other\ndescription: d\n---\n" });
  assert.match(errors(run({ root }))[0].message, /name: is "other"/);
});

test("skills that VS Code would silently skip are errors", () => {
  const long = repo({ ".claude/skills/s/SKILL.md": `---\nname: s\ndescription: ${"x".repeat(1025)}\n---\n` });
  assert.ok(errors(run({ root: long })).some((f) => /1025 characters/.test(f.message)));
  const caps = repo({ ".claude/skills/Bad_Name/SKILL.md": "---\nname: Bad_Name\ndescription: d\n---\n" });
  assert.ok(errors(run({ root: caps })).some((f) => /lowercase letters/.test(f.message)));
});

test("copilotTools maps Claude built-ins, drops mcp__ names and de-duplicates", () => {
  assert.deepEqual(copilotTools(["Read", "Write", "Edit", "Glob", "Grep", "PowerShell", "Bash", "mcp__x__y", "read", "x/*"]),
    ["read", "edit", "search", "execute", "x/*"]);
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
