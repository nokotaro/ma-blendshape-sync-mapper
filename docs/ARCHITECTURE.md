# Architecture

## 目的と唯一のSource of Truth

**既存のModular Avatar Blendshape Sync Componentを唯一のSource of Truthとする。**
既存改変済み衣装へ後付けできることを主要要件とする。専用データへの移行や、衣装階層の再生成を要求しない。

MVPでは既存Componentを読み取り、一覧化し、不足Bindingを検出・追加し、必要に応じて既存Bindingを編集する。
独自Mapping Database、Serialized Mapping Component、Profile Asset、Runtime Sync Systemは作らない。

## 現在の実装範囲

Source RendererとTarget Rootを指定し、Rescanで読み取り専用のSnapshotを作成する。
Inactiveを含むTarget Root自身と子孫のSkinnedMeshRendererを検索し、Source Renderer自身を除外する。
WindowはRenderer単位の集計を表示し、行クリックではSelection.activeGameObjectだけを変更する。
Scene、Prefab、Mesh、MA Componentへの書き込み、Undo、Dirty設定は存在しない。
Matrix編集、Binding追加・更新・削除は未実装。

## 責務分割

- `Editor/UI/`: 入力、Rescan要求、Snapshot表示、Hierarchy選択。
- `Editor/Analysis/`: Mesh名取得、Hierarchy走査、compatible/exact/missing照合、Snapshotと分類モデル。
- `Editor/ModularAvatar/`: MA固有型への依存、全Componentと全Bindingの読取、公開APIでの参照解決。
- `Editor/Utilities/`: 複数箇所で必要になった共通処理のみ。

Utilitiesは未作成。独自Runtime/Builder/NDMF Passは設けない。

## Snapshotと集計の定義

Snapshotは非シリアライズの通常C#オブジェクトで、Window内だけに保持する。AssetやMapping DBとして保存しない。
入力変更・参照していたUnity Objectの削除・Source/TargetのsharedMesh差し替えで破棄して再Scanを促す。
OnGUIはSnapshotの表示と既存参照の有効性確認だけを行う。Hierarchy/Mesh再探索はRescan時だけ。
同一Meshの名前一覧と辞書は1回のScan内で共有する。比較はOrdinalで、大小文字を補正しない。
Meshの全index/nameを保持し、名前からのindex解決はUnityのGetBlendShapeIndexを使用する。
compatible/synced/missingは重複を除く名前の数、Source/Target shapesはMeshの生のblendShapeCount。
同名フレームの追加や重複名を独自ルールで補正しない。

| 分類 | 定義 |
| --- | --- |
| Compatible | Source/Targetの両方に同名Shapeがある。 |
| ExactSameName / Synced | 参照が選択SourceのGameObjectへ解決し、BindingのSource名とfallback後のTarget名が同じ、かつ両側のShapeが存在する。1つ以上あればその名前をsyncedとして1回数える。 |
| Missing | Compatibleのうちexact Bindingがない名前。Customが占有していてもMissing exactには含まれるが、自動追加してよい意味ではない。 |
| CustomMapping | 解決可能で両側のShapeが存在し、BindingのSource名とTarget名が異なる。別Sourceを参照するcustomも記録・集計し、ReferencesSelectedSourceで区別する。 |
| OtherSource | 有効な同名Bindingだが、選択Source以外のGameObjectを参照する。選択Sourceに対するsyncedには数えない。 |
| Broken | 参照なし・解決不能・解決例外・参照先SMRなし・参照先Meshなし・Source Shapeなし・Target Meshなし・Target Shapeなしのいずれか。理由はflagsで保持する。 |

Broken判定では、選択Sourceではなく各Bindingの実際の参照先Meshを検証する。
Custom/BrokenはRendererにある全Bindingの件数であり、重複Bindingも勝手に統合しない。
MAがスキップする無効な参照を本ツールではBrokenとして表示する。これは診断用分類で、MAのbuild errorと同義ではない。
nullのBindingsリストはComponent単位のdiagnosticとし、存在しないBinding数を作り出さない。
TargetにMeshがなくてもRenderer行は残し、Componentがなければ空のBinding一覧として扱う。

