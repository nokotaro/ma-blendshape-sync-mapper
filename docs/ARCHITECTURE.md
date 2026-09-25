# Architecture

## 目的と唯一のSource of Truth

**既存のModular Avatar Blendshape Sync Componentを唯一のSource of Truthとする。**
既存改変済み衣装へ後付けできることを主要要件とする。専用データへの移行や、衣装階層の再生成を要求しない。

MVPでは既存Componentを読み取り、一覧化し、不足Bindingを検出・追加し、必要に応じて既存Bindingを編集する。
独自Mapping Database、Serialized Mapping Component、Profile Asset、Runtime Sync Systemは作らない。

## 現在の実装範囲

Source RendererとTarget Rootを指定し、Rescanで読み取り専用のSnapshotを作成する。
Inactiveを含むTarget Root自身と子孫のSkinnedMeshRendererを検索し、Source Renderer自身を除外する。
WindowはRenderer × Source BlendShapeのMatrixとRenderer単位の集計・Detailsを表示し、行クリックでは選択行のUI stateとSelection.activeGameObjectだけを変更する。
セルクリックは選択のみ。DetailのAdd Syncから安全なMissingセル1件のexact同名Bindingだけを追加する。
安全なMissing全件はPreview確認後に一括追加できる。既存Binding更新・削除・修復、Remap編集は未実装。

## 責務分割

- `Editor/UI/`: 入力、Rescan要求、Snapshot表示、Hierarchy選択。
- `Editor/Analysis/`: Mesh名取得、Hierarchy走査、compatible/exact/missing照合、Snapshotと分類モデル。
- `Editor/ModularAvatar/`: Readerによる全Component/Binding読取・公開APIでの参照解決。別のWriterが再検証・単一追加・Undo/Prefab処理を所有する。
- `Editor/Utilities/`: 複数箇所で必要になった共通処理のみ。

Utilitiesは未作成。独自Runtime/Builder/NDMF Passは設けない。

## Snapshotと集計の定義

Snapshotは非シリアライズの通常C#オブジェクトで、Window内だけに保持する。AssetやMapping DBとして保存しない。
入力変更・参照していたUnity Objectの削除・Source/TargetのsharedMesh差し替えで破棄して再Scanを促す。
OnGUIはSnapshotの表示と既存参照の有効性確認を行う。Hierarchy/Mesh再探索はRescan時と明示的なAdd Sync直前だけ。
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
単一追加の条件とUndo/Prefab差分は後述のWriter仕様に従う。

## 対応下限の判断

初期依存範囲は`>=1.18.7 <2.0.0-a`、導入・検証版は安定版1.18.7。
これは実PackageのAPI・Assembly確認とUnity検証に基づく初期サポート下限であり、Blendshape Sync APIの初登場版を意味しない。
Foundation時に設定した検証済み下限を維持し、今回のReaderもMA 1.18.7の公開APIで検証する。
1.18.7未満や将来版の動作確認はしていない。下限を下げる場合は対象版を導入して検証する。

履歴ではRemapCurve導入が`a49410d`（最初の安定版1.18.0）、null参照を飛ばす修正が`862bc41`（最初の安定版1.18.4）に含まれることも確認した。
この履歴だけを根拠に、過去版全体への互換性を宣言しない。

## 読み取り専用Matrix

`UI/BlendshapeMatrixViewModel.cs`は既存Analysisから再構築できる非シリアライズの一時View Model。
Scanner/Readerの判定・公開データは変更しない。セルの基本状態はSynced / Missing / NotAvailable。
CustomとBrokenは補助情報であり、MissingCount = CompatibleCount - SyncedCountを変更しない。

- `●`: exact同名Syncあり。
- `○`: compatibleだがexactなし。
- `△`: Missingに加えて、その同名Target Shapeを有効なcustom Bindingが使用している。別Sourceからのcustom占有も含む。
- `-`: Targetに同名Shapeなし。別名Mappingがあれば`- C`となり得る。
- `C`: 関連customあり（Synced/Missing/NotAvailableと併存）。`△`では重複表示を省く。
- `×`: 選択Sourceと現存Source Shapeへ確実に関連付けられるBrokenあり。

