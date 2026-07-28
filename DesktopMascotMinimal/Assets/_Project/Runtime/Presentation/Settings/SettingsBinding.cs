namespace DesktopMascot.Runtime.Settings.UI
{
    internal sealed class SettingsBinding
    {
        private readonly SettingsViewModel viewModel;

        internal SettingsBinding(SettingsViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        internal bool FirstRunCompleted
        {
            get => viewModel.FirstRunCompleted;
            set => viewModel.FirstRunCompleted = value;
        }

        internal bool IsDirty => viewModel.IsDirty;
        internal SettingsUiValidationResult Validation =>
            SettingsValidation.Validate(viewModel.Values);
        internal bool ApplyEnabled =>
            IsDirty && Validation.IsValid;
    }
}
