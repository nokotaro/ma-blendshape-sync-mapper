# Environment Setup

## 導入した開発環境

確認日: 2026-09-24

| 項目 | バージョン / 方針 |
| --- | --- |
| Unity | 2022.3.22f1 (887be4894c44) |
| VRChat SDK Avatars | 3.10.5 |
| VRChat SDK Base | 3.10.5 (Avatarsの依存) |
| Modular Avatar | 1.18.7 |
| NDMF | 1.14.8 (MAの推移依存) |
| VPM CLI | 0.1.28 (今回の依存導入に使用) |
| 製品Package | 0.1.0 (Unreleased) |

VRChat Creator Companion（VCC）/ VPMを使うAvatars開発環境を前提とする。
今回は公式VPM CLIでPackageを導入した。VCCアプリのバージョンは検証していない。
MAの製品依存範囲は`>=1.18.7 <2.0.0-a`。理由と未検証範囲はARCHITECTURE.mdに記載する。
SDK APIを製品から直接利用していないため、SDKは開発プロジェクトの依存として管理する。
NDMFは製品から直接依存指定しない。

## cloneから起動まで

1. Unity Hub / VCC経由でUnity 2022.3.22f1をインストールする。
2. 短いパスへ`git clone https://github.com/nokotaro/ma-blendshape-sync-mapper.git`を実行する。深いフォルダではSDKアセットの絶対パスがWindowsの制限を超える場合がある。
3. VCCのRepositoriesへMAの配布元`https://vpm.nadena.dev/vpm.json`を登録する。
4. VCCへcloneしたRepositoryルートを既存プロジェクトとして追加し、`Packages/vpm-manifest.json`の依存を復元する。
5. Unity Hubへ同じRepositoryルートを追加し、2022.3.22f1で開く。Assets、Packages、ProjectSettingsがあるフォルダを指定する。
6. VPM/Unity Package Managerの依存解決・インポート・コンパイルが完了するまで待つ。
7. `Tools > MA Blendshape Sync Mapper`を開く。

VPM CLIを使う場合、.NET 8 SDKと公式`vrchat.vpm.cli`を用意し、Repositoryルートで実行する。

```powershell
vpm add repo https://vpm.nadena.dev/vpm.json
vpm resolve project .
```

初期構築時に実行した導入指定は次のとおり。

```powershell
vpm add package com.vrchat.avatars@3.10.5 -p .
vpm add package nadena.dev.modular-avatar@1.18.7 -p .
```

UnityのUPM依存は`Packages/manifest.json`と`packages-lock.json`、VPMの依存と解決結果は`vpm-manifest.json`を管理する。
SDK、MA、NDMFの解決済みフォルダはcommitせず、各配布元から復元する。
Template由来のbootstrapは開発環境専用で、製品のRelease ZIPには含まれない。
Unity MCPは導入していない。

## 検証

- Unityのコンパイル後、Consoleのerror/warningと発生元を確認する。
- メニューからWindowを開き、Source RendererとTarget Rootを指定してRescanする。入力不足・Source MeshなしではScanできない理由を表示する。
- inactiveを含むRenderer一覧とcompatible/synced/missing/custom/brokenの集計を確認する。Scan・選択は読み取り専用。
- 安全なMissingセルのDetailでAdd Syncを押した場合のみ1件追加する。専用検証SceneでUndo/Redo、Prefab Instance Override、既存Binding保全も確認する。
- MeshやBindingを外部で編集した後はRescanする。入力変更・参照Object削除時はSnapshotを無効化する。
- 製品asmdefのincludePlatformsがEditorだけであることを確認する。
- 製品ファイル・フォルダにUnity生成の.metaがあり、Git追跡対象であることを確認する。
- 新規UTF-8ファイルのBOM・置換文字、JSON/YAML構文、Git差分、デモ残骸を確認する。

Windowsのバッチコンパイル・Window生成確認例（Unity実行ファイルのパスは実環境に合わせる）:

```powershell
Unity.exe -batchmode -quit -projectPath . -executeMethod Nokotaro.BlendshapeSyncMapper.BlendshapeSyncMapperWindow.OpenWindow -logFile foundation.log
```

Window生成確認では`-nographics`を付けない。グラフィックス無効時には表示警告が出る。
バッチでのWindow生成と対話画面の目視確認は別の検証として記録する。

### ScannerのEditor Test

