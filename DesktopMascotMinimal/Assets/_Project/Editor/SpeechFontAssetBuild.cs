using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace DesktopMascot.Editor
{
    internal static class SpeechFontAssetBuild
    {
        private const string SourcePath =
            "Assets/_Project/Runtime/Presentation/Speech/Fonts/NotoSansCJKjp-Regular.otf";
        private const string OutputDirectory =
            "Assets/_Project/Runtime/Presentation/Speech/Resources/DesktopMascotSpeech";
        private const string OutputPath =
            OutputDirectory + "/NotoSansCJKjp-Regular SDF.asset";
        private const string SettingsPath =
            "Assets/_Project/Runtime/Presentation/Speech/Resources/TMP Settings.asset";

        internal static void Generate()
        {
            Directory.CreateDirectory(OutputDirectory);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>(
                    "Assets/TextMesh Pro/Resources/TMP Settings.asset") != null
                && AssetDatabase.LoadAssetAtPath<TMP_Settings>(SettingsPath) != null)
            {
                AssetDatabase.DeleteAsset(SettingsPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            if (TMP_Settings.instance == null)
            {
                var settings = ScriptableObject.CreateInstance<TMP_Settings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            if (TMP_Settings.instance == null)
                throw new System.InvalidOperationException(
                    "TMP Settings asset could not be initialized.");
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (font == null)
                throw new FileNotFoundException("Speech source font was not imported.", SourcePath);
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath) != null)
                AssetDatabase.DeleteAsset(OutputPath);
            var asset = TMP_FontAsset.CreateFontAsset(
                font,
                48,
                6,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            if (asset == null)
                throw new System.InvalidOperationException("TMP font asset generation failed.");
            asset.name = "NotoSansCJKjp-Regular SDF";
            AssetDatabase.CreateAsset(asset, OutputPath);
            if (asset.material != null && !AssetDatabase.Contains(asset.material))
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            foreach (var texture in asset.atlasTextures)
            {
                if (texture != null && !AssetDatabase.Contains(texture))
                    AssetDatabase.AddObjectToAsset(texture, asset);
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[DesktopMascotSpeechFont] TMP font asset generated: " + OutputPath);
        }
    }
}
