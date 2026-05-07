---
name: git-workflow
description: >
  Generate a reviewable summary and executable shell script that creates a feature branch,
  groups changes into conventional commits, pushes, and opens a draft PR. Use this skill
  whenever the user says "/git-workflow", "generate commits", "write the PR", "let's commit",
  or after post-work documentation is complete. The user reviews the summary and runs the
  script. The session ends here.
---

# Git Workflow

Produce a reviewable summary and a single executable shell script. The user reviews both,
runs the script, and the workflow is done. No copy-pasting individual commands.

## Before you begin -- confirm readiness

Ask: "Is post-work complete? Are all foundation docs updated?" Do not proceed until
confirmed. This is the final step -- foundation docs must already reflect the new state.

## Step 1 -- Read the context

Read these to produce accurate commit messages and PR content:

- `Docs/decisions/<feature-branch-name>/spec.md` -- scope, phase, feature name
- `Docs/decisions/<feature-branch-name>/implementation-notes.md` -- what was built,
  deviations, key decisions, file-by-file changes

## Step 2 -- Plan the branch, commits, and PR

### Branch rules

- If currently on `dev`, `main`, or `master`, the script must create a feature branch
  before any commit commands.
- Prefer the branch name from the spec frontmatter/body if present, e.g.
  `refactor/typed-strategy-config`.
- If the spec does not name a branch, derive one from the decision folder or feature
  name using lowercase kebab-case and one of these prefixes:
  - `feature/` for net-new behavior
  - `fix/` for bug fixes or hardening
  - `refactor/` for structural changes
  - `docs/` for documentation-only work
- If already on a non-base feature branch, keep that branch.
- The script must never push to `dev`, `main`, or `master`.

### Commit grouping

Group file changes into 3-5 commits maximum. Use this grouping priority:

| Group | Files | Commit type |
|---|---|---|
| Domain / Application layer | entities, value objects, interfaces, services, validators | `feat` or `refactor` |
| Infrastructure / Persistence | repositories, DbContext, migrations, config, converters | `feat` or `refactor` |
| API layer | controllers, middleware, DTOs, DI registration | `feat` or `fix` |
| Tests | unit tests, integration tests, test helpers | `test` |
| Documentation | all Docs/decisions/*, Docs/architecture.md, etc. | `docs` |

Never mix code and documentation in the same commit. The docs commit is always last.

### Commit message format

Use Conventional Commits strictly:

```
<type>(<scope>): <short description in sentence case, <=72 chars>

<optional body: what and why, not how. Wrap at 72 chars.>
```

Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`
Scope: the layer or domain area (e.g., `domain`, `api`, `infra`, `tests`, `docs`)

Rules:
- Description is sentence case, no period at the end
- Body is optional but include it for non-obvious changes
- One blank line between subject and body
- Reference the spec in the docs commit body

### PR content

**Title format:** `<type>(<scope>): <Feature Name> -- Phase <X>`

**Body template:**

```markdown
## Summary

<2-3 sentences from the "What Was Built" section of implementation-notes.md>

## Changes in this PR

<bullet list of the logical commit groups -- one line each>

## Spec

`Docs/decisions/<feature-branch-name>/spec.md`

## Implementation Notes

`Docs/decisions/<feature-branch-name>/implementation-notes.md`

## Definition of Done

<paste the DoD checklist from the spec with checkmarks applied>

## Testing

<paste the "How to Test" section from implementation-notes.md>
```

Default base branch is `dev` unless the repository default branch or user request
clearly says otherwise.

## Step 3 -- Present the summary for review

Show the user a structured summary:

```
## Git Workflow Summary

**Branch:** <branch-name> (from <base-branch>)
**Commits:** <N>

### Commit 1 -- <group name>
<commit message subject>
Files: <list of files/globs>

### Commit 2 -- <group name>
...

### PR
**Title:** <title>
**Base:** <base-branch>
**Draft:** yes

<PR body preview>
```

Then say:

> Review the summary above. The script is at `/tmp/<feature-slug>-workflow.sh`.
> Run it with `! bash /tmp/<feature-slug>-workflow.sh` when ready.

## Step 4 -- Write the script

Write the script to `/tmp/<feature-slug>-workflow.sh`. The script must:

1. Be executable as a single `bash` invocation
2. Use `set -euo pipefail` at the top -- fail fast on any error
3. Create or switch to the feature branch
4. Stage files and commit in the planned groups (use HEREDOCs for multi-line messages)
5. Push with `-u origin <branch-name>`
6. Write the PR body to a temp file and create a draft PR via `gh pr create`
7. Print the PR URL on success
8. Clean up the PR body temp file
9. Delete itself (`rm -- "$0"`) as the last line

**Script structure:**

```bash
#!/usr/bin/env bash
set -euo pipefail

# --- Branch setup ---
git switch -c <branch-name>

# --- Commit 1: <group> ---
git add <files>
git commit -m "$(cat <<'EOF'
<commit message>
EOF
)"

# --- Commit N: <group> ---
# ...

# --- Push ---
git push -u origin <branch-name>

# --- PR ---
cat > /tmp/<slug>-pr-body.md <<'PREOF'
<PR body markdown>
PREOF

gh pr create \
  --base dev \
  --head <branch-name> \
  --title "<title>" \
  --body-file /tmp/<slug>-pr-body.md \
  --draft

rm -f /tmp/<slug>-pr-body.md

# --- Self-destruct ---
rm -- "$0"
```

## Step 5 -- Hand off

After writing the script and presenting the summary, end with:

> That's everything. Review the summary, then run the script. The branch will be
> created, commits made, pushed, and a draft PR opened. The script deletes itself
> after a successful run.

Do not suggest any further agent actions. The session ends here.