`Packages/manifest.json`の`testables`に製品Packageを指定済み。Test RunnerのEditModeで
`Nokotaro.BlendshapeSyncMapper.Tests`を実行する。テストAssemblyだけがAvatar Descriptorのfixture用にSDK DLLを参照する。
製品AssemblyにはSDK/NDMFの直接参照を追加しない。
対話Editorで実行する場合は、開いている無題Sceneを先に保存する。Unityは未保存の無題Sceneへの追加Scene作成を許可しない。
batchmodeでは独立したEditorプロセス内にテスト専用Sceneを作成する。

```powershell
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter Nokotaro.BlendshapeSyncMapper.Tests -testResults scanner-tests.xml -logFile scanner-tests.log
```

`-quit`は付けず、Test Runnerの終了を待つ。Windowsでスクリプトから起動する場合は`Start-Process -Wait`で終了コードを取得する。
ライセンスのIPC接続が許可される通常ユーザー環境で実行する。
結果XMLとログは成果物として確認し、Repositoryにはcommitしない。
テストは一時Scene、Mesh、GameObject、Prefabを生成し、終了時に削除する。有償アバターは不要。
Scene/Prefabの読み取り専用テストでは、Scan・Window描画前後のserialized値、dirty状態、保存ファイルのbyte列を比較する。

### Read-only Scannerの実測結果（2026-09-24）

- Unity 2022.3.22f1 / MA 1.18.7でコンパイル成功、EditMode 16件成功、失敗・skip 0件、Unity終了コード0。
- inactive、Root自身、Source除外、null Mesh、MAなし、重複exact、空白Local名fallback、custom占有、別Source、Broken各理由を検証。
- 直接参照/path-only/古いpath/空path/avatar rootなし、元の参照キャッシュ不変、Snapshot無効化、同名GameObject識別を検証。
- 一時Prefabのserializedデータで複数MA Componentを再現し、統合せず読めることを検証。
- メニューからWindowを開き、入力指定、Rescan、Repaintを実行。Scene/Prefab/Bindingの不変性を検証。対話操作の目視・行クリックは未検証。
- 30 Renderer × 各150 Shape、3,000 Bindingの1回のScanは15.73 ms（このPCのbatchmode実測。一般的な性能保証ではない）。
- 製品・テストコードのコンパイルwarning/errorは0件。今回のフルコンパイルでは外部Packageに39件（MA 36、NDMF 3）の既存warningあり。
- 最終実行ログにNDMFのNoto Sans CJK JPフォント未検出メッセージ、およびUnityライセンス署名検証Code 10/Access token unavailableがある。ライセンス権利解決後、全テストは正常終了。
- SDK初期化が変更したプロジェクト設定や自動取得したVPM Resolverは機能の差分に含めない。テスト用Assetは後片付け済み。

### Read-only Matrixの実測結果（2026-09-24）

- Unity 2022.3.22f1 / MA 1.18.7、EditMode全22件成功（既存16件を含む）、失敗・skip 0件、終了コード0。
- Matrix基本状態、Missing + Custom占有、別名対応、他Source/解決不能参照を誤ってSource列へ関連付けないことを検証。
- 検索の大小文字、Relevant / All Source / Missing、複数Rendererでの列集約、Source index保持、0 Renderer / 0 Shapeを検証。
- 合成12 Renderer × 60 ShapeをWindowへ渡し、フィルタ・横/縦scroll state・選択・Tooltip内容・Details・参照削除時のMatrix破棄を検証。
- 同じAnalysis/Viewインスタンスがフィルタ・scroll・選択後も維持されること、およびScene dirty、MA serialized値、保存Scene/Prefab byte列、Prefab override数の不変性を検証。
- 30 Renderer × 150 Shape / 3,000 Binding: 最終実行のScan 19.17 ms、Matrix View Model + 全GUIContent/Tooltip生成63.14 ms（当PCのbatchmodeで各1回。描画FPSや他PCの性能保証ではない）。
- 製品・テストコードの最終コンパイルwarning/error 0件。外部Packageは未改変。Unityライセンス署名Code 10/Access token unavailableとNDMFのNoto Sans CJK JP未検出メッセージは残るが、全テスト正常終了。
- 合成12 Renderer × 60 Shapeを使い、WindowsのUnity対話画面（Dark skin）でRescan、横/縦scroll、固定Renderer列、大小文字混在の検索、Relevant/Missing切替、RendererクリックとHierarchy連動、Header/セルTooltip、Missing + Custom情報、Detailsを目視確認した。
- 操作中にSceneの未保存マークが付かず、保存SceneのSHA256も操作前後で一致。Prefab/serialized値の不変性と参照削除時の無効化は上記Editor Testで検証。Light skinは未目視。
- 目視で見つかったRenderer行の2行テキスト切れをStyleのfixedHeight解除で修正。Window再有効化時の空エラー表示も状態初期化で修正し、再コンパイル後にRescan案内へ戻ることを確認した。
- 対話検証中の外部Package再コンパイルでは従来のMA/NDMF warningに加え、CancellationTokenSourceの二重Disposeメッセージが1件あった。最終batchテストでは再現せず、製品コードの例外・テスト失敗はない。

