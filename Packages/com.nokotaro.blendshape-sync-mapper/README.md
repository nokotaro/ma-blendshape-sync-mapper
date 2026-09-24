# MA Blendshape Sync Mapper

Modular Avatar Blendshape Syncをまとめて可視化・編集するためのUnity Editor Toolです。開発中であり、正式Release前です。

## 現在の機能

`Tools > MA Blendshape Sync Mapper`でWindowを開き、Source RendererとTarget Rootを指定できます。
Rescanは未実装のため無効です。Componentの読み取り・変更やBindingの追加は行いません。

## 対応環境

- Unity 2022.3.22f1 / VRChat Avatarsプロジェクト
- Modular Avatar 1.18.7以上、2.0.0未満の安定版（検証版: 1.18.7）
- Editor-only。UdonSharp Runtimeはありません。

既存のMA Blendshape Sync Componentが唯一のSource of Truthです。独自のMapping DBやProfile Assetは作りません。

VPM Packageとして配布予定です。現時点では公開済みRelease・VCC追加用Listingはありません。
package.jsonのダウンロードURLは将来の0.1.0 Release用で、まだ利用できません。

開発用の導入方法は[環境構築手順](https://github.com/nokotaro/ma-blendshape-sync-mapper/blob/main/docs/ENVIRONMENT_SETUP.md)を参照してください。

## ライセンス

[MIT License](LICENSE.md) / Copyright (c) 2026 nokotaro
