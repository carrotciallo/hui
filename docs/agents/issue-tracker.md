# Issue tracker: GitHub

Issues and specs for this repo live on **`sfher/hui`**. Use the `gh` CLI for operations.

## Repo targeting

Always pass `-R sfher/hui` explicitly — this working directory may not be a clone, so `gh` cannot infer the repo from a git remote.

## Conventions

- **Create an issue**: `gh issue create -R sfher/hui --title "..." --body "..."`
- **Read an issue**: `gh issue view <number> -R sfher/hui --json number,title,body,labels,comments`
- **List issues**: `gh issue list -R sfher/hui --state open --json number,title,body,labels,comments`
- **Comment on an issue**: `gh issue comment <number> -R sfher/hui --body "..."`
- **Apply / remove labels**: `gh issue edit <number> -R sfher/hui --add-label "..."`
- **Close**: `gh issue close <number> -R sfher/hui --comment "..."`

## Pull requests

Issues and PRs share one number space. Resolve an ambiguous number with `gh pr view <number> -R sfher/hui` and fall back to `gh issue view <number> -R sfher/hui`.

Read a PR with `gh pr view <number> -R sfher/hui --comments` and its diff with `gh pr diff <number> -R sfher/hui`.

## 需求交接

机制 Issue 应包含触发、效果、读数、反例和影响范围；规则拍板后回写 Issue。具体模板见 [策划-程序交接](策划-程序交接.md)。

当前规则基准和验证入口见 [docs/current-baseline.md](../current-baseline.md)。