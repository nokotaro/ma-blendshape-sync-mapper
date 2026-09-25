# MA Blendshape Sync Mapper

Modular Avatar Blendshape Syncをまとめて可視化・編集するためのUnity Editor Toolです。開発中であり、正式Release前です。

## 現在の機能

`Tools > MA Blendshape Sync Mapper`でWindowを開き、Source RendererとTarget Rootを指定できます。
RescanでTarget Root自身と子孫のSkinnedMeshRendererをInactiveも含めて検索し、Source自身を除外します。
Rendererごとのcompatible / synced / missing / custom / brokenとHierarchy pathを表示します。行クリックでGameObjectを選択できます。
SnapshotはWindow内の一時データです。入力変更や参照先削除時にはRescanを促します。
Missingは同名exact Bindingの不足であり、custom mappingがないことを意味しません。
Matrixの横軸はSource BlendShape、縦軸はRendererです。横scrollでも左側のRenderer列は固定されます。
Searchは大小文字を区別せず、Relevant（既定）/ All Source / Missingと併用できます。
`●` synced、`○` missing exact、`△` missing + Target側custom占有、`-`同名Shapeなしを表します。
補助記号`C`は関連custom、`×`は関連brokenです。Tooltipで実際の参照先・Shape名・Remap情報を確認できます。
Renderer行を選択するとDetailsに全体集計、Hierarchy path、Instance ID、Component数、custom/other-source/broken診断が出ます。
セルクリックは選択のみです。安全なMissingセルを選択するとDetailに`Add Sync`が表示され、押すとexact同名Bindingを1件だけ追加します。
書き込み直前に最新Scene/MAを再検証し、重複・Custom/別Source占有・Renderer内Broken・複数Componentを拒否します。
同じAvatar内のScene Object/Prefab Instanceに対応し、Undo/RedoとPrefab Overrideを記録します。Prefab Asset直接編集・Prefab Mode・Play Modeは対象外です。
成功後とUndo/Redo後は自動再Scanします。既存Binding/Remapは変更しません。Scan・選択・Tooltipでは書き込みません。
`Review Safe Changes`でScan全体のMissingをSafe / Require Reviewに分類してRenderer別に確認できます。Search/Viewの絞り込みは一括追加範囲を変更しません。
Previewの`Add N Safe Missing Syncs`でSafe全件を追加します。全候補の最新状態をpreflightし、状態変化があれば書き込み0件でPreview更新を要求します。
全追加は1回のUndo/Redoにまとまり、途中失敗は全体Rollbackします。Require Reviewの競合は変更せず残します。
Previewは非永続データで、確認画面を開くだけでは書き込みません。Binding変更・削除、Broken修復は未実装です。

## 対応環境

- Unity 2022.3.22f1 / VRChat Avatarsプロジェクト
- Modular Avatar 1.18.7以上、2.0.0未満の安定版（検証版: 1.18.7）
- Editor-only。UdonSharp Runtimeはありません。

既存のMA Blendshape Sync Componentが唯一のSource of Truthです。独自のMapping DBやProfile Assetは作りません。

VPM Repository: https://vpm.nokotaro.com/index.json

VPM Packageとして配布予定です。Beta版のListingは上記Repository URLからVCCへ追加します。
package.jsonのダウンロードURLは将来の0.1.0 Release用で、まだ利用できません。

開発用の導入方法は[環境構築手順](https://github.com/nokotaro/ma-blendshape-sync-mapper/blob/main/docs/ENVIRONMENT_SETUP.md)を参照してください。

## ライセンス

[MIT License](LICENSE.md) / Copyright (c) 2026 nokotaro
