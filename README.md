# MA Blendshape Sync Mapper

VRChat Avatar向けのModular Avatar Blendshape Syncを、一括で可視化・編集するUnity Editor Toolです。
**Status: Beta** (`0.1.0-beta.1`)

## 対象と目的

複数の衣装やメッシュを持つアバターを改変するユーザー向けに、Blendshape Syncの設定状況や不足Bindingを確認しやすくすることを目指しています。
既存改変済み衣装へ後付けでき、既存のMA Blendshape Sync Componentを唯一のSource of Truthとして扱います。

## 現在できること

- `Tools > MA Blendshape Sync Mapper`からWindowを開く。
- Source Renderer（SkinnedMeshRenderer）とTarget Root（GameObject）を指定する。
- RescanでInactiveを含むTarget Rendererと既存MA Bindingを読み取る。
- Rendererごとのcompatible / synced / missing / custom / brokenを表示する。
- Renderer × Source BlendShapeのMatrixを、検索とRelevant / All Source / Missingフィルタで絞り込む。
- 固定Renderer列、セルTooltip、選択RendererのDetailsで既存Mappingと診断を確認する。
- Renderer行をクリックしてHierarchy上のGameObjectを選択する。
- Missingセルを選択し、Detailの`Add Sync`でexact同名Bindingを1件追加する。Undo/RedoとPrefab Instance Overrideに対応。
- `Review Safe Changes`でRenderer別にSafe / Require Reviewを確認し、`Add N Safe Missing Syncs`でSafe全件を追加する。Search/Viewに関係なくScan全体が対象。

SnapshotはWindow内の一時データです。Missingはexact同名Bindingの不足を表し、既存custom mappingの上書き許可を意味しません。
Scan・選択・Tooltip・Previewはアバターを変更しません。書き込みは`Add Sync`またはPreviewの`Add N Safe Missing Syncs`を明示実行した場合のみです。
既存Custom、別SourceによるTarget占有、Renderer内のBroken、複数MA Component、解決不能参照は追加を拒否します。
書き込み直前に最新状態を再検証し、成功後とUndo/Redo後に再Scanします。既存Binding/Remapは変更しません。
同じAvatar内のScene Object・Prefab Instanceを対象とします。Prefab Asset直接編集、Prefab Mode、Play Modeは未対応です。
Bulkでは全候補のpreflight後に追加し、途中失敗は全体Rollbackします。全追加とComponent作成が1回のUndo/Redoにまとまります。
Safeは単件Writerと同じ安全条件を満たすMissing、Require Reviewは競合等で追加禁止のMissingです。Reviewは自動変更せず残します。
Binding更新・削除・Custom編集・Broken修復は未実装です。

主な用途は、別アバター向け衣装を変換した後など、多数のSkinnedMeshRendererに対するMA Blendshape Sync設定を確認・追加することです。特定のアバターや衣装製品は必須ではありません。

## 環境と配布

- Unity **2022.3.22f1** / VRChat SDK Avatars
- Modular Avatar >= 1.18.7 < 2.0.0-a（検証版: 1.18.7）
- Package ID: `com.nokotaro.blendshape-sync-mapper`
- Editor-only VPM Packageとして配布予定。UdonSharp Runtimeや独自NDMF Passはありません。
- Beta版のVCC ListingはGitHub Pagesで提供します。

VCCでBeta版を表示するには、Settings → Packages → Pre-Release Packages → Show Pre-Release Packages を有効にしてください。

## 開発

RepositoryルートがUnityプロジェクトです。製品コードの正本は`Packages/com.nokotaro.blendshape-sync-mapper/`です。

1. [AGENTS.md](AGENTS.md): 開発ルールと文書の読み順
2. [ARCHITECTURE.md](docs/ARCHITECTURE.md): MVP設計原則とSource of Truth
3. [ENVIRONMENT_SETUP.md](docs/ENVIRONMENT_SETUP.md): clone後の依存解決・起動・検証
4. [OFFICIAL_REFERENCE_MAP.md](docs/OFFICIAL_REFERENCE_MAP.md): 一次資料と確認日

## 出典とライセンス

[VRChat Community VPM Package Template](https://github.com/vrchat-community/template-package/tree/591e23fe5175b8279cc3998f65e81add604aabc9)をベースに初期化しました。
使用コミット: `591e23fe5175b8279cc3998f65e81add604aabc9`。
GitHub Template生成APIの組織OAuth制限により、同コミットのファイルをコピーして新規Repositoryを初期化しています。

本プロジェクトの新規コード・文書および製品Packageは[MIT License](Packages/com.nokotaro.blendshape-sync-mapper/LICENSE.md)、著作権者はnokotaroです。
Template由来のファイルの出典は上記のとおりです。開発用bootstrapには同フォルダのVRChat Licenseが適用されます。
外部依存Packageのライセンスは各Packageに従います。
