# Agentic Coding Template

A starter repository for projects that use AI agents (Cursor, Copilot, Claude Code, etc.) with consistent rules, skills, and documentation layout.

## What you get

- **`.agents/rules/`** — Universal guardrails: scope, code quality, git commits, pull requests, and template adoption.
- **`.agents/skills/`** — Optional, stack-specific guidance loaded when relevant (includes **dotnet** for C#/.NET).
- **`AGENTS.md`** — Entry point for agents; index of rules and skills.
- **`REQUIREMENTS.md`** — Living product requirements document for agents to maintain.

## Getting started

1. Use this repo as a [GitHub template](https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-repository-from-a-template) or copy its structure into a new project.
2. Replace this README with your project overview.
3. Fill in `REQUIREMENTS.md` with your product context.
4. Read `.agents/rules/adopting-this-template.md` for tailoring rules and skills to your stack.
5. Remove skills you do not need; add new ones under `.agents/skills/<name>/`.

## Adding a new language or stack

Create `.agents/skills/<stack>/SKILL.md` with YAML frontmatter (`name`, `description`) and link it from `AGENTS.md`. See the existing `dotnet` skill as a reference.

## Lod Softworks

This template is maintained by [Lod Softworks](https://github.com/lod-softworks). The `dotnet` skill encodes Lod-specific C# conventions; other stacks can follow the same skill pattern with your own defaults.
