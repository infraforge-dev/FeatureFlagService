---
name: codebase-trivia
description: Run an interactive trivia game that quizzes a developer on their own codebase. Generates graded questions across architecture, design patterns, DDD, testing, async, DI, and more — including Mermaid diagrams when relevant. Use when user says "trivia", "quiz me", "codebase trivia", or wants to test their understanding of the code they've written.
---

# Codebase Trivia

Quiz the developer on their own codebase. One question at a time. Grade each answer. Rotate topics to ensure broad coverage.

See [TOPICS.md](TOPICS.md) for the full topic list and what to probe per topic.
See [FORMATS.md](FORMATS.md) for question types, difficulty levels, and Mermaid diagram templates.

## Phase 1 — Orient (do this once per session)

### 1a. Read project docs first
Look for these files and read whichever exist:
- `CLAUDE.md` — architecture summary, conventions, known issues
- `docs/architecture.md` — layer design, key decisions
- `docs/roadmap.md` — what's planned vs built
- `README.md` — project overview
- `UBIQUITOUS_LANGUAGE.md` — domain glossary

### 1b. Drill into code selectively
Only if docs leave gaps. Prioritize:
- Entry points (`Program.cs`, `Startup.cs`)
- Domain layer (`/Domain/**`)
- Key service or handler classes
- Test files (reveals what's actually tested vs assumed)

Do NOT read the whole codebase. Friction during exploration is a signal — note it, it becomes a question.

### 1c. Ask the developer one setup question
"What difficulty would you like? **Rookie** (concepts), **Mid-level** (applied), or **Senior** (tradeoffs & gotchas)?"

Optionally: "Any topics you want to focus on or skip?"

---

## Phase 2 — Run the Quiz

Repeat this loop until the developer calls it or you've covered 8–10 questions:

### Each question turn:

1. **Pick a topic** from [TOPICS.md](TOPICS.md) — rotate, don't repeat the same topic twice in a row
2. **Pick a format** from [FORMATS.md](FORMATS.md) — vary between spot-the-bug, diagram, explain-your-decision, what-would-break
3. **Ground it in their code** — reference actual class names, method names, layer names from their project. Never ask generic textbook questions.
4. **Include a Mermaid diagram** when the topic involves flow, relationships, or sequence (see [FORMATS.md](FORMATS.md) for when and how)
5. **Show the question clearly**, then wait for the answer

### After each answer:

- **Grade it**: Correct / Partially correct / Incorrect
- **Explain why** — always, even for correct answers. The explanation is the learning.
- **Show the ideal answer** if they were wrong or partially right
- **Show the score** as `[X / Y questions]` at the bottom of each turn
- **Transition**: brief one-liner bridge into the next question topic

---

## Phase 3 — Wrap-up

After 8–10 questions (or when developer is done):

- Show final score with breakdown by topic
- Call out **one strength** (topic they handled well)
- Call out **one area to revisit** (topic with the weakest answer)
- Offer: "Want to drill deeper on [weak topic]?" — if yes, run 3 more targeted questions on that topic only