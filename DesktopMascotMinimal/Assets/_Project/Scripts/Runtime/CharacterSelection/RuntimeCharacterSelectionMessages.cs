using DesktopMascot.Character;
using DesktopMascot.Runtime.CharacterPersistence;

namespace DesktopMascot.Runtime.CharacterSelection
{
    internal static class RuntimeCharacterSelectionMessages
    {
        internal static string ForPicker(VrmFilePickerStatus status)
        {
            switch (status)
            {
                case VrmFilePickerStatus.Cancelled:
                    return "ファイルの読み込みをキャンセルしました";
                case VrmFilePickerStatus.OwnerUnavailable:
                    return "ファイル選択画面を開けませんでした";
                case VrmFilePickerStatus.AlreadyRunning:
                    return "ファイル選択画面は既に開いています";
                default:
                    return "ファイル選択画面を開けませんでした";
            }
        }

        internal static string ForImport(RuntimeVrmImportStatus status)
        {
            switch (status)
            {
                case RuntimeVrmImportStatus.EmptyPath:
                case RuntimeVrmImportStatus.RelativePath:
                case RuntimeVrmImportStatus.UnsupportedExtension:
                    return "VRM 1.0ファイルを選択してください";
                case RuntimeVrmImportStatus.FileNotFound:
                case RuntimeVrmImportStatus.DirectoryPath:
                case RuntimeVrmImportStatus.EmptyFile:
                case RuntimeVrmImportStatus.UnreadableFile:
                case RuntimeVrmImportStatus.HashFailed:
                    return "ファイルを読み込めませんでした";
                case RuntimeVrmImportStatus.FileTooLarge:
                    return "ファイルサイズが大きすぎます";
                case RuntimeVrmImportStatus.ImportFailed:
                case RuntimeVrmImportStatus.MissingRuntimeInstance:
                    return "このVRMには対応していません";
                case RuntimeVrmImportStatus.Cancelled:
                    return "ファイルの読み込みをキャンセルしました";
                case RuntimeVrmImportStatus.ShutdownStarted:
                    return "終了処理中のため変更できません";
                case RuntimeVrmImportStatus.AlreadyStarted:
                    return "モデルの読み込み中です";
                default:
                    return "ファイルを読み込めませんでした";
            }
        }

        internal static string ForPreparation(
            RuntimeCharacterPreparationStatus status)
        {
            return status == RuntimeCharacterPreparationStatus.Success
                ? string.Empty
                : "モデルの準備に失敗しました";
        }

        internal static string ForActivation(
            CharacterActivationStatus status)
        {
            return status == CharacterActivationStatus.ShutdownStarted
                ? "終了処理中のため変更できません"
                : "モデルの切り替えに失敗しました";
        }

        internal static string ForRestorePending() =>
            "保存されたモデルを確認しています…";

        internal static string ForRestoreValidating() =>
            "保存されたモデルを確認しています…";

        internal static string ForRestoreImporting() =>
            "保存されたモデルを読み込んでいます…";

        internal static string ForRestoreActivating() =>
            "保存されたモデルへ切り替えています…";

        internal static string ForRestoreSucceeded() =>
            "保存されたモデルを復元しました";

        internal static string ForShaMismatch() =>
            "保存されたモデルが変更されています";

        internal static string ForRestorePreparationFailure() =>
            "保存されたモデルを準備できませんでした";

        internal static string ForRestoreActivationFailure() =>
            "保存されたモデルへ切り替えられませんでした";

        internal static string ForRestoreFailure(
            RuntimeVrmImportStatus status)
        {
            switch (status)
            {
                case RuntimeVrmImportStatus.FileNotFound:
                case RuntimeVrmImportStatus.DirectoryPath:
                    return "保存されたモデルが見つかりません";
                default:
                    return "保存されたモデルを読み込めませんでした";
            }
        }

        internal static string ForRecordFailure(
            CharacterSelectionPersistenceInitializationStatus status)
        {
            return status
                    == CharacterSelectionPersistenceInitializationStatus
                        .UnsupportedVersion
                ? "保存されたモデル情報の形式に対応していません"
                : "保存されたモデル情報を読み込めませんでした";
        }

        internal static string ForPersistenceUnavailable() =>
            "モデルの次回起動用設定を利用できません";

        internal static string ForPersistenceWriteFailure() =>
            "モデルは変更されましたが、次回起動用に保存できませんでした";

        internal static string ForPersistenceCleared() =>
            "次回起動時は同梱モデルを使用します";

        internal static string ForBundledActivating() =>
            "同梱モデルへ切り替えています…";

        internal static string ForBundledActivated() =>
            "同梱モデルへ戻しました";

        internal static string ForBundledAlreadyActive() =>
            "同梱モデルを使用中です";

        internal static string ForCharacterOperationBusy() =>
            "モデルの変更処理中です";

        internal static string ForBundledActivationFailure(
            CharacterActivationStatus status)
        {
            return status == CharacterActivationStatus.ShutdownStarted
                ? "終了処理中のため変更できません"
                : "同梱モデルへ戻せませんでした";
        }

        internal static string ForBundledPersistenceClearFailure() =>
            "同梱モデルに戻しましたが、次回起動設定を変更できませんでした";
    }
}
