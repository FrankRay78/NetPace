# Agentic Software Development Workflow

The following document outlines steps taken to transition to a more 'hands-off' spec driven development (SDD) approach using an agentic software development workflow and practices.


## Pre-requisites

### Install Spec Driven Development (spec-kit)

```bash
# Install latest cli from main
uv tool install specify-cli --from git+https://github.com/github/spec-kit.git

# Initialize in existing project
specify init . --ai claude
```

https://github.com/github/spec-kit


### Install Claude Code GitHub action for tagging (eg. in pr comments)

"The easiest way to set up this action is through Claude Code in the terminal. Just open claude and run /install-github-app."

https://code.claude.com/docs/en/github-actions
