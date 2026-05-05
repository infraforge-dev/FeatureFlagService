---
name: safe-exec
description: >
  Command safety enforcer. Every skill must route through safe-exec before executing
  any shell command that touches git, databases, EF Core migrations, or the filesystem.
  Classifies commands against DANGEROUS_COMMANDS.md and enforces the correct gate
  (hard block, typed unlock, warn-and-confirm, or proceed). Use when any skill is
  about to run a shell command and wants to verify it is safe to execute.
---

# safe-exec

The last line of defense before a command runs. Every skill that executes shell
commands must call this before running anything that is not purely read-only.

Read `DANGEROUS_COMMANDS.md` from the project root before every check.
That file is the source of truth — not this skill's memory.

---

## How to invoke safe-exec from another skill

Before running any shell command, the calling skill says internally:

```
safe-exec: [full command string here]
```

safe-exec classifies the command and returns one of four outcomes:

- `PROCEED` — run the command, no interruption needed
- `WARN` — show the command, explain it, wait for "yes"
- `LOCKED` — show the command, show the unlock phrase, wait for exact match
- `BLOCKED` — refuse the command entirely, explain why, do not proceed

The calling skill must respect the outcome. If the outcome is BLOCKED or LOCKED
and the condition is not met, the calling skill halts and informs the user.

---

## Classification logic

Read `DANGEROUS_COMMANDS.md`. For the given command string:

1. Check TIER 0 patterns first (substring match, case-insensitive).
   - If any match → outcome is `BLOCKED`

2. Check TIER 1 patterns.
   - If any match → outcome is `LOCKED`

3. Check TIER 2 patterns.
   - If any match → outcome is `WARN`

4. Check TIER 3 patterns.
   - If any match → outcome is `PROCEED`

5. If no pattern matches in any tier:
   - Default outcome is `WARN`
   - Reason: "Command not recognized in DANGEROUS_COMMANDS.md. Treating as unclassified — please confirm before proceeding."

**More specific patterns take precedence.** If `database update 0` (TIER 0) and
`database update` (TIER 1) both match, the higher-risk tier wins.

---

## Outcome behaviors

### BLOCKED (Tier 0)

```
🚫 BLOCKED — This command cannot be executed.

Command: [full command]
Reason:  [reason from DANGEROUS_COMMANDS.md]

This command is in Tier 0 — there is no override. If you believe this
block is wrong, edit DANGEROUS_COMMANDS.md to move the pattern to a
lower tier, then restart the session.

What would you like to do instead?
```

Do not offer workarounds. Do not suggest how to bypass. Stop completely and hand back to the user.

---

### LOCKED (Tier 1)

```
🔴 LOCKED — Explicit authorization required.

Command:       [full command]
Risk:          [reason from DANGEROUS_COMMANDS.md]

To authorize this command, type exactly:
  [unlock_phrase from DANGEROUS_COMMANDS.md]

Type anything else to cancel.
```

- Wait for the user's next message.
- If it matches the unlock phrase **exactly** (case-insensitive, trimmed) → run the command, log the authorization.
- If it does not match → cancel the command, do not retry, return to the calling skill.

**Log format after authorized execution:**
```
✅ Authorized and executed: [command]
Authorization phrase: "[phrase typed]"
Timestamp: [current time]
```

---

### WARN (Tier 2)

```
🟡 HEADS UP — Please confirm before I run this.

Command: [full command]
Reason:  [reason from DANGEROUS_COMMANDS.md]

Type "yes" to proceed, anything else to cancel.
```

- Wait for the user's next message.
- If it is "yes" (case-insensitive) → run the command.
- If it is anything else → cancel and return to calling skill.

---

### PROCEED (Tier 3)

Run the command without interruption. No message to the user.
Optionally log to a session trace if the calling skill maintains one.

---

### UNCLASSIFIED (no pattern match)

Treat as WARN. Use this message:

```
🟡 UNCLASSIFIED COMMAND — Not found in DANGEROUS_COMMANDS.md.

Command: [full command]

This command is not listed in any tier. Out of caution, I'm pausing
before running it. If this is safe, type "yes" to proceed — or add
it to TIER 3 in DANGEROUS_COMMANDS.md to stop seeing this warning.
```

---

## Rules for calling skills

These rules apply to every skill in the toolbelt:

1. **Always call safe-exec before running:** any git command, any dotnet ef command,
   any rm/del/rmdir, any command that writes to or deletes files outside /tmp.

2. **Never pre-filter on your own.** Do not decide a command is "probably safe" and
   skip safe-exec. The tier list exists so that judgment is centralized and editable.

3. **If safe-exec returns BLOCKED or the user cancels a LOCKED/WARN prompt:**
   - Surface the block to the user clearly
   - Offer an alternative path if one exists (e.g., "I can't run database drop, but
     I can generate a rollback migration instead")
   - Do not attempt a workaround without explicit user instruction

4. **Never re-submit a blocked command with modified syntax to bypass the check.**
   This includes: splitting a command across multiple calls, using aliases, or using
   environment variables to obscure dangerous flags.

---

## What safe-exec does NOT do

- It does not run commands itself. It classifies and gates them.
- It does not maintain a command history (that's the calling skill's job if needed).
- It does not suggest how to bypass Tier 0 blocks — ever.
- It does not override the tier list based on conversational context
  ("I know you said this is blocked but in this case..."). The file is law.
