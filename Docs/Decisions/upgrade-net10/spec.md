# .NET 10 Upgrade — Implementation Spec

**Branch:** `refactor/upgrade-net10`  
**Phase:** 0 — Pre-Phase 1 housekeeping  
**Status:** Ready for implementation  
**Estimated scope:** Small — two files changed, five `.csproj` files updated
**Implementation notes:** `docs/Decisions/upgrade-net10/implementation-notes.md`
**PR:** —

---

## 1. Purpose of This Document

This spec covers the full upgrade from .NET 9 to .NET 10 across the devcontainer
configuration and all project files. It is intentionally scoped — nothing else changes
in this session.

> **SCOPE**  
> This spec covers: `devcontainer.json` base image swap and `dotnet` feature update,
> and `<TargetFramework>` update in all five `.csproj` files.  
> It does **not** cover code changes, new features, EF Core, or controllers.

---

## 2. Background — Why This Is Needed

The current devcontainer uses:

```jsonc
"image": "mcr.microsoft.com/devcontainers/dotnet:9.0"
```

There is no `devcontainers/dotnet:10.0` tag. The workaround already in place —
`"ghcr.io/devcontainers/features/dotnet:2": { "version": "10.0" }` — installs
the .NET 10 SDK on top of the .NET 9 base image. This is why `dotnet --version`
inside the container already reports `10.0.201`.

The problem is the base image is still .NET 9 underneath, and all five `.csproj`
files still declare `<TargetFramework>net9.0</TargetFramework>`. This means:

- NuGet package resolution targets net9.0 but the SDK is 10.0 — version drift
- Packages added during the evaluation engine session resolved to `10.0.5`
  despite the projects targeting `net9.0`
- .NET 9 reaches end-of-life in May 2026

**The fix:** Swap the base image to a plain Ubuntu 24.04 image and let the existing
`dotnet` feature own the .NET installation entirely. Then update all `.csproj` files
to target `net10.0`.

---

## 3. Architecture Context

This change touches infrastructure only — no Clean Architecture layers are affected.
No application code changes. No interface changes. No new classes.

The only files that change:

```
.devcontainer/devcontainer.json    ← base image swap
FeatureFlag.Api/FeatureFlag.Api.csproj
FeatureFlag.Application/FeatureFlag.Application.csproj
FeatureFlag.Domain/FeatureFlag.Domain.csproj
FeatureFlag.Infrastructure/FeatureFlag.Infrastructure.csproj
FeatureFlag.Tests/FeatureFlag.Tests.csproj
```

---

## 4. Change 1 — devcontainer.json

### 4.1 Base Image

**Current:**

```jsonc
"image": "mcr.microsoft.com/devcontainers/dotnet:9.0",
```

**Replace with:**

```jsonc
"image": "mcr.microsoft.com/devcontainers/base:ubuntu-24.04",
```

> **WHY `devcontainers/base` and not `dotnet/sdk:10.0`**  
> `mcr.microsoft.com/dotnet/sdk:10.0` is a production SDK image — it has no VS Code
> dev tooling, no `vscode` user, no common utilities. Using it as a devcontainer base
> would require a custom Dockerfile to layer all of that back in.  
> `mcr.microsoft.com/devcontainers/base:ubuntu-24.04` is a purpose-built devcontainer
> base — it has the `vscode` user, git, common utilities, and everything VS Code expects.
> The `dotnet` feature then installs .NET 10 on top. This is the correct pattern for
> feature-driven devcontainer setups.

---

### 4.2 dotnet Feature — Make the Version Explicit

The `dotnet` feature entry already exists in `devcontainer.json`. It currently reads:

```jsonc
"ghcr.io/devcontainers/features/dotnet:2": {
    "version": "10.0"
}
```

This is already correct. **Do not change it.** It is listed here for confirmation only.

---

### 4.3 Full Updated devcontainer.json

Replace the entire contents of `.devcontainer/devcontainer.json` with the following.
Every property is preserved from the original — only the `image` value changes.

