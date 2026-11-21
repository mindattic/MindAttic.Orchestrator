# MindAttic project agent entrypoint

Read the shared protocol at ..\mindattic-agent-standard\AGENTS.md and this project's own
documentation. Common prompt commands are implemented by the shared runner; do not add a second
copy under a provider-specific command folder.

## Version (M.ORCH-T1888)

The Orchestrator's version is a whole number that goes up by one with every commit touching `src\`; the window title
shows it ("MindAttic Orchestrator vN"). After every commit, run `.\version.ps1` and tell the user the version you just
committed, so they can match it against the deployed title.
