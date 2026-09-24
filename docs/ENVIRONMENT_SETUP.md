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
- メニューからWindowが開き、Title、Source Renderer、Target Root、無効なRescanが表示されることを確認する。
- 現段階ではRenderer/Bindingの読み取りも変更も実行しない。
- 製品asmdefのincludePlatformsがEditorだけであることを確認する。
- 製品ファイル・フォルダにUnity生成の.metaがあり、Git追跡対象であることを確認する。
- 新規UTF-8ファイルのBOM・置換文字、JSON/YAML構文、Git差分、デモ残骸を確認する。

Windowsのバッチコンパイル・Window生成確認例（Unity実行ファイルのパスは実環境に合わせる）:

```powershell
Unity.exe -batchmode -quit -projectPath . -executeMethod Nokotaro.BlendshapeSyncMapper.BlendshapeSyncMapperWindow.OpenWindow -logFile foundation.log
```

Window生成確認では`-nographics`を付けない。グラフィックス無効時には表示警告が出る。
バッチでのWindow生成と対話画面の目視確認は別の検証として記録する。

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