```jsonc
{
  "name": "Feature Flag Service",
  "image": "mcr.microsoft.com/devcontainers/base:ubuntu-24.04",
  "features": {
    "ghcr.io/anthropics/devcontainer-features/claude-code:1.0": {},
    // Node is required at runtime by Claude Code
    "ghcr.io/devcontainers/features/node:1": {
      "version": "lts"
    },
    "ghcr.io/devcontainers/features/github-cli:1": {},
    // Installs .NET 10 SDK
    "ghcr.io/devcontainers/features/dotnet:2": {
      "version": "10.0"
    }
  },
  "forwardPorts": [
    5000,
    5001,
    10000
  ],
  "portsAttributes": {
    "5000": {
      "label": "HTTP",
      "onAutoForward": "notify"
    },
    "5001": {
      "label": "HTTPS",
      "onAutoForward": "notify"
    },
    "10000": {
      "label": "Claude Auth",
      "onAutoForward": "silent"
    }
  },
  // Keeps Claude auth + config between container rebuilds
  "mounts": [
    // Bind from host instead of an isolated volume
    "source=${localEnv:HOME}/.claude,target=/home/vscode/.claude,type=bind",
    "source=${localEnv:HOME}/.claude.json,target=/home/vscode/.claude.json,type=bind"
  ],
  "containerEnv": {
    "CLAUDE_CONFIG_DIR": "/home/vscode/.claude",
    "ASPNETCORE_ENVIRONMENT": "Development"
  },
  // Both must match to avoid file permission issues
  "remoteUser": "vscode",
  "containerUser": "vscode",
  "postCreateCommand": "dotnet restore FeatureFlagService.sln && dotnet tool restore",
  // Git 2.35+ throws ownership warnings in containers — this silences them
  "postStartCommand": "git config --global --add safe.directory ${containerWorkspaceFolder}",
  "customizations": {
    "vscode": {
      "extensions": [
        "ms-dotnettools.csharp",
        "ms-dotnettools.csdevkit",
        "humao.rest-client",
        "eamodio.gitlens",
        "editorconfig.editorconfig",
        "streetsidesoftware.code-spell-checker",
        "csharpier.csharpier-vscode"
      ],
      "settings": {
        "editor.formatOnSave": true,
        "editor.insertSpaces": true,
        "editor.tabSize": 4,
        "files.eol": "\n",
        "dotnet.defaultSolution": "FeatureFlagService.sln"
      }
    }
  }
}
```

> **NOTE — What was removed**  
> The original `devcontainer.json` had a second `dotnet` feature entry:
> ```jsonc
> "ghcr.io/devcontainers/features/dotnet:2": {
>     "version": "10.0"
> }
> ```
> listed separately from the one that was already there. There should only be **one**
> entry for this feature. The version `"10.0"` is the one to keep. Confirm there is
> no duplicate in the file before saving.

---

## 5. Change 2 — Target Framework in All .csproj Files

Update `<TargetFramework>` in all five project files.

**Find this line in each file:**

```xml
<TargetFramework>net9.0</TargetFramework>
```

**Replace with:**

```xml
<TargetFramework>net10.0</TargetFramework>
```

### Files to update:

| File | Current | Target |
|---|---|---|
| `FeatureFlag.Api/FeatureFlag.Api.csproj` | `net9.0` | `net10.0` |
| `FeatureFlag.Application/FeatureFlag.Application.csproj` | `net9.0` | `net10.0` |
| `FeatureFlag.Domain/FeatureFlag.Domain.csproj` | `net9.0` | `net10.0` |
| `FeatureFlag.Infrastructure/FeatureFlag.Infrastructure.csproj` | `net9.0` | `net10.0` |
| `FeatureFlag.Tests/FeatureFlag.Tests.csproj` | `net9.0` | `net10.0` |

No other changes to any `.csproj` file. Package references, project references, and
all other properties stay exactly as they are.

---

## 6. Verification Steps

After making all changes, run these commands in order inside the devcontainer:

```bash
# 1. Confirm the SDK version
dotnet --version
# Expected output: 10.x.x

# 2. Build the solution
dotnet build FeatureFlagService.sln
# Expected: Build succeeded. 0 Warning(s). 0 Error(s).

# 3. Run the tests
dotnet test FeatureFlagService.sln
# Expected: All tests pass
```

If the build fails after the framework upgrade, check whether any NuGet packages
have version constraints that prevent resolution under `net10.0`. Run
`dotnet restore FeatureFlagService.sln` and read the output carefully before
attempting any package version changes.

---

## 7. What NOT to Do in This Session

- Do not change any application code
- Do not add or remove NuGet packages unless the build explicitly fails
  and the error points to a package incompatibility
- Do not modify any interfaces, entities, strategies, or services
- Do not touch `Program.cs` beyond what the build requires
- Do not start the EF Core or controller work — that is a separate session

---

## 8. After the Build Passes

Update `docs/current-state.md`:

- Mark **KI-001** as resolved
- Update the "What Is Completed" section to reflect `net10.0` and the new
  devcontainer base image

Commit message format:

```
refactor: upgrade solution to net10.0 and update devcontainer base image

- Swap devcontainer base image from devcontainers/dotnet:9.0
  to devcontainers/base:ubuntu-24.04
- dotnet feature already targets 10.0 — no change needed
- Update TargetFramework to net10.0 in all five .csproj files
- Resolves KI-001 (net9/net10 version mismatch)
```

---

## 9. Instructions for Claude Code

Read the following before making any changes:

- `CLAUDE.md`
- `docs/current-state.md` — see KI-001
- This file

Then implement in this order:

1. Update `.devcontainer/devcontainer.json` — swap base image per section 4.3
2. Update all five `.csproj` files — change `net9.0` to `net10.0` per section 5
3. Run `dotnet build FeatureFlagService.sln` — confirm 0 errors, 0 warnings
4. Run `dotnet test FeatureFlagService.sln` — confirm all tests pass
5. Update `docs/current-state.md` — mark KI-001 resolved

> **DO NOT**  
> Do not change any application code, interfaces, or services.  
> Do not add or remove NuGet packages unless a build error requires it.  
> Do not start EF Core or controller work.  
> If the container rebuild fails, report the error — do not attempt to fix it
> by modifying unrelated files.

---

*FeatureFlagService | refactor/upgrade-net10 | Phase 0 housekeeping*
