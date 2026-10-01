#!/usr/bin/env node
// Keeps the Claude Code and GitHub Copilot AI tooling of this repo in step.
//
//   node .claude/skills/ai-tooling-sync/scripts/sync-ai-tooling.mjs          check, exit 1 on drift
//   node .claude/skills/ai-tooling-sync/scripts/sync-ai-tooling.mjs --fix    regenerate the mechanical mirrors
//   ... --root <dir>                                                         run against another checkout
//   ... --json                                                               machine-readable findings
//
// Canonical sources and the mirrors derived from them:
//   .claude/commands/<ns>/<name>.md  ->  .github/prompts/<ns>-<name>.prompt.md   (Copilot has no .claude/commands)
//   .mcp.json (mcpServers)           ->  src/.mcp.json (servers)                 (Visual Studio reads <SOLUTIONDIR>)
//   .mcp.json                        ->  plugins/*/.mcp.json entries             (same definition per server)
//   .claude/skills/<name>/           ->  .github/skills/<name>/                  (only where a twin already exists)
//   .claude/{agents,skills}/         ->  plugins/*/{agents,skills}/              (only where a twin already exists)
// Checked but never rewritten (they need judgement): agent tools: lines, plugin version bumps,
// and whether CLAUDE.md and .github/copilot-instructions.md both name every asset.
//
// Node built-ins only, so it runs anywhere the repo's other .mjs tooling runs.

import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const SOLUTION_DIR = "src";

export function run({ root, fix = false }) {
  const findings = [];
  const fixed = [];
  const ctx = { root, fix, findings, fixed };

  const servers = readRootServers(ctx);
  syncCommands(ctx);
  syncSolutionMcp(ctx, servers);
  syncPlugins(ctx, servers);
  syncSkillMirrors(ctx);
  checkAgents(ctx, servers);
  checkInstructions(ctx, servers);

  return { findings, fixed };
}

// ---------------------------------------------------------------- helpers

const abs = (ctx, rel) => path.join(ctx.root, rel);
const exists = (ctx, rel) => fs.existsSync(abs(ctx, rel));
const readText = (ctx, rel) => fs.readFileSync(abs(ctx, rel), "utf8");
const lf = (text) => text.replace(/\r\n/g, "\n");
const listDirs = (ctx, rel) =>
  exists(ctx, rel)
    ? fs.readdirSync(abs(ctx, rel), { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name).sort()
    : [];
const listFiles = (ctx, rel, suffix) =>
  exists(ctx, rel)
    ? fs.readdirSync(abs(ctx, rel), { withFileTypes: true }).filter((d) => d.isFile() && d.name.endsWith(suffix)).map((d) => d.name).sort()
    : [];

function report(ctx, severity, area, file, message) {
  ctx.findings.push({ severity, area, file, message });
}

// Writes text, keeping the target's existing line endings (LF for new files).
function writeText(ctx, rel, text) {
  const target = abs(ctx, rel);
  const crlf = fs.existsSync(target) && fs.readFileSync(target, "utf8").includes("\r\n");
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, crlf ? lf(text).replace(/\n/g, "\r\n") : lf(text));
  ctx.fixed.push(rel);
}

function filesUnder(ctx, rel) {
  const out = [];
  const walk = (dir) => {
    for (const entry of fs.readdirSync(abs(ctx, dir), { withFileTypes: true })) {
      const child = path.posix.join(dir, entry.name);
      if (entry.isDirectory()) walk(child);
      else out.push(path.posix.relative(rel, child));
    }
  };
  if (exists(ctx, rel)) walk(rel);
  return out.sort();
}

// Compares two directory trees ignoring line endings; returns a description of the first difference.
function treeDiff(ctx, sourceRel, mirrorRel) {
  const a = filesUnder(ctx, sourceRel);
  const b = filesUnder(ctx, mirrorRel);
  const missing = a.filter((f) => !b.includes(f));
  const extra = b.filter((f) => !a.includes(f));
  if (missing.length) return `missing ${missing.join(", ")}`;
  if (extra.length) return `extra ${extra.join(", ")}`;
  const changed = a.filter((f) => lf(readText(ctx, `${sourceRel}/${f}`)) !== lf(readText(ctx, `${mirrorRel}/${f}`)));
  return changed.length ? `content differs: ${changed.join(", ")}` : null;
}

