<!--
Sync Impact Report:
Version: 1.0.0 (initial version)
Modified Principles: N/A (initial creation)
Added Sections: All sections (initial creation)
Removed Sections: N/A
Templates Requiring Updates:
  ✅ .specify/templates/plan-template.md - Reviewed, constitution check section aligns
  ✅ .specify/templates/spec-template.md - Reviewed, requirements align with principles
  ✅ .specify/templates/tasks-template.md - Reviewed, task categories align with principles
  ⚠ No command files found in .specify/templates/commands/
Follow-up TODOs: None
-->

# NetPace Constitution

## Core Principles

### I. Test-Driven Development (NON-NEGOTIABLE)

Every line of production code MUST be written in response to a failing test following the RED-GREEN-REFACTOR cycle:

1. **RED** - Write failing test describing desired behavior, run and watch it FAIL
2. **GREEN** - Write minimum code needed to pass, run and watch it PASS
3. **REFACTOR** - Commit before refactoring, improve design, run tests - still PASS

**Critical Rules:**
- MUST NEVER write production code without a failing test first
- MUST NEVER skip the RED step (must see test fail)
- MUST NEVER refactor on red (always get to green first)
- MUST NEVER add features not covered by tests
- MUST NEVER proceed if tests are failing

**Rationale**: TDD ensures every feature is testable, reduces bugs, improves design, and provides living documentation through tests. This is foundational to NetPace quality standards.

### II. Library-First Architecture

Every feature MUST start as a standalone library (`NetPace.Core`) before CLI implementation:

- Libraries MUST be self-contained, independently testable, and documented
- Core library MUST have no dependencies on Console application
- Core library MUST be usable in any context (console, web API, GUI, tests)
- Clear purpose required - no organizational-only libraries
- Interfaces over concrete implementations for abstraction and testability

**Rationale**: Library-first design ensures code reusability, testability, and enables NuGet package distribution. Consumers can use NetPace.Core without any CLI dependencies.

### III. CLI Excellence

The command-line interface MUST follow industry best practices:

- Follow [CLI Guidelines (clig.dev)](https://clig.dev/) strictly
- Use Spectre.Console for all console output and interaction
- Support `--help` and `--version` flags
- Provide clear error messages with actionable guidance
- Support multiple output formats (normal, CSV, JSON) for scripting
- Default behavior should work for most users without flags
- Verbosity levels: Minimal (scripts), Normal (users), Debug (troubleshooting)

**Rationale**: CLI applications are tools for users. Following established guidelines ensures NetPace is intuitive, scriptable, and professional.

### IV. Cross-Platform Compatibility

All code MUST run on Windows, Linux, and macOS without platform-specific workarounds:

- Target .NET 8.0 for cross-platform support
- Consider file paths, line endings, console encoding
- Test on multiple platforms before release
- Avoid platform-specific APIs unless absolutely necessary
- Document any platform-specific behavior clearly

**Rationale**: NetPace serves a diverse user base across operating systems. Cross-platform support maximizes accessibility and adoption.

### V. Code Quality Standards

All production code MUST meet these quality standards:

- **Naming**: PascalCase for classes/methods/properties, camelCase for private fields/variables
- **Documentation**: XML documentation on all public APIs
- **Async/Await**: Network operations MUST be async with CancellationToken support
- **Nullable Reference Types**: Enabled to prevent null reference exceptions
- **Error Handling**: Validate inputs early, don't swallow exceptions, use specific exception types
- **No Warnings**: Build MUST succeed with zero warnings

**Rationale**: Consistent quality standards ensure maintainability, reduce bugs, and provide a professional developer experience for NuGet package consumers.

### VI. Minimal Dependencies

NetPace.Core MUST keep dependencies minimal:

- Every dependency MUST be justified (fewer version conflicts for consumers)
- Prefer .NET BCL over third-party libraries when possible
- Document all dependencies and their purpose
- Review dependency security regularly

**Rationale**: As a NuGet package, NetPace.Core's dependencies become consumers' dependencies. Minimal dependencies reduce version conflicts and security surface area.

### VII. Semantic Versioning

All releases MUST follow semantic versioning (MAJOR.MINOR.PATCH):

- **MAJOR**: Breaking changes to public API
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible
- Document breaking changes in release notes
- Discuss public API changes before implementation

**Rationale**: NuGet consumers depend on predictable versioning to avoid breaking changes. Semantic versioning is industry standard for package distribution.

## Development Workflow

### Git Workflow

- Work on feature branches (`feature/your-feature-name`)
- Commit frequently, especially before refactoring
- Use clear, concise commit messages in imperative mood
- Reference issues when applicable: "Fix #123: Handle null server response"
- Do not commit code with failing tests or build warnings

### Code Review Standards

Before committing, verify:
- Build succeeds with no warnings
- All tests pass (RED-GREEN-REFACTOR cycle followed)
- Code follows naming conventions
- Public APIs have XML documentation
- No commented-out code (delete it, git remembers)
- Documentation updated (README.md, USER_GUIDE.md)

### Testing Standards

- Test project naming: `NetPace.Core.Tests`, `NetPace.Console.Tests`
- Use xUnit testing framework
- Test naming: `MethodName_Scenario_ExpectedResult`
- Given-When-Then pattern for test structure
- Tests MUST be readable, independent, fast, and deterministic
- Mock external dependencies (network, filesystem, time) for unit tests

**Do NOT test**: Spectre.Console output (trust the library), simple property getters/setters with no logic, third-party libraries

## Technology Constraints

### Required Technologies

- **Framework**: .NET 8.0 (cross-platform)
- **Language**: C# 12
- **CLI Library**: Spectre.Console
- **Testing**: xUnit
- **Package Distribution**: NuGet (NetPace.Core)

### Architecture Patterns

- **Separation of Concerns**: NetPace.Core (business logic), NetPace.Console (UI/CLI)
- **Dependency Injection Ready**: Depend on interfaces, constructor injection
- **Result Objects**: Return rich result objects with speed, duration, bytes transferred
- **Extension Methods**: For formatting/conversion logic that doesn't belong in core types
- **Options Pattern**: Complex configuration via options objects instead of many parameters

## Performance & Scale

### Performance Requirements

- Async operations for all network calls
- HttpClient best practices (singleton, pooling)
- CancellationToken support for long operations
- Measure and optimize hot paths (speed test loops)

### Units and Formatting

- Support SI (1000-based) and IEC (1024-based) unit systems
- Support BitsPerSecond and BytesPerSecond
- Auto-scale by default (Mbps, Gbps) with user override
- Consistent formatting across all output modes (normal, CSV, JSON)

## Governance

### Constitutional Authority

This constitution supersedes all other development practices and guides. All development work MUST verify compliance with these principles before proceeding.

### Amendment Process

1. Amendments require clear documentation of rationale
2. Breaking changes to principles require project maintainer approval
3. Version bump per semantic versioning rules:
   - **MAJOR**: Backward incompatible governance/principle removals or redefinitions
   - **MINOR**: New principle/section added or materially expanded guidance
   - **PATCH**: Clarifications, wording, typo fixes, non-semantic refinements

### Compliance Review

- All pull requests MUST verify constitutional compliance
- Complexity MUST be justified against simplicity principles
- For runtime development guidance, refer to `.claude/CLAUDE.md`

**Version**: 1.0.0 | **Ratified**: 2026-04-10 | **Last Amended**: 2026-04-10