各CompatibleBlendshapeは同じTarget名を使用する全ExistingTargetBindingsを保持する。
Custom/OtherSource/Brokenの占有をMissingから追跡できるため、将来の編集で既存Mappingを上書きする判断を避けられる。
RendererのHierarchy pathには各Transformのsibling indexを付け、さらにRendererのinstance IDを表示する。
これらはSnapshot内の識別情報であり、永続キーとして扱わない。

## MAデータの意味と保全

MA 1.18.7の実コードで次を確認した。

- `ModularAvatarBlendshapeSync.Bindings`: 既存Bindingのリスト。
- `BlendshapeBinding.ReferenceMesh`: 参照元RendererのGameObjectを指すAvatarObjectReference。
- `Blendshape`: 参照元のBlendshape名。
- `LocalBlendshape`: Componentが付いたRenderer側のBlendshape名。空白ならBlendshape名へフォールバックする。
- `RemapCurve`と`RemapCurveIsValid`: 既存Bindingの一部。将来の編集で失わせない。
- `AvatarObjectReference.Get(Component)`と`Set(GameObject)`が公開されている。

Readerは既存ReferenceMeshのpublic Clone()に対してGet(Component)を呼び出す。
Getは内部キャッシュを更新するため、Cloneで既存参照のキャッシュまで不変に保ち、Rescanで新しく解決する。
文字列パス比較や独自Transform.FindでSource一致を決めない。返されたGameObjectを選択Source.gameObjectと比較する。
MA 1.18.7のGet(Component)は空のreferencePathを解決不能とし、avatar rootを要求する。
非空のpathではavatar内の直接参照を優先し、avatar rootの特殊値、相対path、重複Armatureの補正もMA自身が処理する。
Binding単位で解決例外を捕捉し、他のRendererの解析を継続する。

Component配置はMAの処理と同じGameObject上のGetComponent<SkinnedMeshRenderer>()に従う。
DisallowMultipleComponentがあるが、保存データに複数存在する場合はGetComponentsで全て読み、Component/Binding indexを別々に保持する。
RemapCurveはkeyframe配列とwrap modeをコピーし、RemapCurveIsValidとcurveの有無も保存する。曲線を正規化・書換えしない。

これらは`nadena.dev.modular-avatar.core` Assemblyに存在する。製品asmdefはこのAssemblyを参照する。
MA Editor AssemblyやNDMF APIは現在参照しない。
外部PackageのRuntime Assemblyを参照しても、この製品のAssemblyはEditor限定のままとする。

Scanで自動修復・自動追加は行わない。不足Bindingと壊れた既存参照を区別し、曖昧な対応を勝手に確定しない。
将来の追加操作は不足分だけを対象とし、既存の別名対応・RemapCurve・無関係なBindingを保全する。
将来の書き込みタスクではUndoとPrefab差分を別途設計する。今回の製品コードにはその処理を含めない。

## 対応下限の判断

初期依存範囲は`>=1.18.7 <2.0.0-a`、導入・検証版は安定版1.18.7。
これは実PackageのAPI・Assembly確認とUnity検証に基づく初期サポート下限であり、Blendshape Sync APIの初登場版を意味しない。
Foundation時に設定した検証済み下限を維持し、今回のReaderもMA 1.18.7の公開APIで検証する。
1.18.7未満や将来版の動作確認はしていない。下限を下げる場合は対象版を導入して検証する。

履歴ではRemapCurve導入が`a49410d`（最初の安定版1.18.0）、null参照を飛ばす修正が`862bc41`（最初の安定版1.18.4）に含まれることも確認した。
この履歴だけを根拠に、過去版全体への互換性を宣言しない。

## 次段階のMatrixに渡せる情報

Source/Targetの全Shape index/name、同名のCompatibleShapes、exact有無、Target名を占有する全Binding、Binding分類・Broken理由を利用できる。
BindingごとのComponent参照・ComponentIndex・BindingIndex・実参照先・元のLocal名・fallback後の名・Remap情報も保持する。
Matrixを作る際も表示はこの一時Snapshotから構築し、独自の永続Mappingデータは導入しない。
