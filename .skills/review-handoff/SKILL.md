---
name: review-handoff
description: >
  Push a spec or document to Google Drive for human review on a tablet, then read
  annotation comments back and facilitate a structured discussion before approving
  changes. Use when the user says "send for review", "send to tablet", 
  "/review-handoff", "/review-handoff --read", "pull my comments", or "let's discuss
  my annotations". Also offer this automatically at the end of /spec as an alternative
  to immediate approval — any time a spec is ready and the user might want to review
  it offline before committing to implementation.
---

# review-handoff

Push a document to Google Drive for tablet review. Pull comments back. Discuss them
one at a time. Only update the spec when you and the developer agree on a change.

This skill has two modes — **push** and **read** — invoked explicitly by the developer.
The agent never calls either mode automatically.

See [REFERENCE.md](REFERENCE.md) for Google Drive API patterns, comment extraction
logic, and the annotation conventions the developer uses on their tablet.
See [CONVENTIONS.md](CONVENTIONS.md) for the annotation rules to remind the developer
before they open the doc.

---

## Mode 1 — Push (`/review-handoff` or `/review-handoff --push`)

Send a local document to Google Drive for offline review.

### Step 1 — Identify the document

Ask or infer:
- Which file is being sent? (default: the most recently produced spec in
  `Docs/decisions/<feature-branch-name>/spec.md`)
- Confirm the file exists before proceeding.

### Step 2 — Convert and upload

1. Read the local markdown file in full.
2. Upload to Google Drive at: `Banderas/review/<filename>`
   - If a file with the same name already exists at that path, ask before overwriting.
   - Use plain filename only — no branch prefix, no timestamp. These are temporary files.
3. Return the shareable Google Doc URL.

See REFERENCE.md → "Upload workflow" for the API call sequence.

### Step 3 — Brief the developer

After a successful upload, output this block exactly:

```
📤 Sent to Google Drive

File:   <filename>
Drive:  Banderas/review/<filename>
URL:    <google-doc-url>

Before you annotate, read CONVENTIONS.md — the short version:
  ✅ Add a `## Review Comments` heading at the bottom of the doc
  ✅ Each comment is a list item: `- [<section>] <your note>`
  ⚠️  Do NOT use Google Docs' built-in comment feature — those don't survive
      the markdown round-trip (the current MCP toolset has no comments API)
  ⚠️  Do not edit the body of the doc — only append to the Review Comments
      section. Body edits will be lost.
  ❌  Handwriting and ink are not preserved on export

When you're done, come back and run: /review-handoff --read
```

Do not proceed further. The session hands off to the developer.

---

## Mode 2 — Read (`/review-handoff --read`)

Pull the developer's comments from Google Drive and facilitate a design discussion.

### Step 1 — Locate the document

Ask or infer which file to read from. Default: the most recently pushed file from
this session. If ambiguous, list files currently in `Banderas/review/` and ask.

### Step 2 — Fetch and extract comments

1. Download the Google Doc as markdown (Drive `files.export` with `text/markdown`
   — see REFERENCE.md → "Download workflow").
2. Locate the `## Review Comments` heading at the bottom of the markdown body.
   If the heading is absent, tell the user the doc has no comments to discuss and
   ask whether to retry or end the session — do not invent comments.
3. Parse each list item under that heading as one comment. The expected shape is:
   `- [<section reference>] <comment body>` — the section reference is optional.
4. Capture the body of the doc above the heading separately. The body is the
   reference text used when discussing each comment; do not modify it here.

See REFERENCE.md → "Comment extraction" for the parsing logic and edge cases.

> **Note:** the current setup intentionally avoids Google Docs' built-in comment
> threads. The MCP Drive tools available in this project are file-level only
> (no `comments.list` / `replies.list`), so all annotations live in the body of
> the markdown. Revisit this if/when a comments-capable tool is wired in.

### Step 3 — Orient the developer

Before discussing any comment, output a summary:

```
📥 Comments pulled from Google Drive

File:     <filename>
Comments: <N> open thread(s)

I'll walk through them one at a time, in document order.
For each one, I'll share my take and we'll decide together whether
the spec needs to change. Ready? Here's the first one.
```

Then immediately present Comment 1. Do not wait for a "yes" — momentum matters.

### Step 4 — Discuss comments one at a time

For each comment thread, in document order:

**4a. Present the comment**

```
── Comment <N> of <total> ──────────────────────────────────

Section: <section reference, or "(unscoped)" if none provided>

Your note: "<comment body>"
```

If the comment includes a section reference, quote the relevant snippet from the
body of the doc (use the markdown captured in Step 2.4) so the user has context
without re-opening the tablet. Keep the snippet to ~3 lines.

**4b. Give your analysis**

Respond as a collaborator, not a transcription service. For each comment:
- State what you think the developer is flagging (interpret the intent, not just the words)
- Give your recommendation: agree with the concern / push back with reasoning / flag
  it as a scope question
- If the comment suggests a spec change, sketch what the change would look like

**4c. Resolve the comment**

Wait for the developer's response, then categorize the outcome:

| Developer says | Outcome |
|---|---|
| "update the spec" / "agreed" | Log as `CHANGE: <description>` |
| "discard" / "never mind" / "skip" | Log as `SKIP` |
| "not now" / "defer" | Log as `DEFER: <description>` — add to Out of Scope |
| Continued discussion | Keep discussing until one of the above is reached |

Do not move to the next comment until the current one is resolved.

**4d. Transition**

After logging the outcome, bridge to the next comment with a single sentence, then
present it. Example: "Got it — flagging that for the Out of Scope section. Next one:"

### Step 5 — Produce the resolution summary

After all comments are resolved, show:

```
── Review Complete ──────────────────────────────────────────

Changes agreed:  <N>
Skipped:         <N>
Deferred:        <N>

Agreed changes:
  1. <CHANGE description>
  2. <CHANGE description>

Deferred to Out of Scope:
  1. <DEFER description>

Shall I apply these changes to the spec now?
```

Wait for explicit confirmation before touching the spec file.

### Step 6 — Apply changes and re-present

If the developer confirms:

1. Apply each agreed `CHANGE` to the local spec file.
2. Move each `DEFER` item into the spec's **Out of Scope** section.
3. Re-present the updated spec in full.
4. End with:

> Spec updated. Review the changes above. Reply "approved" to proceed to `/implement`,
> or tell me what else to adjust.

### Step 7 — Cleanup (optional, on request)

If the developer says "clean up" or "delete the Drive file":
- Delete the file from `Banderas/review/` in Google Drive.
- Confirm deletion.

Do not delete automatically — these are the developer's files.

---

## Rules

- **Never push without being asked.** The agent offers this at spec completion; it
  does not invoke it automatically.
- **Never apply spec changes without explicit "yes".** Discussion is not approval.
- **Never skip a comment.** If a comment is unclear, ask for clarification before
  moving on — do not assume intent.
- **One comment at a time.** Do not front-load the list. The conversation IS the review.
- **Only the `## Review Comments` section is read.** Anything the developer
  changes in the body of the doc will be lost on the next round-trip. If they
  mention an edit they made inline, remind them of the annotation convention
  and ask them to re-state it as a list item under Review Comments.
- **Google Docs' built-in comment threads are not supported in the current
  setup.** If the developer adds a Drive comment, it will not appear on read.
  Remind them to use the inline convention instead.
