---
name: git-workflow
description: >-
  The git workflow for this repo: branches, worktrees, commits, pushes and pull
  requests. Use for ANY git operation, before starting work in the repo, and
  whenever an OpenSpec change is proposed (/opsx:propose, openspec-propose),
  applied (/opsx:apply, openspec-apply-change, "implement the <name> change") or
  archived (/opsx:archive, openspec-archive-change). Day-to-day work happens on the
  `development` branch, created from `main` if it is missing; `main` and
  `development` always share the main checkout and never get a worktree of their
  own. Proposals and archives are committed on `development`. Applying a change
  happens in a new worktree on a `<type>/<change>` branch cut from `development`,
  with very frequent logical commits, then a push and a PR into `development`.
---

# Git workflow

## The branch model

```
main ─────────────────────────────────────────────────────────►  (release line)
  └─► development ──●──────────────●────────────●──────────────►  (integration line)
       propose ─────┘              │            └── archive
                                   └─► feat/<change>  ●─●─●─●─●  ──► PR into development
                                       (own worktree)
```

| Branch | Lives in | Who commits to it |
| --- | --- | --- |
| `main` | the main checkout (repo root) | nobody directly — only via PR from `development` |
| `development` | the main checkout (repo root) | proposals and archives of OpenSpec changes, plus other small day-to-day work |
| `<type>/<change-name>` | its own worktree, `.claude/worktrees/<change-name>` | implementation of one OpenSpec change |

Rules that follow from this:

- **The developer always works on (a local copy of) `development`.** In the main
  checkout, `development` is the branch you have checked out unless you are
  deliberately looking at `main`.
- **`main` and `development` always live in the same worktree** — the main checkout
  at the repo root. Switch between them with `git switch`. **Never** create a
  worktree for `main` or `development`, and never check either of them out inside a
  change worktree.
- **Change branches are cut from `development` and merge back into `development`.**
  Promoting `development` to `main` is a release decision for the user; the agent
  never does it on its own.

Invoking this skill — directly or through an OpenSpec command — authorises the agent
to create `development` if it is missing, commit to it, push it, create change
worktrees and branches, commit and push them, and open PRs into `development`. It is
**not** authorisation to merge a PR, commit to `main`, force-push, or rewrite
pushed history.

## 0. Make sure you are on `development` — always first

Run from the **main checkout** (the repo root):

1. `git fetch origin --prune`
2. **Does `development` exist?**
   - Locally (`git rev-parse --verify --quiet refs/heads/development`) → use it.
   - Only on the remote (`git rev-parse --verify --quiet refs/remotes/origin/development`)
     → `git switch --track origin/development`.
   - Nowhere → create it from `main` and publish it:
     ```bash
     git switch -c development origin/main
     git push -u origin development
     ```
     Tell the user you created it.
3. **Switch to it** if the main checkout is on another branch: `git switch development`.
   If uncommitted work blocks the switch, stop and ask — never stash, reset, discard
   or carry the user's work across on your own.
4. **Bring it up to date:** `git pull --ff-only`. If it cannot fast-forward
   (local and remote diverged), stop and report — do not rebase or merge it
   without the user's say-so.

## 1. Propose a change — commit on `development`

When `openspec-propose` (`/opsx:propose`) runs, do step 0 first, so the change
folder is created on `development` in the main checkout. Then commit the proposal
as **logical commits** as the artifacts are written, not as one lump:

```
docs(openspec): propose <change-name>            ← proposal.md + .openspec.yaml
docs(openspec): add <change-name> design          ← design.md
docs(openspec): add <change-name> specs           ← specs/*/spec.md (one commit per capability if they are large)
docs(openspec): add <change-name> tasks           ← tasks.md
```

Small proposals may fold into fewer commits; the test is that each commit is one
coherent, reviewable step. When the proposal is complete, push `development`
(`git push`) so that the change branch cut later contains it and its PR shows only
the implementation.

## 2. Apply a change — new worktree, new branch, frequent commits, PR

