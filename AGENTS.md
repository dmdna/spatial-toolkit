<!-- pico-cli:plugin-context:pico-unity-agentic-tools:start -->
## Plugin Context

Also read `./PICO-UNITY-AGENTIC-TOOLS.AGENTS.md` for PICO Spatial plugin guidance.
<!-- pico-cli:plugin-context:pico-unity-agentic-tools:end -->

<!-- pico-cli:plugin-context:pico-general-agentic-tools:start -->
## Plugin Context

Also read `./PICO-GENERAL-AGENTIC-TOOLS.AGENTS.md` for PICO Spatial plugin guidance.
<!-- pico-cli:plugin-context:pico-general-agentic-tools:end -->

## README and build history

- Keep `README.md` current whenever creating a new app build or patch. At minimum, prepend an entry for the new version at the top of its Changelog, immediately below the section introduction, with all older numbered builds retained in newest-first order.
- Use the actual app version and Android version code from `ProjectSettings/ToolkitBuildVersion.json` and Player Settings after versioning runs. The build preprocessor advances versions on build attempts, including retries; record a failed numbered attempt as failed rather than claiming a successful release.
- Describe meaningful additions, changes, and fixes for that build, plus the build outcome and verification actually performed. Distinguish automated checks, emulator runs, headset installation/startup, and physical headset acceptance. Do not invent dates, results, or release details.
- Update the README's current version, overview, features, controls, requirements, and setup information whenever the build changes those details. Complete the documentation update as part of the build task before reporting it finished.
