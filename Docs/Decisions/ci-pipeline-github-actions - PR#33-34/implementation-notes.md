1 # CI Pipeline — GitHub Actions — Implementation Notes
        2
        3 **Session date:** 2026-03-30
        4 **Branches:** `feature/code-style-foundation` (PR #33), `feature/ci-core-pipeline` (PR #34)
        5 **Spec reference:** `Docs/Decisions/ci-pipeline-github-actions/spec-v3.md`
        6 **Build status:** Passed — 0 warnings, 0 errors
        7 **Tests:** 8/8 passing
        8 **PRs:** #33 (code style foundation), #34 (core CI pipeline)
        9
       10 ---
       11
       12 ## Deviations from Spec
       13
       14 ### DEV-001 — `cache: 'nuget'` Is Not a Valid Input for `actions/setup-dotnet@v4`
       15
       16 **Spec says:** `cache: 'nuget'` under `actions/setup-dotnet@v4`.
       17
       18 **What actually happened:** `actions/setup-dotnet@v4`'s `cache` input is a boolean (`true`/`false`), not a package manager string like `actions/setup-node`. Setting `cache: 'nuget'` causes a YAML schema validation error in CI.
       19
       20 **Fix applied:** Changed to `cache: true`. Then discovered a second issue: `cache: true` requires a `packages.lock.json` file, which this project does not have (`RestorePackagesWithLockFile` is not enabled). This caused a "Dependencies lock file i
          s not found" error on every run.
       21
       22 **Final resolution:** Removed `cache: true` entirely from both jobs. NuGet caching is deferred to Phase 2 when `RestorePackagesWithLockFile=true` is added to `Directory.Build.props` and `packages.lock.json` is committed. An inline comment in `ci.y
          ml` documents this.
       23
       24 ---
       25
       26 ### DEV-002 — `dotnet csharpier --check` Syntax Changed in CSharpier 1.x
       27
       28 **Spec says:** `dotnet csharpier --check .`
       29
       30 **What actually happened:** CSharpier 1.x changed to subcommand syntax. `--check` is not a valid flag — the correct form is `dotnet csharpier check .` (and `dotnet csharpier format .` to fix).
       31
       32 **Fix applied:** Updated `ci.yml` to use `dotnet csharpier check .`. An inline comment documents the 1.x syntax change.
       33
       34 ---
       35
       36 ### DEV-003 — Node.js 20 Deprecation Warning
       37
       38 **Spec does not address this.** GitHub Actions emits deprecation warnings for `actions/checkout@v4` and `actions/setup-dotnet@v4` because both currently run on Node.js 20, which is being retired on 2026-06-02.
       39
       40 **Fix applied:** Added `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24: true` at the workflow `env` level. This opts into Node.js 24 ahead of the forced migration without requiring action version bumps.
       41
       42 ---
       43
       44 ### DEV-004 — `GenerateDocumentationFile` Required for IDE0005
       45
       46 **Spec does not address this.** `EnforceCodeStyleInBuild=true` in `Directory.Build.props` activates IDE0005 (remove unnecessary usings), but IDE0005 requires the XML documentation file to be generated to resolve cross-assembly `<see cref="..."/>`
          references. Without it, `dotnet build` emits a `EnableGenerateDocumentationFile` warning that is promoted to an error by `TreatWarningsAsErrors=true`.
       47
       48 **Fix applied:** Added `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and `<NoWarn>1591</NoWarn>` (suppresses CS1591 "missing XML comment for public member") to `Directory.Build.props`.
       49
       50 ---
       51
       52 ### DEV-005 — Migration Files Triggered Roslyn Analyzer Violations
       53
       54 **Spec does not address this.** EF Core migration files (`Migrations/**`) are auto-generated and contain patterns that violate several Roslyn rules (IDE0161, IDE0005, CA1861). These are promoted to errors by `TreatWarningsAsErrors=true`.
       55
       56 **Fix applied:** Added a `[**/Migrations/**]` section to `.editorconfig` with `generated_code = true`. This suppresses all Roslyn analysis on migration files without requiring per-file `#pragma` suppression or `<NoWarn>` entries.
       57
       58 ---
       59
       60 ### DEV-006 — CA1806 False Positive in Unit Tests
       61
       62 **Spec does not address this.** The exception-testing pattern `Action act = () => new Foo(...)` triggers CA1806 ("do not ignore method results") because the `Action` wrapper is unused aside from being passed to `Assert.Throws`. This is the standar
          d xUnit pattern, not a real violation.
       63
       64 **Fix applied:** Added `dotnet_diagnostic.CA1806.severity = none` to the `[*.Tests/**]` section of `.editorconfig`.
       65
       66 ---
       67
       68 ### DEV-007 — CSharpier and `dotnet format` Produce Conflicting Output
       69
       70 **Spec does not address this.** Running `dotnet format --diagnostics IDE0011 IDE0008` on some files produces line breaks and indentation that violates CSharpier's `printWidth: 100` rule, and vice versa. The two tools disagree on line wrapping for
          certain constructs.
       71
       72 **Resolution:** The workflow is: run `dotnet format` first (for Roslyn diagnostics), then run `dotnet csharpier format .` (CSharpier is the final authority on whitespace and line length). CSharpier's output is what gets committed. The build step e
          nforces Roslyn rules; the format check step enforces CSharpier. Both must pass.
       73
       74 ---
       75
       76 ### DEV-008 — IDE0011/IDE0008 Fixes Lost During Rebase Conflict Resolution
       77
       78 **What happened:** The `feature/ci-core-pipeline` branch was created before PR #33 (code style foundation) was merged to `dev`. The branch therefore contained unformatted source files. When CSharpier was run on the branch to fix CI failures, it fo
          rmatted those files. When the branch was later rebased onto `dev` (which had the PR #33 versions), conflicts arose in 5 files. Resolving with `--theirs` (origin/dev) correctly restored the CSharpier-formatted state but discarded the IDE0011/IDE000
          8 fixes that had been applied locally.
       79
       80 **Fix applied:** Re-ran `dotnet format Bandera.sln --diagnostics IDE0011 IDE0008` after the rebase, then re-ran `dotnet csharpier format .` to reconcile any line-length disagreements introduced by `dotnet format`.
       81
       82 ---
       83
       84 ## Key Decisions
       85
       86 ### CSharpier as Final Formatting Authority
       87
       88 CSharpier is the arbiter of all whitespace and line-length decisions. `dotnet format` handles Roslyn diagnostic fixes (braces, explicit types, unused usings) but must always be followed by `dotnet csharpier format .` before committing. The CI pipe
          line enforces this order implicitly: the build step runs `dotnet build` (Roslyn), and the format check step runs `dotnet csharpier check .` (CSharpier). Both must pass independently.
       89
       90 ### NuGet Caching Deferred
       91
       92 Enabling `cache: true` on `actions/setup-dotnet@v4` requires `packages.lock.json` to exist in the repo. Generating that file requires `RestorePackagesWithLockFile=true` in `Directory.Build.props`. This is a Phase 2 task — it involves committing th
          e lockfile and ensuring all developers restore with it enabled. Skipping it in Phase 1 was the right call: the lockfile error would have blocked every CI run.
       93
       94 ### `ai-review` Job Left as Commented Stub
       95
       96 Per spec, the AI reviewer job is commented out entirely. The stub preserves the intended structure (needs lint-format and build-test, requires `ANTHROPIC_API_KEY` and `pull-requests: write`) so the implementation is self-documenting when the time
          comes to activate it.
       97
       98 ---
       99
      100 ## Build Verification
      101
      102 - `dotnet build Bandera.sln --no-incremental -p:TreatWarningsAsErrors=true` → **0 errors, 0 warnings**
      103 - `dotnet csharpier check .` → **41 files checked, 0 violations**
      104 - `dotnet test Bandera.sln --filter "Category!=Integration"` → **8/8 passing**
