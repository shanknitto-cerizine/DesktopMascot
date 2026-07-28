namespace DesktopMascot.Runtime.Settings.UI
{
    internal sealed class SettingsViewModel
    {
        private SettingsValues baseline;
        private SettingsValues editable;
        private bool initialized;

        internal SettingsValues Values => editable;
        internal bool IsInitialized => initialized;
        internal bool IsDirty =>
            initialized && !editable.Equals(baseline);

        internal bool FirstRunCompleted
        {
            get => editable.FirstRunCompleted;
            set
            {
                EnsureInitialized();
                editable = new SettingsValues(
                    editable.SchemaVersion,
                    editable.SettingsVersion,
                    value);
            }
        }

        internal void Initialize(SettingsValues values)
        {
            baseline = values;
            editable = values;
            initialized = true;
        }

        internal void CancelChanges()
        {
            EnsureInitialized();
            editable = baseline;
        }

        internal void RestoreDefaults()
        {
            EnsureInitialized();
            editable = SettingsDefaults.Create();
        }

        internal void MarkApplied(SettingsValues values)
        {
            baseline = values;
            editable = values;
            initialized = true;
        }

        private void EnsureInitialized()
        {
            if (!initialized)
                Initialize(SettingsDefaults.Create());
        }
    }
}
