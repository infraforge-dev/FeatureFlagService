# CI AI Reviewer — Implementation Notes

**Session date:** 2026-03-31
**Branch:** `feature/ci-ai-reviewer`
**Spec reference:** `Docs/Decisions/ci-ai-reviewer - PR#35/spec-v2.md`
**PR:** #35

---

## Files Delivered

| File | Action |
|---|---|
| `.github/workflows/ci.yml` | Modified — commented stub replaced with full `ai-review` job |
| `.github/prompts/ai-review-system.md` | Created — system prompt read at runtime by the workflow |

No C# files changed. No changes to `src/` or `tests/`.

---

## Deviations from Spec

None. The implementation follows spec-v2.md exactly.

---

## Minor Observations (carried forward from SWE review — not blockers)

These are low-priority design notes identified during the spec review that were
not addressed in v1.1. They do not affect correctness. Tracked here for future
consideration.

### OBS-1 — `approved` Field in Step 6 Validation ✅ Resolved

**Original concern:** Step 6 required `HAS_APPROVED` but the field was described as
"ignored by the workflow," creating a fragile dependency.

**Resolution (this session):** `HAS_APPROVED` removed from Step 6 validation. `approved`
field removed from system prompt schema entirely. Step 6 now validates only `summary`
and `issues`. Flagged by the AI reviewer itself (warning, line 207 run #2, line 265 run #3).

---

### OBS-2 — `try/catch` Around `JSON.parse` ✅ Resolved

**Original concern:** Steps 7b and 8 called `JSON.parse` without try/catch guards.

**Resolution (this session):** Both steps now wrap `JSON.parse` in try/catch. Step 7b
posts the unavailability notice on parse failure; Step 8 logs a warning and returns.
Flagged by the AI reviewer (warning, line 260) on first successful run.

---

### OBS-3 — No PR Comment Example for "Suggestions Only + APPROVE" State

The spec's PR Comment Format section shows two examples:
1. Issues found (with error + suggestion rows) → `REQUEST_CHANGES`
2. No issues → `APPROVE`

A third state exists due to the derived-approval logic: suggestions present,
review still `APPROVED`. A developer will see a blue-entries issues table alongside
an `APPROVED` review state. This is correct behavior but may be surprising.

**If documentation is desired:**
Add a third example to the PR Comment Format section in the spec showing a
suggestion-only PR with an `APPROVED` review state.

---

### OBS-4 — Pipe Characters in Claude's Response Can Break Markdown Table

**Flagged by:** AI reviewer (error, line 300, run #3)

`issue.comment` and `issue.file` were rendered directly into Markdown table cells
without escaping. A `|` in Claude's output breaks column alignment; a newline
corrupts the row boundary.

**Resolution (this session):** `escapeCell()` helper added in Step 7b — replaces `|`
with `\|` and collapses `\n` to a space before table interpolation.

---

### OBS-5 — `sed` Fence Stripping Pattern Too Broad

**Flagged by:** AI reviewer (warning, line 258, run #3)

`sed '/^```/d'` deletes any line starting with three backticks, including lines
inside a JSON string value that reference code blocks.

**Resolution (this session):** Pattern narrowed to `sed '/^```[a-zA-Z]*[[:space:]]*$/d'`
— matches only fence-marker-only lines (``` or ```json etc.), not lines with content
after the fence.

---

### OBS-6 — DIFF Content Visible in Actions Log

**Flagged by:** AI reviewer (warning, line 233, run #3)

Step outputs set via `core.setOutput()` are stored in the runner environment file and
visible in the Actions log by default. A diff containing accidentally committed secrets
(e.g. `.env` file) would appear in the log.

**Mitigation:** This is an inherent limitation of the approach. Cannot be fully mitigated
without using restricted-access artifacts. Document in Known Constraints and ensure
`.gitignore` covers secret files. Tracked for Phase 8 hardening.

---

### OBS-7 — `listReviews` Missing Pagination

**Flagged by:** AI reviewer (suggestion, line 155, run #3)

`listReviews` called without `per_page` — defaults to 30. A long-lived PR with many
re-runs could accumulate more than 30 reviews; earlier bot reviews would not be dismissed.

**Resolution (this session):** Added `per_page: 100` to the `listReviews` call.

---

### OBS-8 — Missing `--connect-timeout` on curl

**Flagged by:** AI reviewer (warning, line 244, run #3)

`--max-time 60` bounds total transfer time but not connection establishment. A hung
connection would wait indefinitely.

**Resolution (this session):** Added `--connect-timeout 10` alongside `--max-time 60`.

---

### OBS-9 — Model Slug Mismatch Between API Call and Footer ✅ Resolved

**Flagged by:** AI reviewer (error, line 228, run #3 and run #4)

Step 5 called `claude-sonnet-4-5` while the Step 7b footer credited `claude-sonnet-4-6`.
Initially dismissed as a knowledge-cutoff false positive, but the mismatch was real.

**Resolution (this session):** Model slug in Step 5 corrected to `claude-sonnet-4-6`
to match the footer and the current intended model.

---

## Build Verification

Run after implementation to confirm no YAML syntax errors:

```bash
# Verify ci.yml parses cleanly (requires actionlint or yamllint)
yamllint .github/workflows/ci.yml

# Verify system prompt file exists and is non-empty
cat .github/prompts/ai-review-system.md | wc -l
```

Pre-merge manual checklist:

- [ ] `ANTHROPIC_API_KEY` secret added to GitHub repo
  (`Settings → Secrets and variables → Actions → New repository secret`)
- [ ] `ai-review` label created in the GitHub repo
  (`Issues → Labels → New label`)
- [ ] First run triggered by adding `ai-review` label to a test PR
- [ ] Confirm job appears as skipped on PRs without the label
- [ ] Confirm fail-open path fires with an invalid API key (AC-6)
