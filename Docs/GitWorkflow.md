# Git Workflow and Rollback

Use Git as the single source of truth for the project. This guide covers branching, tagging, and safe rollback.

## Branching model

- **main** — Stable branch. Only merge when the project builds and core features work. Prefer tagged commits for “known-good” states.
- **dev** (optional) — Active development. Integrate feature branches here; merge to `main` when ready for release.
- **feature/*** — Short-lived branches for specific features (e.g. `feature/hat-offset`, `feature/new-ui`). Branch from `main` or `dev`; merge back when done.

Suggested flow:

1. Create a feature branch from `main`: `git checkout -b feature/my-change main`
2. Commit and push; open a PR into `main` (or `dev` if you use it).
3. After review and CI checks, merge. Delete the feature branch after merge.

## Tagging “known-good” builds

Tag commits that represent a working build or milestone so you can return to them easily.

```bash
# Create an annotated tag (recommended)
git tag -a v1.0.0 -m "Quest build 2026-02-26"

# Push tags to remote
git push origin v1.0.0
# Or push all tags
git push origin --tags
```

Use semantic-style versions if you like (e.g. `v1.0.0`, `v1.1.0`) or date-based (e.g. `build-20260226`).

## How to rollback

### Roll back to a tagged commit (safe “point of truth”)

```bash
# List tags
git tag -l

# Checkout the tagged commit (detached HEAD — for viewing or building)
git checkout v1.0.0

# Or create a new branch from that tag to work from it
git checkout -b hotfix-from-v1 v1.0.0
```

After `git checkout <tag>`, you’re in detached HEAD state. To build that version, open Unity and build. To continue development from that tag, create a branch as above.

### Roll back `main` to a previous commit (rewrite history)

Only do this if you understand the impact and coordinate with others:

```bash
# Reset main to a specific commit (e.g. a tag)
git checkout main
git reset --hard v1.0.0
git push --force origin main   # Use with care; rewrites remote history
```

Prefer **revert** for shared branches if others have already pulled:

```bash
git revert <commit-hash> -m 1
git push origin main
```

### Discard local changes and match remote

```bash
git fetch origin
git checkout main
git reset --hard origin/main
```

For a clean reimport in Unity after a rollback, run the `Tools/clean_reimport` script (see repo README or PreCommitChecklist) and reopen the project.

## Summary

- Use **main** as the stable branch; tag **known-good builds** with `git tag -a`.
- Roll back by **checking out a tag** (`git checkout <tag>`) or by **revert** on shared branches.
- Avoid `git push --force` on `main` unless you explicitly need to rewrite history and have agreed with the team.
