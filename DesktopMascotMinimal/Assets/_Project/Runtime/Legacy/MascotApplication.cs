using UnityEngine;

namespace DesktopMascot
{
    public sealed class MascotApplication : MonoBehaviour
    {
        [SerializeField] private Win32Window window;
        [SerializeField] private Transform mascotRoot;
        private MascotSettings _settings;

        private void Awake()
        {
            Application.runInBackground = true;
            _settings = SettingsStore.Load();
            if (mascotRoot != null) mascotRoot.localScale = Vector3.one * _settings.modelScale;
        }

        private void Start()
        {
            if (window == null)
            {
                Debug.LogError("Win32Window is not assigned.");
                return;
            }

            window.Initialize(_settings.alwaysOnTop);
            window.SetPosition(_settings.windowX, _settings.windowY);
        }

        private void OnApplicationQuit()
        {
            if (window == null || _settings == null) return;

            var position = window.GetPosition();
            _settings.windowX = position.x;
            _settings.windowY = position.y;
            SettingsStore.Save(_settings);
        }
    }
}