When `openspec-apply-change` (`/opsx:apply`) runs, set up git **before the first line
of code**:

### 2.1 Prepare

1. Do **step 0**, then make sure the change folder is committed on `development`
   and pushed. If it still sits there uncommitted, commit it as in step 1 first —
   otherwise the worktree will not contain it.
2. **Pick the branch name** — `<type>/<change-name>`, where `<type>` is the
   Conventional Commit type that best describes the change: `feat` (default), `fix`,
   `refactor`, `perf`, `docs`, `chore`. Example: `feat/passenger-happiness`.
3. **Resume or create.** If the branch or `.claude/worktrees/<change-name>` already
   exists, this is a resume — continue in the existing worktree and do not recreate
   it. Otherwise:
   ```bash
   git worktree add .claude/worktrees/<change-name> -b <type>/<change-name> development
   ```
   `.claude/worktrees/` is git-ignored.
4. **Move all work into the worktree.** Every file read, edit, build, test and git
   command runs against `.claude/worktrees/<change-name>/` — never the main
   checkout, and never on `development` or `main`.
   - **Claude Code:** use the `EnterWorktree` tool if available; otherwise absolute
     paths into the worktree and `git -C <worktree>`.
   - **Copilot CLI / shell:** `cd` into the worktree, or `git -C <worktree>`.
   - A worktree is a fresh checkout without `bin/`, `obj/` or `node_modules/`: run
     `dotnet restore` (from `src/`) and, if the Angular app is touched, `npm ci`
     (from `src/FourDotnet.BoogaBooster.App`). Aspire user secrets are shared by
     `UserSecretsId` and need no setup.
5. **Announce:**
   `Working on <type>/<change-name> in .claude/worktrees/<change-name> (from development @ <short-sha>)`.
6. **Linked GitHub issue?** If `proposal.md` contains `<!-- github-issue: <n> -->`,
   sync the issue as the `github-issue-triage` skill (section 5) describes:
   `spec:proposed` → `spec:in-progress`, plus a comment naming the branch.

### 2.2 Implement — commit very frequently

Commit **early and often**: every time a small, coherent step is green, commit it.
Many small commits are expected; one big commit at the end is wrong.

- **Grain.** One task from `tasks.md` is the *largest* commit; split it further
  whenever it has natural steps (a value object, then the aggregate that uses it,
  then the handler, then the endpoint). Never mix unrelated concerns or two
  modules' unrelated work in one commit.
- **Tests travel with the code** they cover — same commit.
- **`tasks.md` travels with the work** — tick `- [ ]` → `- [x]` and stage it with
  the commit that completes the task.
- **Green before every commit** — build and run the tests for what you touched
  (`dotnet test` on the affected test project; `npx vitest run <spec>` for the
  frontend). Never commit a red build.
- **Stage explicitly** — `git add <paths>`, never `git add -A` / `git add .`. Check
  `git diff --cached --stat`; nothing generated (`bin/`, `obj/`, coverage output,
  `.code-review/`) or unrelated may slip in.
- **Push regularly** (`git push -u origin <type>/<change-name>` the first time,
  `git push` after) so work in progress is never only on one machine.
- If a design issue forces a change to `proposal.md`, `design.md` or a spec, commit
  it separately as `docs(openspec): …` so reviewers see it.

### 2.3 Finish — verify, push, PR into `development`

When every task is complete:

1. **Verify the whole branch** from the worktree:
   `cd src && dotnet build BoogaBooster.slnx && dotnet test BoogaBooster.slnx`, plus
   `npm test` in the Angular app if any frontend file changed. Check the
   `test-coverage` floor for any changed backend module. Fix in a new commit or stop
   and report — never open a PR on red.
2. **Clean tree** — `git status --porcelain` is empty, every task in `tasks.md` is `[x]`.
3. **Catch up with `development`** if it moved: `git fetch origin` then
   `git merge origin/development` (the branch is already pushed, so merge — never
   rebase pushed history). Resolve conflicts and re-run the tests.
