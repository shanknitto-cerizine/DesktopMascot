# Private GitHubリポジトリへの初回登録

このプロジェクトは、GitHubの空のPrivateリポジトリへ登録する前提で構成されています。

## 事前準備

- Gitをインストールする
- GitHubアカウントへサインインする
- Unity HubにUnity 6.5（6000.5.4f1）を登録する
- Unity 6.5へWindows Build Supportを追加する

## 方法A: GitHub Web + コマンドプロンプト

### 1. GitHubで空のPrivateリポジトリを作る

GitHubの `New repository` から以下のように作成します。

- Repository name: `DesktopMascotMinimal` など
- Visibility: `Private`
- Add a README file: オフ
- Add .gitignore: None
- Choose a license: None

READMEや.gitignoreをGitHub側で追加せず、空のリポジトリとして作成してください。

### 2. ZIPを展開する

ZIP内の `DesktopMascotMinimal` フォルダーを任意の作業場所へ展開します。

### 3. 初回コミットする

PowerShellまたはコマンドプロンプトでプロジェクトフォルダーを開き、次を実行します。

```powershell
git init
git branch -M main
git add .
git status
git commit -m "Initial Unity 6.5 desktop mascot project"
```

### 4. GitHubへ接続してpushする

`YOUR_NAME`と`YOUR_REPOSITORY`を自分の値へ置き換えます。

```powershell
git remote add origin https://github.com/YOUR_NAME/YOUR_REPOSITORY.git
git push -u origin main
```

認証を求められた場合は、ブラウザー認証またはGit Credential Managerを使用します。

## 方法B: GitHub CLI

GitHub CLIをインストールしてログイン済みなら、プロジェクトフォルダーで次を実行できます。

```powershell
git init
git branch -M main
git add .
git commit -m "Initial Unity 6.5 desktop mascot project"
gh repo create DesktopMascotMinimal --private --source=. --remote=origin --push
```

## 方法C: GitHub Desktop

1. GitHub Desktopを起動する
2. `File > Add local repository` を選ぶ
3. 展開した `DesktopMascotMinimal` を指定する
4. リポジトリでないと表示されたら `create a repository` を選ぶ
5. 最初のコミットを作成する
6. `Publish repository` を押す
7. `Keep this code private` をオンにして公開する

## VRMモデルについて

既定の `.gitignore` は、以下に置かれたVRMモデルをGitへ登録しません。

```text
Assets/_Project/Models/*.vrm
```

モデルの再配布条件が明確で、Privateリポジトリへ含めたい場合のみ、`.gitignore`のVRM除外行を削除してください。

## 初回push前の確認

```powershell
git status
```

次が登録対象に含まれていないことを確認します。

- Library
- Temp
- Logs
- UserSettings
- Build
- 実際に使用するVRMモデル
- APIキーや認証情報

## 2台目のPCで開始する場合

```powershell
git clone https://github.com/YOUR_NAME/YOUR_REPOSITORY.git
```

クローン後、Unity HubからプロジェクトフォルダーをUnity 6.5（6000.5.4f1）で開きます。`Library`はUnityが再生成します。
