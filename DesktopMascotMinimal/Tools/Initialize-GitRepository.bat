@echo off
setlocal
cd /d "%~dp0\.."
where git >nul 2>nul
if errorlevel 1 (
  echo Gitが見つかりません。Git for Windowsをインストールしてください。
  pause
  exit /b 1
)
if not exist .git git init
git branch -M main
git add .
git status
git commit -m "Initial Unity 6.5 desktop mascot project"
echo.
echo 初回コミットが完了しました。
echo GitHubで空のPrivateリポジトリを作成後、次を実行してください。
echo git remote add origin https://github.com/YOUR_NAME/YOUR_REPOSITORY.git
echo git push -u origin main
pause
