# Changelog

## [1.0.1] - 2026-09-28

### Fixed

- Removed Unity serialization of the recursive `MenuToolNode.children` graph, eliminating `Serialization depth limit 10 exceeded` warnings when creating, importing, focusing, or editing `.menutool` assets.
- Replaced `JsonUtility` for `.menutool` source files with a package-local JSON codec, preserving the existing version 1 tree-shaped source format without a migration.
- The imported `MenuToolDefinition` now stores scalar summary metadata only; the `.menutool` JSON remains the source of truth.

## 1.0.0

- Added `.menutool` `ScriptedImporter` for Unity 2022.3.
- Added dedicated hierarchy editor and asset creation menu.
- Added Input-System-style C# generation settings in the importer Inspector.
- Added idempotent automatic generation through the Unity Asset Pipeline.
- Added generated `Label`, `FileName`, `Path`, and root `DefaultFileName` constants.
- Added validation for C# identifiers, duplicate siblings, labels, namespace, and output path.
- Added basic sample.