### Single Cell Binding Writeの実測結果（2026-09-25）

- Unity 2022.3.22f1 / MA 1.18.7で最終コンパイル成功。EditMode全40件成功、失敗・skip 0件。
- 既存/新規Componentへの単一追加、Source参照、Local名fallback、MA標準OnValidate後と一致するRemap初期値、古い要求の重複拒否を検証。
- Scan後に生じたcustom/other-source/broken競合、Shape消失、Source変更、Mesh差替え、Avatar外への移動、Component追加、削除済みObject、複数MA Componentを拒否することを検証。
- 既存Curveのキー・wrap mode・参照を追加処理が変更しないこと、拒否時のScene dirty/serialized値/Undo Group不変性を検証。
- Component既存/新規の両方でUndo/Redo成功。Prefab Instanceの既存Binding Override、新規Component Override、保存Sceneの再読込後の参照解決、元Prefabのbyte列不変性を検証。Prefab Assetへの直接書き込みは拒否。
- Windowのセル選択/描画でSceneをdirtyにせず、Add Sync後とUndo/Redo後に再Scanし、選択とMatrix表示を更新することを検証。既存の読み取り専用回帰テストも全件成功。
- Windows Unity対話画面（Dark skin）で、合成Prefabの既存/新規Component双方についてMissing選択、Add Sync、○→●、MA標準InspectorのSource/Shape/同名fallback/直線Remap、Ctrl+Z、Ctrl+Y、Componentの消去/復元を目視確認。
- Prefab Override一覧で既存MA Binding変更と新規MA Component追加を確認。検証用Scene/Asset/helperはRepositoryから除去済み。Light skinと実アバター資産での目視は未実施。
- 製品・テストコードの最終warning/errorは0件。Unityのライセンス署名Code 10/Access token unavailableとNDMFのフォント未検出ログは残る。対話ログにはUnity更新確認のHTTP 404もあるが、機能の例外はない。
- 書き込み後の意図的な例外注入によるRollbackテストは未実施。例外時は専用Undo GroupをRevertし、Rollback失敗もUIへ報告する設計。

### 初期構築時の実測結果

- Unity 2022.3.22f1: コンパイル成功。グラフィックス有効のbatchmodeでOpenWindowを実行し、終了コード0。
- 製品コードのコンパイルerror/warning: 0件。
- 初回フルコンパイルの外部Package warning: 重複を除いて39件（MA 36、NDMF 3）。CS0618とnullable関連警告等。外部Packageを改変して抑制していない。
- UnityログにLicensing Client署名検証Code 10とAccess token unavailableが残る。その後ライセンス更新・権利解決は成功し、上記の実行は正常終了した。
- 長い実パスではSDKアセットのパスが278文字となりDirectoryNotFoundExceptionが発生した。同じプロジェクトを短いJunction経由で起動すると解消した。clone先は短く保つ。
- `-nographics`によるWindow表示警告は、グラフィックス有効での再検証で解消した。
- 対話画面の目視確認は未実施。batchmodeでのWindow生成まで確認した。
- VPM CLIのcheck package、actionlint 1.7.12による両workflowの静的検証、厳格UTF-8/BOM/JSON/asmdef/Markdown検査、製品.metaの存在・GUID重複検査に合格。
- Unity生成の.meta/.assetにある空値後のスペース、および未改変の上流bootstrapの空白は.gitattributesで差分の末尾空白検査から除外する。

## GitHub Actionsと公開

- `release.yml`: 手動実行。`Packages/${{ vars.PACKAGE_NAME }}`からZIP、unitypackage、Manifestを生成しReleaseする。
- `build-listing.yml`: 手動、Releaseイベント、成功したBuild Releaseを契機にVPM ListingとWebsiteを生成する。
- Repository Variable `PACKAGE_NAME`: `com.nokotaro.blendshape-sync-mapper`を設定済み。
- `Website/`のScriban式はListing生成に必要なTemplate構文で、未置換の製品placeholderではない。
- GitHub Pagesは公開時にSourceをGitHub Actionsへ設定する。今回Release/Listing workflowは実行しない。
- package.jsonのurlは将来の0.1.0ダウンロード先。正式Release前のため現在は存在しない。

Release workflowは梱包処理であり、Unityコンパイルの成功を保証しない。公開前には別途Unityで検証する。
