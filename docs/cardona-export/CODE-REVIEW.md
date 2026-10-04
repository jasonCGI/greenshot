# Code review: export profiles and preview delivery

Review scope: export profile model/storage, Output settings bindings and handlers, new regression tests, isolated launcher, and release packaging. Existing DPI, WebP, resize, and UI-scale changes were checked at their integration boundaries. This is a self-review, not an independent review of the entire upstream repository.

## Issues addressed

- Fixed settings could otherwise be bypassed or lead to a partially applied profile. The implementation checks every affected property before changing any export preferences. Profile storage also respects fixed policy.
- Malformed profile storage could otherwise be silently replaced by a new save. Loading retains the original value and blocks destructive edits, with an explicit status message.
- Unavailable plugin formats could otherwise become the default export format. Apply checks the active saveable format registry before changing preferences.
- Duplicate, reserved, oversized, and invalid names or numeric settings could otherwise produce ambiguous or invalid profiles. Validation covers the complete profile before applying or serializing it. Custom storage has a 20-profile and 64 KiB limit.
- XML entity resolution could otherwise read external resources. Deserialization prohibits DTDs, disables the resolver, and limits document size.
- A blank name field was visually unclear. It now has a visible label and accessible name. The first built-in profile is selected for discovery, but settings change only when Apply is pressed.
- Preview packaging could otherwise include user settings/logs or stale libraries. Packaging includes only runtime assets and explicit documentation, compares Base/Editor DLL hashes against the test host, requires committed source, and creates a file manifest. The launcher uses separate settings/log directories.

## Verification

Final result: 578 tests passed, zero failures. The policy fixture runs without parallel tests and unregisters its temporary configuration; an earlier test-isolation failure was corrected before the final run.

The selected build and regression suite is executed with `Build-Preview.ps1 -Test -NoRestore`. Results include profile round-trip through the INI writer/parser, policy refusal without partial updates, invalid and malicious storage, profile application, custom save/reopen/delete, corruption preservation, and Output rendering at 100%, 125%, 150%, and 200%. The release record gives the final test count and exact source commit.

## Remaining limitations

No unresolved defect was identified in the reviewed changes after fixes and focused checks. Native keyboard, Save As/overwrite, real configuration save at process exit, Windows scaling combinations, and mixed-monitor capture are still unverified. Existing upstream Preferences changes settings live, so Cancel is not a full rollback; the guide makes that explicit. At 200% on smaller screens, the existing wrapper permits scrolling rather than shrinking controls. New profile/help copy is English; complete translation coverage is deferred. Existing build analyzer warnings and the unretested full restore graph remain.

The release is unsigned and explicitly marked prerelease. The installed application is unchanged.
