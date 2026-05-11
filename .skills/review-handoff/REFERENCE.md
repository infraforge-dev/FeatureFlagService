# review-handoff — Reference

API patterns and parsing logic for the Push and Read modes. The skill body
(SKILL.md) defines the user-facing flow; this file pins the exact tool calls.

---

## Tools used

The skill relies on the `claude.ai Google Drive` MCP server. The tool surface
is file-level only — there is no `comments.list`, which is why the
inline-comment convention in CONVENTIONS.md exists.

| MCP tool | Used in | Purpose |
|---|---|---|
| `mcp__claude_ai_Google_Drive__search_files` | Push, Read | Resolve `Banderas/review/` to a folder ID; find the doc by title |
| `mcp__claude_ai_Google_Drive__create_file` | Push | Upload markdown as a Google Doc |
| `mcp__claude_ai_Google_Drive__get_file_metadata` | Push, Read | Get the `webViewLink` to share with the developer; confirm mime type |
| `mcp__claude_ai_Google_Drive__read_file_content` | Read | Pull the doc body back as text |

---

## Resolving the destination folder

`create_file` takes a `parentId`, not a path. Before any upload, resolve
`Banderas/review/` to a folder ID. Cache the result in conversation memory for
the rest of the session — it does not change.

Two-step search (folder → subfolder):

```
search_files(query="title = 'Banderas' and mimeType = 'application/vnd.google-apps.folder'")
  → take the first result's id, call it BANDERAS_ID

search_files(query="title = 'review' and mimeType = 'application/vnd.google-apps.folder' and parentId = '<BANDERAS_ID>'")
  → take the first result's id, call it REVIEW_FOLDER_ID
```

If either lookup returns no results, stop and ask the developer to create the
folder in Drive (or supply the ID directly). Do not silently create folders.

---

## Upload workflow (Push mode)

Goal: take a local markdown file and produce a Google Doc the developer can
open and annotate on a tablet.

### Step 1 — Read the local file

Use the standard Read tool to load the spec into memory as a single UTF-8
string. Strip trailing whitespace but preserve all markdown syntax verbatim.

### Step 2 — Check for a name collision

Before uploading, search for an existing file with the same title under the
review folder:

```
search_files(query="title = '<filename>' and parentId = '<REVIEW_FOLDER_ID>'")
```

If a result exists, stop and ask the developer whether to overwrite. Do not
delete or update silently. (Overwrite path: delete the old file via a separate
tool call if confirmed, then continue.)

### Step 3 — Upload with markdown conversion

```
create_file(
  title = "<filename>"          # plain filename, no path
  parentId = "<REVIEW_FOLDER_ID>"
  textContent = <markdown body>
  contentMimeType = "text/markdown"
)
```

**Conversion behavior — needs a smoke test.** The MCP tool documentation only
lists `text/plain` and `text/csv` as auto-converted to Google formats. Drive
itself has supported `text/markdown` → `application/vnd.google-apps.document`
conversion since July 2024 (Workspace launch), so passing `text/markdown` in
`contentMimeType` *should* produce a Google Doc — but verify on first run.

If the upload lands as a plain `.md` file in Drive (mime type
`text/markdown` instead of `application/vnd.google-apps.document`), fall back
to `contentMimeType = "text/plain"` — Drive will convert plain text to a Doc
but you will lose markdown formatting in the rendered Doc body. Flag this to
the developer and consider it a setup task to fix.

### Step 4 — Get the share URL

The `create_file` response includes the new file's id. Fetch metadata to get
the share link:

```
get_file_metadata(fileId = "<new_id>")
  → use the webViewLink field
```

This URL is what gets surfaced in the "📤 Sent to Google Drive" block in
SKILL.md → Step 3.

---

## Download workflow (Read mode)

Goal: pull the annotated Doc back as text the agent can parse.

### Step 1 — Locate the file

If the developer's session is unbroken from the Push, reuse the file id from
that step. Otherwise, search:

```
search_files(query="title = '<filename>' and parentId = '<REVIEW_FOLDER_ID>'")
```

If multiple matches, list them and ask which to read.

### Step 2 — Read the content

```
read_file_content(fileId = "<file_id>")
```

**Format caveat.** The MCP tool description says: *"The text representation
will change over time, so don't make assumptions about the particular format
of the text returned by this tool."*

In practice this returns a natural-language flattening of the Doc — usually
close to markdown, but not guaranteed. The parser in the next step has to
tolerate at minimum: `## Review Comments` becoming `Review Comments` (no
hashes), list items rendered as `- ` or `• ` or numbered, and arbitrary
whitespace.

If the returned text has no clear "Review Comments" delimiter, halt and tell
the developer the doc has no comments — do not invent structure.

---

## Comment extraction

Parse the text returned by `read_file_content` into:

1. **Body** — everything before the Review Comments delimiter. Preserved
   for snippet quoting in SKILL.md → Step 4a, never edited here.
2. **Comments** — a list of objects with optional `section` and required
   `body`.

### Delimiter detection

Match any of these patterns at the start of a line, case-insensitive:

```
##\s*Review Comments         # markdown round-trip with heading preserved
Review Comments              # heading flattened to plain text
```

Treat the first match as the delimiter. Everything after it is comment
territory.

### Comment shape

Each comment is one list item. Match list-item starts:

```
^[-*•]\s+      # markdown bullet, possibly bullet-character
^\d+[.)]\s+    # numbered list (in case the export numbers them)
```

Within each item, look for an optional section reference at the start:

```
\[(.+?)\]\s*(.*)
  group 1 = section name
  group 2 = comment body
```

If no `[...]` prefix is present, leave `section = null` and use the entire
item body.

### Edge cases to handle

| Case | Behavior |
|---|---|
| Multi-line comment (continuation indented under the bullet) | Concatenate continuation lines into the body until the next bullet |
| Empty list item (`- `) | Skip silently |
| Item with only a section reference and no body (`- [foo]`) | Treat as ambiguous — surface to the developer and ask what they meant before discussing |
| Comments contain markdown formatting (links, code spans) | Preserve as-is when echoing back |
| Two `## Review Comments` headings in the doc | Use the first; warn the developer the second is being ignored |

---

## Step 6 — Apply changes (writeback)

When the developer approves spec changes, the agent edits the **local** spec
file (`Docs/decisions/<branch>/spec.md`) using normal Edit tool calls.

The Drive copy is **not** updated — it is treated as a one-shot review
artifact. The developer can re-push if they want a fresh round.

---

## Step 7 — Cleanup

Deletion uses the Drive tool surface (no MCP `delete_file` is available in
the current toolset — flag this if the developer requests cleanup). For now,
instruct them to delete the file manually from Drive UI, or implement
deletion via a follow-up MCP/shell wrapper.

---

## Smoke-test checklist (run once before relying on the skill)

1. Push a small spec to Drive. Confirm the resulting file in Drive UI is a
   Google Doc (not a `.md` file with a markdown icon).
2. Open it, append a `## Review Comments` section with one
   `- [test] this is a test comment` item, save (auto-saves).
3. Run `/review-handoff --read`. Confirm the agent finds the comment and
   surfaces it as `Section: test, Your note: "this is a test comment"`.
4. Reply with each of `agreed`, `discard`, `defer` to verify the resolution
   logging works.

If step 1 fails (file lands as `.md`), the `text/markdown` conversion isn't
flowing through the MCP — fall back to `text/plain` and document the gap.