4. **Push:** `git push`.
5. **Open the PR into `development`:**
   ```bash
   gh pr create --base development --head <type>/<change-name> \
     --title "<type>(<change-name>): <one-line summary>" \
     --body-file <file>
   ```
   Write the body to a file in the scratchpad (not the repo):
   ```markdown
   ## Summary
   <2–4 sentences: what the change does and why — from proposal.md>

   OpenSpec change: `openspec/changes/<change-name>/`
   Refs #<n>   ← only if proposal.md links a GitHub issue

   ## Changes
   - <one bullet per area / group of commits>

   ## Specs affected
   - `<capability>` — <added / modified requirement>

   ## Verification
   - `dotnet test BoogaBooster.slnx` — <N passed>
   - `npm test` — <N passed>   (if applicable)

   ## Follow-up
   - Archive the change on `development` (`/opsx:archive <change-name>`) after merge.
   ```
   End the body with the PR attribution line if the session requires one. If the
   diff touches `.github/workflows/`, `.github/code-review/`, `CLAUDE.md` or
   `.claude/`, say so in the Summary — the CI review flags those `major` by design.
6. **Linked GitHub issue?** Move it `spec:in-progress` → `spec:in-review` and
   comment with the PR link (`github-issue-triage` section 5).
7. **Report** the PR URL, branch, worktree path and
   `git log --oneline origin/development..HEAD`.

**Do not** merge the PR, enable auto-merge or remove the worktree. After the merge
the user (or a later session) cleans up from the main checkout:

```bash
git worktree remove .claude/worktrees/<change-name>
git branch -d <type>/<change-name>
```

## 3. Archive a change — commit on `development`

When `openspec-archive-change` (`/opsx:archive`) runs, do **step 0** first, so the
archive happens on an up-to-date `development` in the main checkout — which, after
the PR merged, contains the implemented change. Then commit logically:

```
docs(openspec): sync <change-name> specs into openspec/specs     ← only if specs were synced
docs(openspec): archive <change-name>                             ← the move into changes/archive/
```

Stage the move with `git add openspec/changes/<change-name> openspec/changes/archive/<date>-<change-name>`
so git records it as a rename. Push `development` when done. If the change's PR is
not merged yet, warn the user before archiving.

If `proposal.md` links a GitHub issue (`<!-- github-issue: <n> -->`), finish by
syncing it per `github-issue-triage` section 5: `spec:done`, point its OpenSpec link
at the archive path, and close it as completed with a comment.

## Commit messages

Conventional Commits, matching the repo history:

```
<type>(<scope>): <imperative summary, lower case, no full stop>

<optional body: why, not what — wrapped at ~72 chars>

<attribution trailer, if the session requires one>
```

`<scope>` is the module or capability (`queue`, `digital-twin`, `weather`, the change
name, `openspec`, …). Examples:

- `feat(passenger-happiness): add properties for happiness, ride intensity, and nausea`
- `fix(queue): compute the status average from the same snapshot as the groups`
- `docs(openspec): propose single-riders-queue`

Pass multi-line messages with a heredoc (bash) or a single-quoted here-string
(PowerShell), not with chained `-m` flags.

## Guardrails

- Never commit to `main`. Never implement an OpenSpec change on `development` or in
  the main checkout.
- Never create a worktree for `main` or `development`; never check them out in a
  change worktree.
- Never stash, reset, clean or delete the user's uncommitted work in any checkout.
- Never force-push, rewrite pushed history, amend a pushed commit, or skip hooks
  or signing (`--no-verify`, `--no-gpg-sign`).
- Never merge a PR or enable auto-merge, and never promote `development` to `main`
  unless the user asks.
- If `gh auth status` fails, push the branch, stop, and ask the user to run
  `! gh auth login`; then open the PR.
- If a push or PR creation is rejected, report the exact error — do not retry with
  `--force` or another remote.
- Pausing mid-change is fine: commit and push what is green, leave the rest
  uncommitted in the worktree, and report where things stand. The next
  `/opsx:apply <change-name>` resumes in the same worktree.
