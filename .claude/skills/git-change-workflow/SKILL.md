---
name: git-change-workflow
description: >-
  Git workflow for implementing an OpenSpec change. Use whenever an OpenSpec change
  is about to be applied (/opsx:apply, openspec-apply-change, "implement the
  <name> change", "start on the tasks") and for the whole time it is being
  implemented. Before the first line of code it creates a dedicated git worktree
  and branch from an up-to-date origin/main; during implementation it makes small,
  logical Conventional Commits; when every task is done it pushes the branch and
  opens a pull request into main with gh. Never implement an OpenSpec change on
  main or in the main checkout.
---

# Git workflow for OpenSpec changes

An OpenSpec change is implemented on **its own branch, in its own worktree, cut from
`origin/main`**, and lands on `main` only through a **pull request** — where the
two-model Copilot review in `.github/workflows/code-quality-check.yml` gates it.

The main checkout stays on `main` and untouched, so other work (another change,
a hotfix, a review) can carry on beside it.

Invoking this skill — directly, or by applying an OpenSpec change — is the user's
authorisation to create the branch, commit, push it and open the PR. It is **not**
authorisation to merge, force-push, or touch `main`.

## The flow

```
origin/main ──► worktree + branch ──► task ► commit ► task ► commit … ──► verify ──► push ──► PR
```

### 1. Prepare — before applying the change

Run from the **main checkout** (the repo root). Resolve the change name first (the
same way `openspec-apply-change` does), then:

1. **Check the main checkout is safe to branch from.**
   `git status --porcelain`. Unrelated uncommitted work stays where it is — never
   stash, reset or discard it. Only the change's own folder
   (`openspec/changes/<name>/`) is carried across (step 4).

2. **Bring `main` up to date.**
   ```bash
   git fetch origin --prune
   ```
   Branch from `origin/main`, not from the local `main`, so a stale local branch
   can never leak into the PR.

3. **Pick the branch name** — `<type>/<change-name>`, where `<type>` is the
   Conventional Commit type that best describes the change as a whole:
   `feat` (new capability — the default), `fix`, `refactor`, `perf`, `docs`, `chore`.
   Example: `feat/passenger-happiness`.

   If the branch or the worktree already exists, **this is a resume** — skip to
   step 5 and continue in the existing worktree. Do not recreate it.

4. **Create the worktree and branch.**
   ```bash
   git worktree add .claude/worktrees/<change-name> -b <type>/<change-name> origin/main
   ```
   `.claude/worktrees/` is git-ignored and is the same place Claude Code puts its own
   worktrees.

   **The change folder may not exist on `main`.** A freshly proposed change usually
   lives only as untracked files in the main checkout, so a worktree cut from
   `origin/main` will not contain it. Check
   `git ls-tree -d origin/main openspec/changes/<change-name>`; if it is missing,
   copy `openspec/changes/<change-name>/` into the same path in the worktree and make
   it the branch's first commit:
   ```
   docs(openspec): propose <change-name>
   ```
   Once it is committed on the branch, tell the user they can delete the untracked
   copy from the main checkout — do not delete it yourself.

5. **Move all work into the worktree.** From here on, every file read, edit, build,
   test and git command runs against `.claude/worktrees/<change-name>/`, never the
   main checkout.
   - **Claude Code:** use the `EnterWorktree` tool if it is available so the session
     switches into the worktree; otherwise use absolute paths into the worktree and
     `git -C <worktree>` for git.
   - **Copilot CLI / shell:** `cd` into the worktree, or use `git -C <worktree>`.
   - A worktree is a fresh checkout: `bin/`, `obj/` and `node_modules/` are absent.
     Run `dotnet restore` (from `src/`) and, if the change touches the Angular app,
     `npm ci` (from `src/FourDotnet.BoogaBooster.App`) before building. Aspire user
     secrets are keyed by `UserSecretsId`, so they are shared and need no setup.

6. **Announce it:**
   `Working on <type>/<change-name> in .claude/worktrees/<change-name> (from origin/main @ <short-sha>)`.

Only now hand over to `openspec-apply-change` to implement the tasks.

### 2. Implement — logical commits

Commit as you go, not in one lump at the end. A commit is one **coherent, reviewable
step** that leaves the solution building and its tests passing.

- **Grain.** Usually one task, or a small group of tightly related tasks from
  `tasks.md` (e.g. a value object and its tests). Split a task that mixes concerns
  (domain change + unrelated refactor). Never mix two modules' unrelated work in one
  commit.
- **Tests travel with the code** they cover — same commit, not a "add tests" commit
  afterwards.
- **`tasks.md` travels with the work.** Tick the task's checkbox (`- [ ]` → `- [x]`)
  and stage `tasks.md` in the same commit as the code that completes it.
- **Before each commit:** build and run the tests for what you touched
  (`dotnet test` on the affected test project; `npx vitest run <spec>` for the
  frontend). Do not commit a red build.
- **Stage explicitly** — `git add <paths>`, never `git add -A` / `git add .`. Check
  `git diff --cached --stat` before committing; nothing generated (`bin/`, `obj/`,
  coverage output, `.code-review/`) or unrelated may slip in.
- **Message format — Conventional Commits**, matching the repo history:
  ```
  <type>(<scope>): <imperative summary, lower case, no full stop>

  <optional body: why, not what — wrapped at ~72 chars>

  <attribution trailer, if the session requires one>
  ```
  `<scope>` is the module or capability (`queue`, `digital-twin`, `weather`,
  `observability`, the change name, …). Examples from this repo:
  - `feat(passenger-happiness): add properties for happiness, ride intensity, and nausea`
  - `fix(queue): compute the status average from the same snapshot as the groups`
  - `docs(rider-experience): add the physics doc the rider constants cite`

  Pass multi-line messages with a heredoc (bash) or a single-quoted here-string
  (PowerShell), never by chaining `-m` flags that lose formatting.
- **Never** amend or rewrite a commit that has already been pushed, never
  `--no-verify`, never commit to `main`.

If a design issue forces a change to `proposal.md`, `design.md` or a spec, commit
that artifact update separately as `docs(openspec): …` so reviewers see it.

### 3. Finish — verify, push, open the PR

When `openspec-apply-change` reports every task complete:

1. **Verify the whole branch**, from the worktree:
   ```bash
   cd src && dotnet build BoogaBooster.slnx && dotnet test BoogaBooster.slnx
   ```
   plus `npm test` in the Angular app if any frontend file changed. If anything
   fails, fix it (in a new commit) or stop and report — do not open a PR on red.
   Check the `test-coverage` floor for any backend module that changed.

2. **Confirm the tree is clean** — `git status --porcelain` is empty and every task
   in `tasks.md` is `[x]`.

3. **Catch up with main** if it moved:
   `git fetch origin && git rebase origin/main` — only while the branch is still
   unpushed. Once pushed, merge `origin/main` instead of rebasing. Resolve conflicts,
   re-run the tests.

4. **Push:**
   ```bash
   git push -u origin <type>/<change-name>
   ```

5. **Open the PR into `main`:**
   ```bash
   gh pr create --base main --head <type>/<change-name> \
     --title "<type>(<change-name>): <one-line summary>" \
     --body-file <file>
   ```
   Write the body to a file in the scratchpad (not the repo) and use this shape:
   ```markdown
   ## Summary
   <2–4 sentences: what the change does and why — from proposal.md>

   OpenSpec change: `openspec/changes/<change-name>/`

   ## Changes
   - <one bullet per logical commit / area>

   ## Specs affected
   - `<capability>` — <added / modified requirement>

   ## Verification
   - `dotnet test BoogaBooster.slnx` — <N passed>
   - `npm test` — <N passed>   (if applicable)

   ## Follow-up
   - Archive the change (`/opsx:archive <change-name>`) after merge.
   ```
   End the body with the PR attribution line if the session requires one. If the
   diff touches `.github/workflows/`, `.github/code-review/`, `CLAUDE.md` or
   `.claude/`, say so in the Summary — the CI review will flag it `major` by design.

6. **Report** the PR URL, the branch, the commit list (`git log --oneline origin/main..HEAD`)
   and the worktree path.

**Do not** merge the PR, enable auto-merge, or remove the worktree. After the PR is
merged the user (or a later session) cleans up:
```bash
git worktree remove .claude/worktrees/<change-name>
git branch -d <type>/<change-name>
```

## Guardrails

- No OpenSpec implementation on `main`, and no edits in the main checkout while a
  change is in progress.
- Never stash, reset, clean or delete the user's uncommitted work in either checkout.
- Never force-push, never rewrite pushed history, never skip hooks or signing.
- If `gh` is not authenticated (`gh auth status` fails), push the branch, stop, and
  ask the user to run `! gh auth login`; then open the PR.
- If the push or PR creation is rejected, report the exact error — do not retry with
  `--force` or a different remote.
- Pausing mid-change (blocker, question) is fine: commit what is complete and green,
  leave the rest uncommitted in the worktree, and report where things stand. The next
  `/opsx:apply <change-name>` resumes in the same worktree (step 1.3).
