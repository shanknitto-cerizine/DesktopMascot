# DesktopMascot documentation index

Current architecture baseline: **M-046**

M-046 repository structure cleanup and its explicitly approved tray startup
recovery prerequisite have completed automated and manual validation.

## Authoritative documents

- [`../../AGENTS.md`](../../AGENTS.md) defines the current development and
  ownership contracts.
- [`Milestones.md`](Milestones.md) records completed and in-progress
  milestone history. Historical paths and contracts in older entries are
  evidence of those milestones, not a description of the current tree.
- [`RepositoryStructureDesign.md`](RepositoryStructureDesign.md) defines the
  current source, diagnostics, documentation, tooling, and generated-artifact
  boundaries established by M-046.

## Current design documents

- [`SettingsPresentationDesign.md`](SettingsPresentationDesign.md)
- [`RuntimeRetiredCharacterDisposalDesign.md`](RuntimeRetiredCharacterDisposalDesign.md)
- [`RuntimeBundledCharacterRestorationDesign.md`](RuntimeBundledCharacterRestorationDesign.md)
- [`RuntimeCharacterPersistenceDesign.md`](RuntimeCharacterPersistenceDesign.md)
- [`RuntimeVrmSelectionUiDesign.md`](RuntimeVrmSelectionUiDesign.md)
- [`RuntimeVrmImportFoundationDesign.md`](RuntimeVrmImportFoundationDesign.md)
- [`CharacterAssetFoundationDesign.md`](CharacterAssetFoundationDesign.md)
- [`SingleInstanceDesign.md`](SingleInstanceDesign.md)
- [`WindowPositionPersistenceDesign.md`](WindowPositionPersistenceDesign.md)
- [`D3D12TextureTransferDesign.md`](D3D12TextureTransferDesign.md)
- [`SpeechPresentationDesign.md`](SpeechPresentationDesign.md)

The remaining files in this directory are focused design records for earlier
native composition, interaction, and diagnostic milestones. They remain
useful historical and regression references.

## Generated evidence is not documentation

`NativePlugin/out` contains generated build output and diagnostic logs.
Source code and documentation must never depend on it. Logs explicitly cited
from `Milestones.md` are retained as validation evidence, but their presence
does not make `NativePlugin/out` a source or documentation directory.

Native DLL, PDB, ILK, Ninja output, Unity Player builds, Unity-generated
folders, and runtime persistence files are generated artifacts. Do not edit
or reorganize them as if they were source.
