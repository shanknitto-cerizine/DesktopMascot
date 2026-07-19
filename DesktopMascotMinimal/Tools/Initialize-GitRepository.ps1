param(
    [string]$CommitMessage = "Initial Unity 6.5 desktop mascot project"
)

$ErrorActionPreference = "Stop"
Set-Location (Split-Path -Parent $PSScriptRoot)

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "Gitが見つかりません。Git for Windowsをインストールしてください。"
}

if (-not (Test-Path ".git")) {
    git init
}

git branch -M main
git add .
git status
git commit -m $CommitMessage

Write-Host ""
Write-Host "初回コミットが完了しました。"
Write-Host "次にGitHubで空のPrivateリポジトリを作り、以下を実行してください。"
Write-Host "git remote add origin https://github.com/YOUR_NAME/YOUR_REPOSITORY.git"
Write-Host "git push -u origin main"