関連付けは2種類を区別する。有効なBindingのTarget名が列名と一致する場合はTarget側占有として表示する。
選択Sourceを参照し、Source名が列名と一致し、Source参照/Shapeが有効なBindingはSource側の対応として表示する。
他Sourceの同名文字列だけでSource側の関連付けをしない。解決不能・Source欠落等のBrokenをセルへ推測で割り当てない。
全custom/other-source/broken Bindingは関連付けの可否にかかわらずRenderer Detailsに残す。
同じBindingが両方の関連付けに一致してもTooltipには1回だけ表示する。

列はSourceの生のShape順・indexを保持する。SearchはOrdinalIgnoreCaseの部分一致。
Relevantは少なくとも1行でcompatibleまたは上記関連Bindingがある列、Missingは少なくとも1行でMissing exactとなる列、All Sourceは全列。
全Rendererの集計値は表示列の絞り込みでは変えない。検索・フィルタ・選択は再Scanしない。

`UI/BlendshapeMatrixView.cs`は固定幅200のRenderer列と112のShape列を描画する。
単一の縦scroll値を両paneで共有し、横scrollはMatrixとHeaderだけに適用する。通常wheelは縦、Shift+wheelは横。
Shape名は固定幅でclipし、完全名はHeader Tooltipに出す。記号を主に使い、選択色にはLight/Pro用の色と標準GUIStyleを使う。
表示範囲にある行・列だけ描画し、GUIContent/Tooltip/DetailsはSnapshot構築時にcacheする。
フィルタの列indexリストは検索/View変更時のみ再作成する。Repaint時にMA再読取・大規模LINQ・全セル再構築は行わない。
入力変更や参照削除、Window再有効化でAnalysisとViewをまとめて破棄する。セルは選択ボタンであり、クリックだけでは書き込まない。

## 単一セルWriter

`ModularAvatarBlendshapeSyncWriter.TryAddExactSync`は一時ExactSyncRequestと現在のSource/Rootを受け、AddSyncResultを返す。UIはMA Listを操作しない。
Requestは元Snapshot・Renderer行・Shape名を保持するだけで、Serializeしない。MA Componentが唯一のSource of Truthであることは変わらない。

- 両Meshの現存Shape、Source/Rootの同一性、TargetのRoot内配置、Mesh差替え、削除済み参照を検証する。
- 同じAvatar rootに属する編集可能なScene Object/Prefab Instanceに限定。Prefab Asset直接編集・Prefab Mode・Play Modeは拒否する。
- `AvatarObjectReference.AVATAR_ROOT`をClone().Get(source/target)で別々に解決し、同一Avatarを確認する。`Set(source.gameObject)`で新しい参照を作り、Clone().Get(target)が実際のSource GameObjectへ解決することも確認する。MAのGetComponent解釈と異なるSMRも拒否する。MAのinternal RuntimeUtilは利用しない。
- 書き込み直前にScanner/Readerで現在のTargetを再解析する。Snapshotだけでは許可しない。
- exactありはAlreadyExists。CustomのTarget占有または選択Source Shapeからの既存customはCustomConflict。他Sourceの同名Target占有も拒否する。
- 解決不能Bindingはセルへ確実に割り当てられないため、今回はRenderer内にBrokenが1件でもあれば全セルの追加を拒否する。Matrixの表示・Missing集計の意味は変えない。
- Component 0個はUndo.AddComponent、1個は再利用、複数は拒否する。null List等の診断がある場合も拒否する。
- 同期処理内で検証と追加を完結し、古い要求の再実行も最新exact判定で重複を防ぐ。

MA 1.18.7の標準Inspectorの候補生成はLocalBlendshape未設定、Remap未初期化。OnValidateはRemapを0→0、100→100の2キー、左右Linear tangent、broken tangent、valid=trueへ正規化する。
WriterはLocalBlendshapeを空文字（同名fallback）とし、新規Remapだけをその正規化済み初期値で作る。MAと同じAddKey手順を使い、Keyframe内部値とwrap modeの一致をテストする。
既存BindingのList要素・参照・Curveは変更しない。Component全体のOnValidateやSerializedObject.Applyを呼ぶと既存Curveも正規化されるため、標準Inspector同様に直接Listへ1件追加する。
MA自身の後続Inspector編集やUndo再読み込みでMAのOnValidateが走ることは外部Packageの挙動であり、本ツールが既存Curveを修復する機能ではない。

