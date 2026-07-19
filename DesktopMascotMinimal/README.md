# DesktopMascotMinimal

Unity **6.5（6000.5.4f1）** と UniVRM **0.131.1** を対象にした、Windows用3Dデスクトップマスコットの最小プロジェクトです。

## 実装済み

- VRM 1.0用UniVRMパッケージ
- 黒色カラーキーによる透明背景
- 枠なし・常に最前面
- マスコット以外のクリック透過
- キャラクター上でのウィンドウドラッグ
- クリック時のAnimator Triggerまたは簡易バウンス
- ウィンドウ位置のJSON保存
- サンプルシーン自動生成
- Windows x86-64ビルドメニュー
- Private GitHub向け`.gitignore`と初回登録手順

## 必要環境

- Unity 6.5 / 6000.5.4f1
- Windows 10または11
- Unity Windows Build Support
- Git for Windows

UniVRMはUnity Package ManagerがGitHubから取得します。

## 最初に行うこと

1. Unity Hubでこのフォルダーを開く
2. Unity 6.5（6000.5.4f1）を選択する
3. Package Managerとコンパイルの完了を待つ
4. Unityメニューから次を実行する

```text
Tools > Desktop Mascot > Create or Rebuild Sample Scene
```

生成されるシーン:

```text
Assets/_Project/Scenes/DesktopMascot.unity
```

Play Modeではカプセル型プレースホルダーのクリック反応を確認できます。透明化、最前面、クリック透過、ネイティブドラッグはWindows向けビルドで確認してください。

## VRMモデルへの置き換え

1. VRM 1.0ファイルを `Assets/_Project/Models` へ置く
2. UnityへインポートされたVRM Prefabを `MascotRoot` の子へ配置する
3. `PLACEHOLDER_Replace_With_VRM_Prefab` を削除する
4. モデルのルート付近へBox ColliderまたはCapsule Colliderを追加する
5. ColliderのGameObjectを `Mascot` レイヤーにする
6. 同じGameObjectへ `MascotInteraction` を追加する
7. `Window`へSystemsの `Win32Window` を割り当てる
8. Animatorを使用する場合は `Reaction` Triggerを用意する

実際の`.vrm`ファイルは既定でGit管理から除外されます。

## Windowsビルド

```text
Tools > Desktop Mascot > Build Windows x86_64
```

出力先:

```text
Build/DesktopMascot/DesktopMascot.exe
```

## GitHubへ登録

詳しい手順は以下を参照してください。

```text
Docs/GITHUB_SETUP.md
```

Windowsでは、次の補助スクリプトで初回ローカルコミットを作成できます。

```text
Tools/Initialize-GitRepository.ps1
Tools/Initialize-GitRepository.bat
```

## リポジトリへ保存するもの

- Assets
- Packages
- ProjectSettings
- Docs
- Tools
- README.md
- .gitignore
- .gitattributes

## 保存しないもの

- Library
- Temp
- Logs
- UserSettings
- Build
- ローカルのVRMモデル
- APIキーや認証情報

## 注意事項

- 現在は黒色を透明色として扱うため、モデル内の完全な黒色も透明になる場合があります。
- VRM 0.xは対象外です。
- このプロジェクトはUnity 6.5のプロジェクト形式に固定していますが、この生成環境にはUnity Editorがないため、6000.5.4f1上での実コンパイル確認は行っていません。
