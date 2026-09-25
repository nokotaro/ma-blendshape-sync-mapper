# Changelog

## 0.1.0 - Unreleased

- Add explicit single-cell Add Sync with live validation, duplicate/conflict rejection, Undo/Redo and Prefab instance overrides.
- Preserve existing bindings/remap curves; use MA 1.18.7 normalized defaults only for the new exact binding.
- Select cells without mutation; rescan after writes and Undo/Redo while retaining Renderer/Shape selection.
- Add a read-only Renderer × Source BlendShape matrix with a fixed renderer pane and synchronized scrolling.
- Add case-insensitive column search and Relevant / All Source / Missing views.
- Show Missing + Custom distinctly, related custom/broken indicators, cached binding/remap tooltips and renderer diagnostics.
- Test Matrix classification/filtering and 12-renderer / 60-shape read-only window operations.
- Add read-only scanning and per-renderer summaries, including inactive targets.
- Distinguish exact, custom, other-source, and broken bindings; retain detached remap data.
- Invalidate temporary snapshots when inputs or referenced objects change.
- Add EditMode tests using generated assets, including read-only and multiple-component cases.
- Initialize the Editor-only VPM package and project-owned documentation.
- Add a window with Source Renderer, Target Root, Rescan, and renderer selection.
- Declare Modular Avatar as a VPM dependency; no direct NDMF dependency.
