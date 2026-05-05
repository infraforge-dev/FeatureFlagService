# DANGEROUS_COMMANDS.md
# Command Safety Tier List
#
# This file is read by the safe-exec skill before any shell command is executed.
# Edit this file to adjust what's blocked, warned, or allowed.
#
# HOW TIERS WORK:
#   TIER 0 — Hard block. No exceptions. Agent refuses and explains why.
#   TIER 1 — Locked. Agent shows the command and requires you to TYPE the unlock phrase exactly.
#   TIER 2 — Warn. Agent shows the command and waits for explicit "yes" before proceeding.
#   TIER 3 — Safe. Agent may proceed without interruption.
#
# FORMAT:
#   Each entry is a pattern (substring match against the full command string).
#   Patterns are case-insensitive.
#   More specific patterns take precedence over general ones.
#   Add a comment after each entry to document WHY it's in that tier.

---

## TIER 0 — Hard Block (no exceptions, no override)

These commands are never executed under any circumstances.
If the agent encounters one, it must refuse, explain the risk, and stop.

patterns:
  # Git — permanent remote history destruction
  - "push --force"           # Rewrites remote history. Breaks teammates. Unrecoverable without repo admin.
  - "push -f "               # Short form of --force. Same risk.
  - "push -f\n"              # Handles end-of-command edge case

  # Git — branch/remote deletion
  - "push origin --delete"   # Deletes a remote branch permanently
  - "push origin :refs"      # Refspec deletion syntax — same risk as above

  # Database — nuclear options
  - "database drop"          # Destroys the entire database. Unrecoverable without backup.
  - "DROP DATABASE"          # Raw SQL equivalent of database drop
  - "DROP TABLE"             # Destroys a table and all its data permanently
  - "DELETE FROM"            # Mass delete — only safe with a WHERE clause (see TIER 1 for parameterized form)
  - "TRUNCATE TABLE"         # Empties a table instantly, no transaction log, often non-rollbackable

  # EF Core — rollback everything
  - "database update 0"      # Rolls back ALL migrations. Destroys the entire schema.

  # Filesystem — recursive deletion outside safe zones
  - "rm -rf /"               # Deletes the entire filesystem. No explanation needed.
  - "rm -rf ~"               # Deletes home directory
  - "rm -rf .."              # Deletes parent directory
  - "rmdir /s /q C:\\"       # Windows equivalent of rm -rf /

---

## TIER 1 — Locked (requires typed unlock phrase)

These commands are blocked by default.
To authorize one, you must type the exact unlock phrase shown by the agent.
This prevents accidental approval — you have to mean it.

patterns:
  # Git — local destructive state operations
  - pattern: "reset --hard"
    unlock_phrase: "I authorize reset --hard"
    reason: "Permanently discards all uncommitted changes. Cannot be undone."

  - pattern: "clean -fd"
    unlock_phrase: "I authorize clean -fd"
    reason: "Deletes all untracked files and directories. Unrecoverable without a backup."

  - pattern: "clean -f "
    unlock_phrase: "I authorize clean -f"
    reason: "Deletes untracked files. Unrecoverable."

  - pattern: "git checkout ."
    unlock_phrase: "I authorize checkout ."
    reason: "Discards all unstaged changes to tracked files."

  - pattern: "git restore ."
    unlock_phrase: "I authorize restore ."
    reason: "Discards all unstaged changes to tracked files."

  - pattern: "commit --amend"
    unlock_phrase: "I authorize commit --amend"
    reason: "Rewrites the last commit. Dangerous if the commit was already pushed."

  - pattern: "rebase"
    unlock_phrase: "I authorize rebase"
    reason: "Rewrites commit history. Dangerous on any branch that has been pushed."

  - pattern: "branch -D "
    unlock_phrase: "I authorize branch delete"
    reason: "Force-deletes a local branch regardless of merge status. Unrecoverable."

  # EF Core — migration surgery
  - pattern: "migrations remove"
    unlock_phrase: "I authorize migrations remove"
    reason: "Deletes the last migration file. Catastrophic if already applied to any environment."

  - pattern: "database update"
    unlock_phrase: "I authorize database update"
    reason: "Runs migrations against a live database. Always confirm the connection string first with: dotnet ef dbcontext info"

  # Git — pushing to protected branches
  - pattern: "push origin main"
    unlock_phrase: "I authorize push to main"
    reason: "Pushes directly to the main branch. Should go through a PR."

  - pattern: "push origin master"
    unlock_phrase: "I authorize push to master"
    reason: "Pushes directly to the master branch. Should go through a PR."

  - pattern: "push origin dev"
    unlock_phrase: "I authorize push to dev"
    reason: "Pushes directly to the dev branch. Should go through a PR."

---

## TIER 2 — Warn (show command, wait for explicit "yes")

These commands are allowed but the agent must pause, display the full command,
explain what it does, and wait for you to type "yes" before proceeding.

patterns:
  # Git — any push to a remote (not already caught by TIER 1)
  - pattern: "git push"
    reason: "Pushes to a remote branch. Agent will show the target branch before proceeding."

  # Git — stash destruction
  - pattern: "stash drop"
    reason: "Permanently removes a stash entry."

  - pattern: "stash clear"
    reason: "Permanently removes ALL stash entries."

  # EF Core — migration generation (safe to generate, risky to apply)
  - pattern: "migrations add"
    reason: "Generates a new migration file. Agent will show the generated .cs file before any apply step."

  # File operations — bulk deletes in working directory
  - pattern: "rm -rf"
    reason: "Recursive delete. Agent will show the exact path before proceeding."

  - pattern: "del /f /s /q"
    reason: "Windows force-delete. Agent will show the exact path before proceeding."

---

## TIER 3 — Safe (proceed without interruption)

These commands are explicitly approved. The agent may run them autonomously.

patterns:
  # Build and test
  - "dotnet build"
  - "dotnet test"
  - "dotnet run"
  - "dotnet restore"
  - "dotnet format"
  - "dotnet publish"

  # EF Core — read-only / informational
  - "migrations list"
  - "dbcontext info"
  - "dbcontext list"
  - "dbcontext script"
  - "migrations script"     # Generates SQL — does not apply it

  # Git — read-only operations
  - "git status"
  - "git log"
  - "git diff"
  - "git stash list"
  - "git branch"            # Listing branches only
  - "git fetch"
  - "git remote -v"
  - "git show"
  - "git blame"

  # Git — safe write operations
  - "git add"
  - "git commit"            # New commits only — amend is TIER 1
  - "git stash"             # Creating a stash — dropping is TIER 2
  - "git checkout -b"       # Creating a new branch
  - "git switch -c"         # Creating a new branch (modern syntax)
  - "git merge --no-ff"     # Explicit merge with commit — fast-forward only variants are safe too

  # File reads
  - "cat "
  - "ls "
  - "dir "
  - "find "
  - "grep "
  - "type "                 # Windows cat equivalent
