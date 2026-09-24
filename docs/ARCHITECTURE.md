# Architecture

## 目的と唯一のSource of Truth

**既存のModular Avatar Blendshape Sync Componentを唯一のSource of Truthとする。**
既存改変済み衣装へ後付けできることを主要要件とする。専用データへの移行や、衣装階層の再生成を要求しない。

MVPでは既存Componentを読み取り、一覧化し、不足Bindingを検出・追加し、必要に応じて既存Bindingを編集する。
独自Mapping Database、Serialized Mapping Component、Profile Asset、Runtime Sync Systemは作らない。

## 現在の実装範囲

Window shellのみ。Source RendererとTarget RootはEditorWindowの一時的な選択状態。
Rescanは無効であり、Renderer走査、BlendShape取得、Binding解析・変更は未実装。
Window表示や選択操作はScene、Prefab、Mesh、MA Componentを変更しない。

## 将来の責務分割

- `Editor/UI/`: Window、入力、表示、操作要求。
- `Editor/Analysis/`: 読み取り専用の走査・不足判定。結果は再構築可能な一時スナップショット。
- `Editor/ModularAvatar/`: MAの参照解決、Bindingsの読み書き境界。追加・編集は明示操作時だけ。
- `Editor/Utilities/`: 複数箇所で必要になった共通処理のみ。

コードが必要になった時点でフォルダを作成する。独自Runtime/Builder/NDMF Passは設けない。

## MAデータの意味と保全

MA 1.18.7の実コードで次を確認した。

- `ModularAvatarBlendshapeSync.Bindings`: 既存Bindingのリスト。
- `BlendshapeBinding.ReferenceMesh`: 参照元RendererのGameObjectを指すAvatarObjectReference。
- `Blendshape`: 参照元のBlendshape名。
- `LocalBlendshape`: Componentが付いたRenderer側のBlendshape名。空白ならBlendshape名へフォールバックする。
- `RemapCurve`と`RemapCurveIsValid`: 既存Bindingの一部。将来の編集で失わせない。
- `AvatarObjectReference.Get(Component)`と`Set(GameObject)`が公開されている。

これらは`nadena.dev.modular-avatar.core` Assemblyに存在する。製品asmdefはこのAssemblyを参照する。
MA Editor AssemblyやNDMF APIは現在参照しない。
外部PackageのRuntime Assemblyを参照しても、この製品のAssemblyはEditor限定のままとする。

Scanで自動修復・自動追加は行わない。不足Bindingと壊れた既存参照を区別し、曖昧な対応を勝手に確定しない。
将来の追加操作は不足分だけを対象とし、既存の別名対応・RemapCurve・無関係なBindingを保全する。
変更時にはUndoとPrefab差分を扱い、再実行しても同じBindingを増殖させない設計とする。

## 対応下限の判断

初期依存範囲は`>=1.18.7 <2.0.0-a`、導入・検証版は安定版1.18.7。
これは実PackageのAPI・Assembly確認とUnity検証に基づく初期サポート下限であり、Blendshape Sync APIの初登場版を意味しない。
現在のshellはMA APIを呼び出さないため、API要件だけから歴史的な最低版を決めることはできない。
1.18.7未満や将来版の動作確認はしていない。下限を下げる場合は対象版を導入して検証する。

履歴ではRemapCurve導入が`a49410d`（最初の安定版1.18.0）、null参照を飛ばす修正が`862bc41`（最初の安定版1.18.4）に含まれることも確認した。
この履歴だけを根拠に、過去版全体への互換性を宣言しない。

## 次段階の入口

`BlendshapeSyncMapperWindow.OnGUI`のRescanボタンから、将来の読み取り専用Analysis処理を呼び出す。
Source RendererとTarget Rootを入力とし、MA Componentを再読込して一時結果を返す。
この段階でも独自の永続Mappingデータは導入しない。