Undo.RecordObjectとUndo.AddComponentを専用Groupへまとめ、Prefab Instanceでは変更後にRecordPrefabInstancePropertyModificationsを呼ぶ。
FlushUndoRecordObjectsで確定し、次操作とGroupを分離する。例外はResultへ変換し、作成済みGroupをRevertする。Rollback失敗もWindowへ表示する。
正常なScene dirtyはUnity Undo経由で管理し、Scan/選択/TooltipではDirtyを設定しない。
成功後とUndo/Redoイベント後はSnapshotを再構築し、RendererとShape名による選択を復元する。Missing filterでは追加済み列が非表示になる場合も選択Detailは保持する。

## Safe Bulk Add

`BulkAddPreview`はAnalysis Snapshotから再構築する非シリアライズの一時データ。対象はSearch/Viewとは独立した、Scan全体のMissing exact同名セル。
Renderer階層順、Source Shape index順で収集し、Renderer + Shape名で重複を除く。既存Bindingの順序を変更せず、新規分を末尾へ追加する。

- Safe: 単件WriterのCheckSelectionが許可するMissing。同名両Shapeがあり、exact・Custom・OtherSource競合がなく、Renderer全体にBroken/参照診断がなく、Componentが0または1個で、編集可能な同一Avatar内のScene Object/Prefab Instance。
- Require Review: MissingだがWriterが追加を許可しないもの。候補ごとにWriterの理由を表示し、自動修復・上書きしない。
- Missing = Safe + Review。Custom/Brokenの全Binding件数とは異なり、ここではMissing exactセルを数える。BrokenだけでCompatibleがないRendererは候補に含まず、従来のDetailsで診断する。

WindowはRescan時にPreviewを準備し、件数を表示する。`Review Safe Changes`でRenderer別のShape/分類/除外理由を確認した後、`Add N Safe Missing Syncs`を明示実行する。MVPはSafe全件方式で個別checkboxは設けない。Previewを開く・戻る・Searchを変える操作は書き込まない。

`BlendshapeSyncBulkAdd`は次の二段階処理だけを所有する。MA List・Binding初期値・Remap生成は従来Writerに集約する。

1. 全Safe候補についてWriter.Preflightで現在のSource/Root/Shape/参照/競合/Component同一性を検証。1件でも変化があれば書き込み0件で終了し、再Scan後のPreview再確認を要求する。
2. 外側Undo Groupを開始し、Writer.TryAddExactSyncを順次実行。各追加も同じPreflightを通す。全件成功でGroupをCollapseし、Component作成を含む全追加を1回のUndo/Redoにする。途中拒否・例外はRevertAllDownToGroupで全体を戻す。Rollback自体の失敗も明示表示する。

同じRendererへの後続追加では、この呼び出し内でWriter自身が作成したComponentだけを再利用する。元SnapshotのComponentが外部から変わった場合は引き続き拒否する。単件APIはこの例外を持たない。
故障注入はinternal overloadの呼び出し単位delegateで行う。N件追加後に例外を発生させ、作成Component・既存Binding・Prefab Override・直前のユーザーUndoが保持/復元されることをテストする。製品UIはdelegateを渡さない。
MAのOnValidateはUndo復元時に既存Remapも正規化するため、失敗時はWriterが退避した既存Binding/参照/CurveのコピーをUndo後に復元する。これはトランザクション内だけの一時退避であり、永続Mapping DBではない。成功後の通常Undo/RedoはUnity/MA標準の挙動に従う。

処理は同期実行。現段階ではProgress/Cancelは設けない。完了・失敗後は再Scanし、成功件数と残るReview件数を表示する。Filterで追加済み列が消えても結果メッセージを保持する。Undo/Redoでも再Scanする。
