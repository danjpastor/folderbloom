# Repository preparation validation — 2026-10-16

- Built the unmodified 0.7.1.0 source with Windows .NET Framework C# compiler using build.ps1: passed.
- Ran the built executable with --self-test and an isolated task-workspace output directory: all 174 checks passed, exit code 0.
- Initial sandboxed test stopped at Task Scheduler's validate-only API with HRESULT 0x80070005. The accepted unrestricted rerun passed; the test uses validation mode and does not create or change a scheduled task.
- Reviewed the 20 source/build files and existing license. Pattern scan found no credential token, private-key block or literal secret assignment. The key-material match in Native.cs is a Windows virtual-key constant.
- Candidate excludes installed EXE, personal settings, desktop recovery records, test output, binaries and historical logs. Compiled test executable is ignored and not committed.
- No Git history existed in the selected versioned source snapshot. A byte-preserving private backup plus SHA-256 inventory was created before preparation. Initial source snapshot is newly committed, with no upstream history rewritten.
- No normal app launch, installation, sign-in startup registration, Explorer registration or live desktop behavior test performed. Live hover/drag and mixed-DPI acceptance remain unverified.
- Intended remote visibility: PUBLIC. No remote was created or pushed during this preparation because repository creation/authentication tooling is unavailable.
