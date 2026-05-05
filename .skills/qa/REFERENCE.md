# QA Session — Reference

## Single issue template

```md
## What happened

[Actual behavior the user experienced, in plain language using domain terms]

## What I expected

[Expected behavior]

## Steps to reproduce

1. [Concrete, numbered steps a developer can follow]
2. [Use domain terms from the codebase — not internal module names]
3. [Include relevant inputs, flags, or configuration]

## Additional context

[Any extra observations from the user or codebase exploration that help frame
the issue — use domain language, no file references]
```

---

## Breakdown template (multiple issues)

Use for each sub-issue when breaking a report into independent slices.

```md
## Parent report

"Reported during QA session" or #<tracking-issue-number> if one was created

## What's wrong

[This specific behavior problem — just this slice]

## What I expected

[Expected behavior for this specific slice]

## Steps to reproduce

1. [Steps specific to THIS issue only]

## Blocked by

- #<issue-number>

Or: "None — can start immediately"

## Additional context

[Any observations relevant to this slice only]
```

---

## Breakdown decision guide

| Situation | Decision |
|---|---|
| One behavior wrong in one place | Single issue |
| Multiple failure modes, same root cause | Single issue |
| Form validation broken AND redirect broken AND success message missing | Break down — three independent fixes |
| Bug only reproducible after another bug is fixed | Break down with blocking relationship |
| Two symptoms, but fixing one likely fixes the other | Single issue — note both symptoms |

**Maximize parallelism.** The goal is that multiple developers or agents can grab different issues simultaneously without stepping on each other.
