---
name: tdd
description: Run a red-green-refactor TDD loop for .NET projects using xUnit, Moq, FluentAssertions, WebApplicationFactory, and TestContainers. Selectively applies TDD where it adds the most design signal. Use when the user says "TDD", "write the test first", "test-driven", or is adding domain logic, algorithms, or fixing a bug.
---

# TDD

Write the test first. Let the failing test drive the implementation. Stop when it passes and clean up.

See [REFERENCE.md](REFERENCE.md) for the full tooling stack, naming conventions, and .NET-specific patterns.

## When to use TDD

**Use TDD for:**
- Domain logic and invariants (guard clauses, state transitions, business rules)
- Algorithms with clear inputs and outputs (rollout percentages, evaluation logic)
- Bug fixes — write a failing test that reproduces the bug, then fix it
- New interfaces — the test forces you to design the API before you commit to it

**Don't bother for:**
- Exploratory or prototype code (figure out EF Core mappings, then test)
- HTTP pipeline plumbing (middleware order, DI setup) — use integration tests after
- Configuration and environment wiring

## The loop

```
1. RED    → Write the smallest failing test. Run it. Confirm it fails for the right reason.
2. GREEN  → Write the minimum code to make it pass. No more.
3. REFACTOR → Clean up duplication. Tests stay green throughout.
```

Repeat. One behaviour per cycle.

## Workflow

### 1. Explore the codebase first
Identify the layer and class under test. Read the existing test files to match established conventions (naming, directory structure, base classes).

### 2. Pick the right test type
- **Unit test** — domain layer, pure logic, no I/O. Use xUnit + Moq + FluentAssertions.
- **Integration test** — HTTP layer, full request pipeline. Use WebApplicationFactory.
- **Database test** — EF Core queries, repository behaviour. Use TestContainers.

### 3. Write the test shell
Name it: `MethodOrBehaviour_Scenario_ExpectedOutcome`
Structure it: Arrange → Act → Assert. One assertion per test.

### 4. Run and confirm RED
The test must fail. If it passes immediately, the test is wrong or already covered.

### 5. Write minimum GREEN code
No speculative logic. No handling cases the tests haven't asked for yet.

### 6. Refactor
Remove duplication. Extract well-named helpers. Never change behaviour.