function copyTree(ctx, sourceRel, mirrorRel) {
  for (const f of filesUnder(ctx, mirrorRel)) {
    if (!exists(ctx, `${sourceRel}/${f}`)) fs.rmSync(abs(ctx, `${mirrorRel}/${f}`));
  }
  for (const f of filesUnder(ctx, sourceRel)) {
    const src = lf(readText(ctx, `${sourceRel}/${f}`));
    const dst = `${mirrorRel}/${f}`;
    if (!exists(ctx, dst) || lf(readText(ctx, dst)) !== src) writeText(ctx, dst, src);
  }
}

export function splitFrontmatter(text) {
  const match = /^---\n([\s\S]*?)\n---\n?([\s\S]*)$/.exec(lf(text));
  return match ? { frontmatter: match[1], body: match[2] } : { frontmatter: "", body: lf(text) };
}

// Reads a single-line or folded (>- / |) YAML scalar; enough for agent/command frontmatter.
export function frontmatterValue(frontmatter, key) {
  const lines = frontmatter.split("\n");
  const i = lines.findIndex((l) => l.startsWith(`${key}:`));
  if (i < 0) return undefined;
  const inline = lines[i].slice(key.length + 1).trim();
  if (!/^[>|]-?$/.test(inline)) return inline.replace(/^(["'])(.*)\1$/, "$2");
  const block = [];
  for (const line of lines.slice(i + 1)) {
    if (!/^\s+/.test(line)) break;
    block.push(line.trim());
  }
  return block.join(" ");
}

const sameJson = (a, b) => JSON.stringify(a) === JSON.stringify(b);

function readJson(ctx, rel) {
  try {
    return JSON.parse(readText(ctx, rel));
  } catch (error) {
    report(ctx, "error", "mcp", rel, `not valid JSON: ${error.message}`);
    return undefined;
  }
}

// ---------------------------------------------------------------- commands -> prompts

function promptFor(command) {
  return `.github/prompts/${command.ns}-${command.name}.prompt.md`;
}

function syncCommands(ctx) {
  const commands = [];
  for (const ns of listDirs(ctx, ".claude/commands")) {
    for (const file of listFiles(ctx, `.claude/commands/${ns}`, ".md")) {
      commands.push({ ns, name: file.replace(/\.md$/, ""), rel: `.claude/commands/${ns}/${file}` });
    }
  }
  for (const file of listFiles(ctx, ".claude/commands", ".md")) {
    report(ctx, "warning", "commands", `.claude/commands/${file}`,
      "command has no namespace folder; move it to .claude/commands/<ns>/ so its Copilot prompt can be named <ns>-<name>");
  }

  const expected = new Set(commands.map(promptFor));
  for (const command of commands) {
    const source = splitFrontmatter(readText(ctx, command.rel));
    const description = frontmatterValue(source.frontmatter, "description");
    if (!description) report(ctx, "error", "commands", command.rel, "command has no description: in its frontmatter");

    const mirrorRel = promptFor(command);
    const existing = exists(ctx, mirrorRel) ? splitFrontmatter(readText(ctx, mirrorRel)) : undefined;
    // Keep any Copilot-only keys (agent:, model:, tools:) the prompt already carries.
    const extraKeys = existing
      ? existing.frontmatter.split("\n").filter((l) => l.trim() && !l.startsWith("description:"))
      : [];
    const wanted = `---\ndescription: ${description ?? ""}\n${extraKeys.map((l) => `${l}\n`).join("")}---\n\n${source.body.replace(/^\n+/, "")}`;

    if (!existing) {
      report(ctx, "error", "commands", mirrorRel, `missing Copilot prompt for ${command.rel}`);
    } else if (lf(readText(ctx, mirrorRel)) !== wanted) {
      const why = frontmatterValue(existing.frontmatter, "description") !== description ? "description" : "body";
      report(ctx, "error", "commands", mirrorRel, `${why} differs from ${command.rel}`);
    } else {
      continue;
    }
    if (ctx.fix) writeText(ctx, mirrorRel, wanted);
  }

  for (const file of listFiles(ctx, ".github/prompts", ".prompt.md")) {
    const rel = `.github/prompts/${file}`;
    if (!expected.has(rel)) {
      report(ctx, "error", "commands", rel,
        "Copilot prompt has no Claude command; add .claude/commands/<ns>/<name>.md (the canonical side), or delete the prompt");
    }
  }
}

// ---------------------------------------------------------------- MCP servers

function readRootServers(ctx) {
  if (!exists(ctx, ".mcp.json")) {
    report(ctx, "error", "mcp", ".mcp.json", "the canonical MCP configuration is missing");
    return {};
  }
  const config = readJson(ctx, ".mcp.json");
  if (config && !config.mcpServers) report(ctx, "error", "mcp", ".mcp.json", 'top-level key must be "mcpServers"');
  return config?.mcpServers ?? {};
}

// Visual Studio only discovers <SOLUTIONDIR>\.mcp.json, and wants the "servers" key.
function syncSolutionMcp(ctx, servers) {
  const rel = `${SOLUTION_DIR}/.mcp.json`;
  const wanted = { servers };
  const current = exists(ctx, rel) ? readJson(ctx, rel) : undefined;
  if (current && sameJson(current, wanted)) return;
  report(ctx, "error", "mcp", rel,
    current ? "differs from the root .mcp.json" : "missing Visual Studio mirror of the root .mcp.json");
  if (ctx.fix) writeText(ctx, rel, `${JSON.stringify(wanted, null, 2)}\n`);
}

// ---------------------------------------------------------------- plugins

function syncPlugins(ctx, servers) {
  for (const plugin of listDirs(ctx, "plugins")) {
    const base = `plugins/${plugin}`;
    const manifestRel = `${base}/.claude-plugin/plugin.json`;
    if (!exists(ctx, manifestRel)) continue;
    const manifest = readJson(ctx, manifestRel) ?? {};

    for (const entry of [...(manifest.agents ?? []), ...(manifest.skills ?? [])]) {
      if (!exists(ctx, path.posix.join(base, entry))) {
        report(ctx, "error", "plugin", manifestRel, `lists ${entry}, which does not exist`);
      }
    }

    for (const name of listDirs(ctx, `${base}/skills`)) {
      const listed = (manifest.skills ?? []).some((s) => path.posix.normalize(s) === `skills/${name}`);
      if (!listed) report(ctx, "error", "plugin", manifestRel, `skills/${name} is bundled but not listed in "skills"`);
      syncTwin(ctx, "plugin", `.claude/skills/${name}`, `${base}/skills/${name}`, true);
    }
    for (const file of listFiles(ctx, `${base}/agents`, ".md")) {
      const listed = (manifest.agents ?? []).some((a) => path.posix.normalize(a) === `agents/${file}`);
      if (!listed) report(ctx, "error", "plugin", manifestRel, `agents/${file} is bundled but not listed in "agents"`);
      syncTwin(ctx, "plugin", `.claude/agents/${file}`, `${base}/agents/${file}`, false);
    }

    const mcpRel = `${base}/.mcp.json`;
    if (exists(ctx, mcpRel)) {
      const config = readJson(ctx, mcpRel);
      let dirty = false;
      for (const [name, definition] of Object.entries(config?.mcpServers ?? {})) {
        if (!servers[name]) {
          report(ctx, "warning", "plugin", mcpRel, `bundles MCP server "${name}", which the repo itself does not use`);
        } else if (!sameJson(servers[name], definition)) {
          report(ctx, "error", "plugin", mcpRel, `MCP server "${name}" differs from the root .mcp.json`);
          config.mcpServers[name] = servers[name];
          dirty = true;
        }
      }
      if (dirty && ctx.fix) writeText(ctx, mcpRel, `${JSON.stringify(config, null, 2)}\n`);
    }

    checkPluginVersion(ctx, base, manifestRel, manifest);
  }
}

function syncTwin(ctx, area, sourceRel, mirrorRel, isDir) {
  if (!exists(ctx, sourceRel)) {
    report(ctx, "error", area, mirrorRel, `mirror of ${sourceRel}, which no longer exists`);
    return;
  }
  const difference = isDir
    ? treeDiff(ctx, sourceRel, mirrorRel)
    : lf(readText(ctx, sourceRel)) === lf(readText(ctx, mirrorRel)) ? null : "content differs";
  if (!difference) return;
  report(ctx, "error", area, mirrorRel, `out of step with ${sourceRel} (${difference})`);
  if (ctx.fix) {
    if (isDir) copyTree(ctx, sourceRel, mirrorRel);
    else writeText(ctx, mirrorRel, lf(readText(ctx, sourceRel)));
  }
}

// A plugin whose content changed since HEAD must ship under a new version.
function checkPluginVersion(ctx, base, manifestRel, manifest) {
  const git = (...args) => execFileSync("git", ["-C", ctx.root, ...args], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
  let changed;
  let headVersion;
  try {
    changed = git("status", "--porcelain", "--", base, `:!${manifestRel}`).trim() !== "";
    headVersion = JSON.parse(git("show", `HEAD:${manifestRel}`)).version;
  } catch {
    return; // not a git checkout, or the plugin is new: nothing to compare against
  }
  if (changed && headVersion === manifest.version) {
    report(ctx, "error", "plugin", manifestRel,
      `plugin content changed but "version" is still ${manifest.version}; bump it (patch for fixes, minor for new agents/skills)`);
  }
}

// ---------------------------------------------------------------- .github/skills mirrors

function syncSkillMirrors(ctx) {
  for (const name of listDirs(ctx, ".github/skills")) {
    if (!exists(ctx, `.claude/skills/${name}`)) {
      report(ctx, "error", "skills", `.github/skills/${name}`,
        "skill exists only under .github/skills, so Claude Code cannot see it; move it to .claude/skills (both tools read that)");
      continue;
    }
    syncTwin(ctx, "skills", `.claude/skills/${name}`, `.github/skills/${name}`, true);
  }
  for (const name of listDirs(ctx, ".claude/skills")) {
    if (!exists(ctx, `.claude/skills/${name}/SKILL.md`)) {
      report(ctx, "error", "skills", `.claude/skills/${name}`, "skill folder has no SKILL.md");
      continue;
    }
    const { frontmatter } = splitFrontmatter(readText(ctx, `.claude/skills/${name}/SKILL.md`));
    const declared = frontmatterValue(frontmatter, "name");
    if (declared !== name) {
      report(ctx, "error", "skills", `.claude/skills/${name}/SKILL.md`,
        `name: is "${declared ?? ""}" but the folder is "${name}"; Copilot and VS Code require them to match`);
    }
    if (!frontmatterValue(frontmatter, "description")) {
      report(ctx, "error", "skills", `.claude/skills/${name}/SKILL.md`, "skill has no description:, so neither tool can decide when to load it");
    }
  }
}

// ---------------------------------------------------------------- agents

// Claude Code built-ins and the Copilot alias each one maps to (docs.github.com, custom-agents-configuration).
const CLAUDE_TO_COPILOT = {
  Bash: "execute", PowerShell: "execute", Read: "read", NotebookRead: "read",
  Edit: "edit", MultiEdit: "edit", Write: "edit", NotebookEdit: "edit",
  Grep: "search", Glob: "search", Task: "agent", Agent: "agent",
  WebFetch: "web", WebSearch: "web", TodoWrite: "todo",
};
const COPILOT_ALIASES = new Set(["execute", "shell", "powershell", "read", "view", "edit", "write", "search", "agent", "custom-agent", "web", "todo"]);

export function parseTools(value) {
  return (value ?? "").replace(/^\[|\]$/g, "").split(",").map((t) => t.trim().replace(/^(["'])(.*)\1$/, "$2")).filter(Boolean);
}

function checkAgents(ctx, servers) {
  const known = Object.keys(servers);
  for (const file of listFiles(ctx, ".claude/agents", ".md")) {
    const rel = `.claude/agents/${file}`;
    const { frontmatter } = splitFrontmatter(readText(ctx, rel));
    const name = frontmatterValue(frontmatter, "name");
    if (name !== file.replace(/\.md$/, "")) {
      report(ctx, "error", "agents", rel, `name: is "${name ?? ""}" but the file is "${file}"`);
    }
    if (!frontmatterValue(frontmatter, "description")) report(ctx, "error", "agents", rel, "agent has no description:");

    const toolsValue = frontmatterValue(frontmatter, "tools");
    if (toolsValue === undefined) continue; // no tools: line means "all tools" in both products
    const tools = parseTools(toolsValue);

    const claudeServers = new Map(); // server -> tool names
    const copilotServers = new Map();
    for (const tool of tools) {
      const claude = /^mcp__(.+?)__(.+)$/.exec(tool);
      const copilot = /^([\w.-]+)\/(.+)$/.exec(tool);
      if (claude) {
        if (!claudeServers.has(claude[1])) claudeServers.set(claude[1], []);
        claudeServers.get(claude[1]).push(claude[2]);
      } else if (copilot) {
        if (!copilotServers.has(copilot[1])) copilotServers.set(copilot[1], []);
        copilotServers.get(copilot[1]).push(copilot[2]);
      } else if (!CLAUDE_TO_COPILOT[tool] && !COPILOT_ALIASES.has(tool)) {
        report(ctx, "warning", "agents", rel, `tool "${tool}" is neither a Claude Code built-in nor a Copilot alias; check the spelling`);
      }
    }

    for (const server of new Set([...claudeServers.keys(), ...copilotServers.keys()])) {
      const projectServer = known.find((k) => k === server);
      if (!projectServer) {
        const hint = known.find((k) => server.endsWith(k));
        report(ctx, "error", "agents", rel, hint
          ? `references MCP server "${server}", a machine-local plugin's copy; use the project server "${hint}" (mcp__${hint}__<tool> / ${hint}/*)`
          : `references MCP server "${server}", which is not in the root .mcp.json; teammates will not have it`);
        continue;
      }
      if (!claudeServers.has(server)) {
        report(ctx, "error", "agents", rel, `grants ${server}/* to Copilot but no mcp__${server}__<tool> entries to Claude Code`);
      }
      if (!copilotServers.has(server)) {
        report(ctx, "error", "agents", rel, `grants mcp__${server}__* to Claude Code but no ${server}/* entry to Copilot`);
      }
    }
  }
}

// ---------------------------------------------------------------- instructions

function checkInstructions(ctx, servers) {
  const docs = ["CLAUDE.md", ".github/copilot-instructions.md"].filter((d) => {
    if (exists(ctx, d)) return true;
    report(ctx, "error", "instructions", d, "repo-wide instruction file is missing");
    return false;
  });
  const assets = [
    ...Object.keys(servers).map((n) => ["MCP server", n]),
    ...listFiles(ctx, ".claude/agents", ".md").map((f) => ["agent", f.replace(/\.md$/, "")]),
    ...listDirs(ctx, ".claude/skills").filter((n) => !n.startsWith("openspec-")).map((n) => ["skill", n]),
  ];
  for (const doc of docs) {
    const text = readText(ctx, doc);
    for (const [kind, name] of assets) {
      if (!text.includes(name)) report(ctx, "warning", "instructions", doc, `does not mention the ${kind} "${name}"`);
    }
  }
}

// ---------------------------------------------------------------- CLI

function main() {
  const args = process.argv.slice(2);
  const rootIndex = args.indexOf("--root");
  const root = path.resolve(rootIndex >= 0 ? args[rootIndex + 1] : process.cwd());
  const fix = args.includes("--fix");
  const { findings, fixed } = run({ root, fix });

  if (args.includes("--json")) {
    process.stdout.write(`${JSON.stringify({ findings, fixed }, null, 2)}\n`);
  } else {
    for (const f of findings) console.log(`${f.severity === "error" ? "✗" : "!"} [${f.area}] ${f.file}: ${f.message}`);
    for (const f of [...new Set(fixed)]) console.log(`✓ rewrote ${f}`);
    const errors = findings.filter((f) => f.severity === "error").length;
    const warnings = findings.length - errors;
    console.log(findings.length ? `\n${errors} error(s), ${warnings} warning(s)${fix ? " before --fix" : ""}` : "AI tooling is in sync.");
  }

  // After --fix, re-check: only drift that needs a human should fail the run.
  const remaining = fix ? run({ root, fix: false }).findings : findings;
  process.exitCode = remaining.some((f) => f.severity === "error") ? 1 : 0;
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main();
}
