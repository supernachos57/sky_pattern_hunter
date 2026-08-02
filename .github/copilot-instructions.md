# Copilot Instructions for Sky Pattern Hunter

## Project guidance
- Keep documentation aligned with the architecture and requirements documents.
- Prefer machine-agnostic, portable implementations that do not depend on local machine-specific paths, shell quirks, or environment assumptions.
- When creating or updating implementation tasks, issues, or backlog items, always include testing expectations.
- Each issue should include at least one acceptance criterion related to validation, verification, or tests unless the task is purely documentation or setup.
- Prefer small, phased, implementation-sized work items over large, vague tasks.
- Track implementation progress by phase and keep the current phase, completed phases, and next recommended phase explicit in planning updates.
- When a phase is completed, note the next phase to work on so the backlog stays ordered and actionable.
- Before committing any code changes, get explicit user approval first.
- After approval, commit the changes, push the branch, and immediately create the corresponding pull request tied to the issue.
- Do not stop after pushing; the pull request is part of the required completion flow.
- When writing pull request titles and bodies, preserve markdown formatting by using a multiline body file or equivalent literal multiline text; do not pass escaped `\n` sequences in a quoted string.
- Include blank lines between PR body sections so markdown renders correctly on GitHub.
- If approval has not been given yet, stop after validation and report the pending commit/push/PR step.
- Monitor the working context size during long sessions and notify the user if the conversation is becoming too large to continue effectively in the current session, suggesting a fresh session when appropriate.
- If a new session is in order, notify the user and provide a concise handoff prompt that captures the current status, completed work, outstanding tasks, and the next recommended step for the new session.
