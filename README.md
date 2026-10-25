# BimTools.FFTDIM

> **BIM tools project repository**
>
> This README describes the repository, its documentation standards, and the information required to build, install, and verify FFTDIM. The repository currently exposes only this README on the `main` branch; implementation source, project files, installer packages, and release artifacts were not available to inspect when this documentation was prepared. Accordingly, feature names, compatibility claims, and execution steps are not presented as implemented until they can be verified from the actual project.

[![Repository](https://img.shields.io/badge/GitHub-BimTools.FFTDIM-181717?logo=github)](https://github.com/amdghedy/BimTools.FFTDIM)

## Table of contents

- [Project overview](#project-overview)
- [Current repository status](#current-repository-status)
- [Feature and capability catalogue](#feature-and-capability-catalogue)
- [Supported environment](#supported-environment)
- [Getting the repository](#getting-the-repository)
- [Build, installation, and first run](#build-installation-and-first-run)
- [Recommended user workflow](#recommended-user-workflow)
- [Quality assurance and verification](#quality-assurance-and-verification)
- [Troubleshooting](#troubleshooting)
- [Security and model safety](#security-and-model-safety)
- [Repository structure](#repository-structure)
- [Development and contribution](#development-and-contribution)
- [Roadmap for complete project documentation](#roadmap-for-complete-project-documentation)
- [License](#license)

## Project overview

**BimTools.FFTDIM** is the GitHub repository for the FFTDIM-named BIM tools project. The repository name alone does not establish the full expansion of “FFTDIM”, the host application, the target versions, or the specific commands it provides. Those details should be taken from the implementation, project metadata, and release notes—not inferred from the name.

The purpose of this README is to provide a reliable starting point for users and contributors while clearly distinguishing verified repository contents from information that still needs to be published.

### Project goals for a complete, maintainable BIM tool

When the implementation is available, its documentation should make it easy to answer the following questions:

- **Purpose:** What design, modelling, coordination, documentation, or delivery task does the tool address?
- **Inputs:** Which active model, selected elements, linked models, external files, or user parameters are required?
- **Outputs:** Which model changes, schedules, reports, exports, or other files are produced?
- **Control:** Which options, filters, tolerances, units, and selection modes can the user configure?
- **Compatibility:** Which host-application versions, APIs, runtimes, and operating systems are supported?
- **Reliability:** What validation, transaction handling, rollback, logging, and error recovery are provided?
- **Verification:** Which automated tests and real-application checks have actually passed?

These are documentation requirements, not a claim that every capability already exists in this repository.

## Current repository status

At the time this README was prepared, the public repository's default branch was `main`, and the only file available for inspection was `README.md`. No source code, solution/project file, dependency manifest, test suite, installer, or published release could be verified.

| Item | Status |
|---|---|
| Public GitHub repository | Available |
| README documentation | Available |
| Implementation source | Not available to verify |
| Build instructions and project targets | Not yet verifiable |
| Host application and supported versions | Not yet verifiable |
| Automated tests and results | Not yet verifiable |
| Installer or downloadable release | Not yet verifiable |
| License | Not yet verifiable |

**Important:** This status reflects the repository contents that were accessible during this review. It does not establish whether code exists in another local folder, private location, branch, or unpublished artifact.

## Feature and capability catalogue

A detailed feature list must be derived from actual commands, UI controls, service classes, configuration files, and tests. To avoid misleading users, the catalogue below identifies the information that should be documented for each implemented feature.

| Documentation item | What should be recorded |
|---|---|
| Command name | Exact name shown in the application UI |
| Purpose | The practical BIM problem it solves |
| Entry point | Ribbon tab/panel, menu, toolbar, or command-line entry |
| Selection scope | Current selection, active view, active model, linked model, or entire project |
| Inputs | Required parameters, file formats, units, and prerequisites |
| Options | Defaults, valid ranges, checkboxes, filters, and selection modes |
| Processing | Key steps, rules, constraints, and validation behaviour |
| Outputs | Created or modified elements, reports, files, or data |
| Failure handling | Validation messages, rollback behaviour, and recovery guidance |
| Evidence | Automated tests, sample files, screenshots, and verified application version |

When source code is published, replace this checklist with a **command-by-command reference**. Include screenshots of the actual interface and short, reproducible examples. Do not advertise planned or partially implemented functions as production-ready.

## Supported environment

The exact requirements cannot be confirmed from the currently available repository files. Before using or distributing the tool, publish and verify the following information:

- Host application and supported versions.
- Operating system and processor architecture, if relevant.
- Required runtime and target framework.
- API assemblies and any external dependencies.
- Required access rights and whether installation is per-user or per-machine.
- Supported input and output formats.
- Known limitations, including version-specific behaviour.

Do not assume that a particular application version or .NET target is supported until the project configuration and a successful build confirm it.

## Getting the repository

Clone the repository:

```bash
git clone https://github.com/amdghedy/BimTools.FFTDIM.git
cd BimTools.FFTDIM
```

Inspect the available files and branches before attempting a build:

```bash
git branch --all
git status
git ls-files
```

At the time of writing, the public default branch did not expose the implementation needed to build or run the application. Cloning the repository therefore retrieves the published repository contents; it does not by itself provide a runnable tool.

## Build, installation, and first run

**Build and installation instructions are pending publication of the actual project files.** A reliable guide must be specific to the implementation rather than guess at the build system or host application.

Once the source is available, document these steps in order:

1. **Check prerequisites.** List the required host application, version, runtime, SDK, API assemblies, and external packages.
2. **Open the correct solution or project.** Give the exact relative path and supported build configuration.
3. **Restore dependencies.** Provide the tested command or IDE action and explain any offline requirements.
4. **Build.** State the exact configuration, target framework, platform, and output location.
5. **Run automated tests.** Include the actual test command and distinguish passed, failed, skipped, and not-run tests.
6. **Deploy.** Explain whether installation uses a release package, a per-user add-in directory, an application-managed installer, or another verified method.
7. **Launch and validate.** Describe where the command appears and how to confirm it loaded successfully.
8. **Test on a disposable sample.** Explain the expected result and how to verify that the model or output file changed correctly.

Do not install DLLs copied from an unknown build, overwrite a production deployment without a backup, or run model-changing commands on the only copy of a project.

## Recommended user workflow

The following is a general workflow for responsible BIM automation. The precise commands and expected outputs must be added when FFTDIM's implementation is published.

1. Open a controlled test project or a copy of the production model.
2. Confirm the active document, view, workset, selection, and linked-model state where relevant.
3. Review all available options and their defaults before execution.
4. Run the tool on a small, representative selection first.
5. Review created or modified elements and inspect warnings or logs.
6. Compare the result against the expected geometry, parameters, and project standards.
7. Save or export only after the output has been checked.
8. Record the tool version, host-application version, input model, configuration, and result for repeatability.

## Quality assurance and verification

For a BIM automation tool, a successful compilation is necessary but not sufficient. The project should document each verification layer separately.

### 1. Source and build checks

- Confirm all required source files are tracked in Git.
- Restore dependencies in a clean environment.
- Compile the intended configurations and target versions.
- Treat compiler errors separately from warnings; explain any intentionally suppressed warning.
- Confirm the expected binaries and supporting files were produced.

### 2. Automated tests

Tests should cover the actual behaviour of the implementation, including:

- Normal inputs and expected results.
- Empty selections, missing parameters, and invalid input.
- Unit conversions, tolerances, and boundary conditions, where applicable.
- Duplicate execution and repeatability.
- Error handling and transaction rollback for model-changing operations.
- File input/output, malformed files, and access failures, where applicable.
- Regression tests for previously fixed defects.

### 3. Host-application integration checks

Where the tool depends on a BIM application's API, verify it in the real host application. Confirm that commands load, UI actions work, model changes are correct, and the application remains responsive. Mock-based tests and CI compilation should not be described as equivalent to live integration testing.

### 4. Publish test evidence

For each release, report:

| Check | Result to publish |
|---|---|
| Build | Target version/configuration and pass/fail |
| Automated tests | Passed, failed, skipped, and total |
| Integration test | Host version and tested workflow |
| Deployment | Package and installation method |
| Known issues | Reproducible limitations and workarounds |

Do not add passing badges or claim a test passed unless the corresponding workflow or test result supports that statement.

## Troubleshooting

Because the implementation and build configuration are not currently available for inspection, the entries below are general diagnostic guidance rather than FFTDIM-specific fixes.

| Symptom | Recommended checks |
|---|---|
| No application command appears | Confirm the correct package is installed, the host version is supported, and the application startup log contains no load errors. |
| Assembly or runtime load error | Check the target framework, architecture, API references, dependency versions, and the host application's runtime requirements. |
| Build cannot find a reference | Verify the project file, reference paths, SDK/runtime installation, and whether required assemblies are available in the build environment. |
| Command reports invalid input | Check required selections, parameters, units, file paths, and model/document state. |
| Unexpected model changes | Stop further execution, preserve diagnostic information, and restore a known-good copy or use the application's supported undo/rollback mechanism. |
| Output file is missing | Check the configured output path, permissions, file locks, validation messages, and logs. |
| Works in one host version but not another | Verify API differences and build/test evidence for each supported version independently. |

When reporting an issue, include the exact error message, steps to reproduce, host-application version, tool/build version, relevant logs, and a minimal sample where sharing is permitted. Remove confidential project data before sharing files publicly.

## Security and model safety

- Test model-modifying operations on a copy before using them on a live project.
- Use only dependencies and release artifacts from trusted, documented sources.
- Never commit credentials, access tokens, private project models, or confidential client information.
- Validate external files and user-supplied paths before processing.
- Preserve useful diagnostic messages without exposing secrets or sensitive project information.
- Document whether a command creates, edits, deletes, or exports model data.
- Provide a recovery path for operations that can change project content.

## Repository structure

The implementation's directory structure could not be verified because source and project files were not available in the public default branch during this review. Once they are published, replace this note with the actual tree and explain the role of each important directory, including:

- Application entry points and command registration.
- User interface and view models, if used.
- Core business logic and model services.
- Configuration and resources.
- Tests and test data.
- Build and deployment scripts.
- Documentation, examples, and release notes.

Only list paths that exist in the repository.

## Development and contribution

Contributions should be small, traceable, and verifiable.

1. Open an issue describing the problem or improvement.
2. Include reproducible steps and expected versus actual behaviour for defects.
3. Create a feature branch and keep unrelated changes separate.
4. Follow the existing language, naming, architecture, and formatting conventions once those conventions are visible in the source.
5. Add or update tests for behaviour changes.
6. Build and run the relevant tests before opening a pull request.
7. Summarize the change, compatibility impact, test evidence, and any known limitations.

### Suggested pull request checklist

- [ ] The change has a clear purpose and scope.
- [ ] Existing behaviour and supported versions were considered.
- [ ] Tests were added or updated where applicable.
- [ ] Build/test results are reported accurately.
- [ ] User-facing options and defaults are documented.
- [ ] Errors, limitations, and recovery steps are documented.
- [ ] No credentials, private models, generated binaries, or unrelated files were added.

## Roadmap for complete project documentation

The next documentation pass should be based on the actual implementation and should close these gaps:

- [ ] Publish the source code and project/solution files.
- [ ] Confirm the meaning and intended scope of FFTDIM.
- [ ] Identify the host application, API versions, frameworks, and dependencies.
- [ ] Inventory every command, feature, option, default, and output.
- [ ] Add verified installation and deployment instructions.
- [ ] Add screenshots and a short end-to-end example using a sample project.
- [ ] Document configuration, logging, error messages, and recovery.
- [ ] Publish automated test commands and truthful results.
- [ ] Record known limitations and version compatibility.
- [ ] Add a license and release notes when the project's licensing and release process are decided.

## License

No license file was available to verify when this README was prepared. Until a license is added to the repository, do not assume that the source code is licensed for redistribution, modification, or commercial use. Add the license selected by the project owner and ensure that third-party dependencies retain their required notices.

---

**Documentation accuracy note:** This README intentionally avoids claiming unverified functionality, compatibility, successful builds, or test results. Update it from the published implementation and CI evidence as the repository becomes complete.
