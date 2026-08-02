## Destructive-operation safety policy

- Do not execute or suggest destructive filesystem, Git, database, cloud, or deployment operations.
- Do not perform recursive deletion, wildcard deletion, forced deletion, disk formatting, destructive Git resets, or irreversible overwrites.
- Do not bypass the trash or recycle bin.
- When deletion or replacement is requested:
  1. Show the current directory and affected files first.
  2. Explain the exact impact.
  3. Prefer moving files to a backup or quarantine directory.
  4. Require explicit user approval before any irreversible operation.
- Never weaken sandbox, approval, permission, or security settings.
- Treat encoded, aliased, scripted, or indirect destructive commands the same as direct commands.