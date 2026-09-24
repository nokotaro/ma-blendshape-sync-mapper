# AGENTS.md

## 読み順

1. このファイル
2. `docs/ARCHITECTURE.md`
3. `docs/ENVIRONMENT_SETUP.md`
4. `docs/OFFICIAL_REFERENCE_MAP.md`
5. 変更に関係する製品コードとREADME

## プロジェクトの境界

- MA Blendshape Sync MapperはVRChat Avatar向けのEditor-only VPM Package。
- Unity 2022.3.22f1を使用する。
- 製品コードと配布Manifestの正本は`Packages/com.nokotaro.blendshape-sync-mapper/`。
- 既存のModular Avatar Blendshape Sync Componentを唯一のSource of Truthとする。
- 独自Mapping DB、Serialized Mapping Component、Profile Asset、Runtime Sync Systemを作らない。
- Windowの選択状態・スクロール位置等の一時状態は保持できる。解析結果はComponentから再構築できる一時スナップショットに限定する。
- EditorコードはEditor限定asmdefに収める。独自Runtime層、Builder、NDMF Passは明示的な要件なしに追加しない。
- 現在は読み取り専用Scan・Matrix・Renderer Detailsまで。製品コードにUndo、AddComponent、Binding書き込み、Prefab変更、Dirty設定を追加しない。
- Editor Testの一時Mesh/GameObject/Scene/Prefab作成は検証用fixtureに限定し、必ず後片付けする。製品の書き込み機能と混同しない。

## 編集範囲

- 通常の製品編集: `Packages/com.nokotaro.blendshape-sync-mapper/`。
- 開発文書: ルートREADME、AGENTS、`docs/`。
- 開発環境・配布設定: 必要な場合に限り`ProjectSettings/`、PackagesのManifest/lock、`.github/`、`Website/`。
- `Assets/`は開発・検証用。外部アバターや衣装の有償・私有資産をcommitしない。
- `Library/`、`Temp/`、`Logs/`、`UserSettings/`、解決済みの外部Packageを製品コードとして編集・commitしない。
- Unity生成の製品`.meta`を追跡し、GUIDを不用意に作り直さない。

## Windows安全編集

1. `apply_patch`を第一選択にする。
2. 新規Markdown、C#、JSON、asmdef、YAMLはUTF-8 BOMなしを基本とする。
3. 既存の文字コード・改行を無関係な編集で変更しない。`.editorconfig`と`.gitattributes`に従う。
4. PowerShellの危険な多段文字列置換、展開付きhere-string、文字コード未指定のSet-Contentを避ける。
5. apply_patchが環境由来で失敗した場合のみ、安全なテンプレートコピー、またはUTF-8を明示した最小限の書き込みを使う。
6. 編集後は厳格なUTF-8再読込、BOM、置換文字、意図しない連続`?`、不可視制御文字、URL、Markdownのコードフェンス、プレースホルダを確認する。
7. JSON/asmdef/YAMLの構文とC#のUnityコンパイルを変更内容に応じて確認する。
8. 完了・commit前に`git status --short`、`git diff`、ステージ後の`git diff --cached --check`と差分を確認する。

日本語文書・UIを禁止しない。C#識別子は英語、コードコメントは簡潔な英語を基本とする。

## 検証と依存

- 推測より、対象バージョンの実コード・Manifest・asmdefと公式一次資料を優先する。
- MAのBindingsとAvatarObjectReferenceの公開APIを確認する。internal APIを前提にしない。
- 依存範囲の下限と実測版を区別する。APIの導入版、未検証の互換性を断定しない。
- 新規依存は必要性を説明する。NDMFを直接使わない限りMAの推移依存として解決する。
- Unity MCPは任意の開発支援。明示指示なしに導入・必須化・接続設定追加しない。
- FoundationではManifest、Editor限定asmdef、メニュー起動、コンパイル、`.meta`追跡、demo残骸、Releaseの対象パスを確認する。
- 将来の変更では既存Binding保全、Undo、Prefab差分、再実行時の重複防止に応じた検証を行う。
- コンパイルできなかった場合やwarning/errorは理由とともに報告し、未実行を成功と書かない。
- 仕様・環境・依存が変わったら対応するproject-owned文書を同じ変更で更新する。

## Skillから採用した方針

出典: https://github.com/sechiro/VRCUdonSkills-for-Codex/tree/1a19d149b1730cae4dbde9dd5d9a4e4aa50d0c0f

- `codex-edit-stability-windows`: 上記の安全編集・編集後検証を直接適用する。
- `vrc-udon-project-docs`: 仕様、環境、参照根拠を自Repositoryの文書で所有する。
- `vrc-udon-core-module`: 責務分離と他プロジェクトへ配布可能なPackage構造を応用する。
- `vrc-udon-knowhow-transfer`: 外部知見は採用範囲を整理して文書へ反映する。import/exportを常設しない。
- `vrc-udon-unity-mcp`: Editor補助と製品依存を分離する考え方のみ採用する。

UdonSharp固有のNetworking、Ownership、Late Joiner、Persistence、Player Data、World Runtime、Fukuro Udon、DataList優先規則は適用しない。
固定長配列優先、List/Dictionary/LINQの制限、Builder First、World空間UI、VS Code固有リンク形式も適用しない。
外部Skillが存在しない環境でも、このファイルのルールを開発の基準とする。
