# review-handoff — Annotation Conventions

These are the rules for annotating a doc that has been pushed to Drive via
`/review-handoff`. Read this before opening the doc on the tablet — the agent
follows these conventions exactly when pulling comments back.

## The short version

1. Append a `## Review Comments` heading to the bottom of the doc.
2. Each comment is one list item:
   `- [<section name>] <your note>`
3. Don't edit anything above the heading. Don't use Google Docs' comment threads.

## The long version

### What the agent reads

When you run `/review-handoff --read`, the agent downloads the doc as markdown
(via Drive's native `text/markdown` export) and looks for one section:

```markdown
## Review Comments
```

Everything below that heading is parsed as a list of comments. Everything
above it is treated as the unmodified spec body — used for context when
discussing each comment, but never edited as part of the round-trip.

### Comment shape

Each comment is one Markdown list item. The recommended shape:

```markdown
- [<section name>] <your note in plain prose>
```

The square-bracket prefix is optional but strongly recommended — it lets the
agent quote the relevant snippet from the body when discussing the comment, so
you don't have to flip back to the tablet.

Examples:

```markdown
## Review Comments

- [Background and Goals] this section reads like marketing copy — what's the
  actual user pain point?
- [Acceptance Criteria] AC #3 is too vague. What does "consistent" mean in
  numerical terms?
- the Definition of Done is missing a CSharpier check.
```

The third comment has no section reference; the agent will mark it as
`(unscoped)` and discuss it last (or wherever you raise it).

### What NOT to do

| Don't | Why |
|---|---|
| Use Google Docs' built-in comment / suggestion threads | The MCP Drive tools available in this project are file-level only — `comments.list` is not wired up. Comment threads will be invisible on read. |
| Edit the body of the doc | The agent treats the body as immutable reference text. Any edits you make there will be silently overwritten when the spec is updated locally. |
| Use handwriting / ink / drawings | Not preserved on `text/markdown` export. |
| Put comments in the middle of the body | The agent only parses what's under `## Review Comments`. Mid-body notes will be ignored. |
| Forget the `##` heading | Without the heading, the agent will tell you the doc has no comments and stop. |

### When in doubt

If you want to discuss something that doesn't fit the convention — a structural
rewrite suggestion, a request to split the spec, anything cross-cutting — write
it as a comment too:

```markdown
- [meta] this whole spec should be split into two phases — see my note in the
  next section.
```

The agent will treat it like any other comment and bring it up in order.

## Why this convention exists (temporary)

Drive comment threads would give you anchor-text and proper resolution
semantics. The current MCP server only exposes file-level operations, so we
trade fidelity for simplicity: comments live in the body of the markdown,
parsed by convention.

If a comments-capable MCP tool or shell wrapper gets added, the SKILL.md and
this file will be updated to use the native Drive comments API and the
inline-comment convention will be deprecated.
