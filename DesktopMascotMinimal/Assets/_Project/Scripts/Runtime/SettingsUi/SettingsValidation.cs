namespace DesktopMascot.Runtime.Settings.UI
{
    internal readonly struct SettingsUiValidationResult
    {
        internal SettingsUiValidationResult(
            bool isValid,
            string message)
        {
            IsValid = isValid;
            Message = message;
        }

        internal bool IsValid { get; }
        internal string Message { get; }
    }

    internal static class SettingsValidation
    {
        internal static SettingsUiValidationResult Validate(
            SettingsValues values)
        {
            if (values.SchemaVersion
                != SettingsDefaults.CurrentSchemaVersion)
            {
                return Invalid("Unsupported settings schema.");
            }
            if (values.SettingsVersion
                != SettingsDefaults.CurrentSettingsVersion)
            {
                return Invalid("Unsupported settings version.");
            }

            // firstRunCompleted is Boolean by construction. Keeping this
            // validation boundary explicit allows future fields to add their
            // own rules without moving validation into controls.
            return new SettingsUiValidationResult(true, string.Empty);
        }

        private static SettingsUiValidationResult Invalid(string message) =>
            new SettingsUiValidationResult(false, message);
    }
}
