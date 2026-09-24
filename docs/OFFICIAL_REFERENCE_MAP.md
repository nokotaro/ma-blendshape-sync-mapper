# Official Reference Map

確認日: **2026-09-24**。一次資料を優先し、対象バージョンのソースと現行ドキュメントを区別する。

| 主題 | 一次資料 | 確認内容 |
| --- | --- | --- |
| VRChat supported Unity | https://creators.vrchat.com/sdk/upgrade/current-unity-version/ | Unity 2022.3.22f1 |
| VPM Package | https://vcc.docs.vrchat.com/vpm/packages/ | package.json、vpmDependencies |
| VPM CLI | https://vcc.docs.vrchat.com/vpm/cli/ | add package、resolve project、check package |
| 公式VPM Template | https://github.com/vrchat-community/template-package/tree/591e23fe5175b8279cc3998f65e81add604aabc9 | Unity project構成、Release/Listing workflow |
| Unity Custom Package layout | https://docs.unity3d.com/2022.3/Documentation/Manual/cus-layout.html | Editor Assemblyと.meta管理 |
| Modular Avatar documentation | https://modular-avatar.nadena.dev/docs/intro | MAの役割と導入 |
| MA Blendshape Sync | https://modular-avatar.nadena.dev/docs/reference/blendshape-sync | Renderer間の連動、連鎖等の制限 |
| MA GitHub | https://github.com/bdunderscore/modular-avatar/tree/1.18.7 | 採用した安定版の実コード |
| MA Component | https://github.com/bdunderscore/modular-avatar/blob/1.18.7/Runtime/ModularAvatarBlendshapeSync.cs | Bindings、BlendshapeBinding、RemapCurve |
| MA Object Reference | https://github.com/bdunderscore/modular-avatar/blob/1.18.7/Runtime/AvatarObjectReference.cs | Get(Component)、Set(GameObject) |
| MA Assembly | https://github.com/bdunderscore/modular-avatar/blob/1.18.7/Runtime/nadena.dev.modular-avatar.core.asmdef | 実際のAssembly名 |
| MA Manifest | https://github.com/bdunderscore/modular-avatar/blob/1.18.7/package.json | MA 1.18.7、NDMF推移依存 |
| MA RemapCurve導入履歴 | https://github.com/bdunderscore/modular-avatar/commit/a49410d | 1.18.0からのデータ構造変更 |
| MA null参照修正履歴 | https://github.com/bdunderscore/modular-avatar/commit/862bc41 | 1.18.4からの修正 |
| MA VPM repository | https://vpm.nadena.dev/vpm.json | 安定版配布Manifest・SHA256 |
| VRChat公式VPM repository | https://packages.vrchat.com/official | Avatars/Base 3.10.5配布Manifest |

## 開発運用の補助資料

https://github.com/sechiro/VRCUdonSkills-for-Codex/tree/1a19d149b1730cae4dbde9dd5d9a4e4aa50d0c0f

SkillはVRChat/Unityの公式仕様ではない。採用範囲をAGENTS.mdへ移して管理する。
World/UdonSharp用の規則をAvatar Editor Packageへ自動適用しない。
未確認のAPIや互換性は未確認と記録し、推測を仕様として扱わない。
