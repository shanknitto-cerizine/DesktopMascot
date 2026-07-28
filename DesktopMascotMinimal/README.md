# あなたといつも

Unity **6.3 LTS（6000.3.20f1）** と UniVRM / VRM 1.0を対象にした、
Windows用3Dデスクトップマスコットです。

## 製品名と開発名

ユーザー向け製品名は「あなたといつも」、内部の開発・プロジェクト名は
`DesktopMascotMinimal`です。この2つは用途を分けて維持します。

`Minimal`は、機能や品質を意図的に減らすという意味ではありません。
機能数や視覚的な派手さ、不必要な複雑さよりも、予測可能で堅牢な動作、
保守性、抑制された複雑さを優先する設計姿勢を表しています。

「あなたといつも」は、いつでも身近にいるデスクトップ上の相棒という
考えから付けた製品名です。名称の発想には「どこでもいっしょ」への
敬意がありますが、本プロジェクトは独立した名称・設計・キャラクターで
開発しており、第三者による承認、提携、許諾を示すものではありません。

位置情報の保存には、表示名とは独立した安定した内部識別子を使用します。
既存の保存先
`%LOCALAPPDATA%\DesktopMascotMinimal\window-position.json`は変更・移行
しません。

アプリケーション設定は同じ内部保存ディレクトリの`settings.json`へ
保存します。2つのファイルは意図的に分離しています。

- `window-position.json`: 高頻度に変化するネイティブウィンドウ位置、
  画面境界回復、ドラッグ完了位置
- `settings.json`: バージョン管理されたアプリケーション設定と将来の
  ユーザー設定

M-033では位置情報を`settings.json`へ移動・複製せず、自動移行も
行いません。

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

- Unity 6.3 LTS / 6000.3.20f1
- Windows 10または11
- Unity Windows Build Support
- Git for Windows

UniVRMはUnity Package ManagerがGitHubから取得します。

## 最初に行うこと

1. Unity Hubでこのフォルダーを開く
2. Unity 6.3 LTS（6000.3.20f1）を選択する
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
Build/DesktopMascot/あなたといつも.exe
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

## M-034 Settings UI Foundation

The production Settings window is optional and starts closed. In normal
runtime the Unity Player is hidden and acts as an on-demand Settings host.
Open Settings from the native mascot menu, the tray menu, or a tray-icon
double-click. Close it with the Settings `Cancel` or `Close` control. Product
keyboard shortcuts, including `F10` and Settings `Escape`, are intentionally
unsupported.

The first editable setting is `firstRunCompleted`. `Apply` validates and saves
the editable copy through `SettingsManager`; `Cancel` discards edits;
`Restore Defaults` changes only the editable copy until `Apply` is selected;
and `Close` discards unapplied edits. The Apply button is enabled only when
the ViewModel is valid and dirty.

The UI is divided into `SettingsWindowController`, `SettingsViewModel`,
`SettingsBinding`, and `SettingsValidation`. Controls never access the
filesystem. `settings.json` remains application configuration, while
`window-position.json` remains independent high-frequency window state.

Settings is drawn over a dedicated full-resolution Player preview Camera.
That Camera renders directly to the Player backbuffer and does not allocate a
preview RenderTexture. The separate 256 x 256 normalized texture remains
exclusive to the native D3D12 transfer, alpha mask, window region, and
DirectComposition pipeline.

## M-035 Native mascot context menu

Right-click the visible DirectComposition mascot body to open its native
Windows popup menu. It contains exactly:

- `設定`: opens the existing Settings UI and makes the Unity Player available
  for interaction
- `終了`: requests the existing managed orderly-shutdown path

Transparent pixels remain outside the active `SetWindowRgn`, so right-clicks
there continue through to the window behind the mascot. Cancelling the menu
does nothing. The popup does not replace left-button dragging, does not use a
tray icon, and does not change either persistence schema.

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
- 内部のリポジトリ名、Unityプロジェクトフォルダー、名前空間、
  `DesktopMascotNative.dll`は製品表示名へ変更しません。
## System tray

Normal Windows runtime registers one notification-area icon for
`あなたといつも`. Right-click opens a native menu containing only `設定` and
`終了`; left double-click opens Settings, while a single left-click does
nothing. Both the tray and the native mascot menu use the same Settings
controller and orderly-shutdown command boundary.

The tray uses a dedicated hidden native owner window rather than the
DirectComposition mascot window. A stable GUID allows `TaskbarCreated`
recovery after Explorer restarts. The current small cyan/yellow/pink icon is
an application-owned, dynamically generated temporary product icon; its
`HICON` is destroyed exactly once during orderly cleanup.

M-036 intentionally leaves the M-034 Cancel and Close behavior unchanged:
both discard unapplied edits and close Settings. It does not add About,
character switching, a settings-schema change, or a third tray command.

## Production Player visibility

M-037 makes the existing Unity Player an on-demand Settings host in normal
runtime. After the DirectComposition mascot path is initialized, the Player
is application-cloaked with `DwmSetWindowAttribute(DWMWA_CLOAK)`. This keeps
the Unity rendering surface visible to its Player loop while DWM omits the
window from the user-visible desktop. The mascot, animation, GPU transfer,
alpha-region updates, command polling, and tray icon continue because the
rendering surface remains active and the production runtime keeps
`Application.runInBackground` enabled until final cleanup.

The initial cloak is requested at Unity's `BeforeSplashScreen` managed
initialization point. Startup diagnostics report elapsed time from process
creation through cloak completion as `Player Flash Measured`. Unity creates
its native Player window before managed callbacks are available, so a strict
zero-millisecond guarantee is not possible; the product quality target is no
visibly long startup Player display.

Both native Settings entry points use one centralized managed visibility
owner. It identifies and caches only the current process's exact
`UnityWndClass`, restores it when minimized, places an inaccessible Player
back in a current monitor work area, shows it, and reuses the existing
`SettingsWindowController`. `Cancel` and `Close` discard and close as before,
then hide only the Player. `Apply` and `Restore Defaults` leave Settings
visible.

Ordinary Settings hiding never calls `ShowWindow(SW_HIDE)`, never posts
`WM_CLOSE`, and never quits the application. Showing Settings removes only
the application cloak. The M-036 final shutdown remains separate: after the shutdown
barrier and all cleanup invariants, one exact cached `UnityWndClass`
`WM_CLOSE` is posted and `Application.Quit()` is requested. Diagnostic modes
retain their established visible Player behavior.
