using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine.TextCore;

namespace YoungBob.Editor.Build
{
    public static class YoungBobWebGlBuild
    {
        private const string DefaultRelativeOutputPath = "Builds/WebGL";
        private const string ChineseFontAssetPath = "Assets/Resources/Fonts/NotoSansSC-Regular.ttf";

        public static void PerformCommandLineBuild()
        {
            try
            {
                var outputPath = ResolveOutputPath();
                ConfigureChineseFontForBuild();
                Build(outputPath);
                UnityEngine.Debug.Log("[YoungBobWebGlBuild] Build completed: " + outputPath);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[YoungBobWebGlBuild] Build failed: " + ex);
                EditorApplication.Exit(1);
            }
        }

        private static void ConfigureChineseFontForBuild()
        {
            var importer = AssetImporter.GetAtPath(ChineseFontAssetPath) as TrueTypeFontImporter;
            if (importer == null)
            {
                UnityEngine.Debug.LogWarning("[YoungBobWebGlBuild] Chinese font importer not found: " + ChineseFontAssetPath);
                return;
            }

            var customCharacters = CollectProjectCharacters();
            if (string.IsNullOrEmpty(customCharacters))
            {
                UnityEngine.Debug.LogWarning("[YoungBobWebGlBuild] No custom characters collected for Chinese font.");
                return;
            }

            importer.fontTextureCase = FontTextureCase.CustomSet;
            importer.customCharacters = customCharacters;
            importer.includeFontData = false;
            importer.fontSize = 32;
            importer.characterPadding = 2;
            importer.fontRenderingMode = FontRenderingMode.HintedSmooth;
            importer.SaveAndReimport();

            UnityEngine.Debug.Log("[YoungBobWebGlBuild] Configured Chinese font with " + customCharacters.Length + " custom characters.");
        }

        private static string CollectProjectCharacters()
        {
            var chars = new HashSet<char>();
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cs",
                ".json",
                ".unity",
                ".prefab",
                ".asset",
                ".md"
            };

            var files = Directory.EnumerateFiles(Path.GetFullPath("Assets"), "*", SearchOption.AllDirectories)
                .Where(path => allowedExtensions.Contains(Path.GetExtension(path)));

            foreach (var file in files)
            {
                foreach (var c in File.ReadAllText(file))
                {
                    if (IsUsefulFontCharacter(c))
                    {
                        chars.Add(c);
                    }
                }
            }

            const string asciiAndPunctuation =
                " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                "\n\r\t";
            foreach (var c in asciiAndPunctuation)
            {
                chars.Add(c);
            }

            var ordered = chars.ToList();
            ordered.Sort();
            return new string(ordered.ToArray());
        }

        private static bool IsUsefulFontCharacter(char c)
        {
            if (char.IsWhiteSpace(c))
            {
                return true;
            }

            var category = char.GetUnicodeCategory(c);
            if (category == System.Globalization.UnicodeCategory.Control ||
                category == System.Globalization.UnicodeCategory.OtherNotAssigned)
            {
                return false;
            }

            return c >= 0x20;
        }

        private static void Build(string outputPath)
        {
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (enabledScenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes found in EditorBuildSettings.");
            }

            Directory.CreateDirectory(outputPath);

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = enabledScenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"WebGL build failed with result {report.summary.result} and {report.summary.totalErrors} errors.");
            }
        }

        private static string ResolveOutputPath()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-buildOutput", StringComparison.OrdinalIgnoreCase))
                {
                    return Path.GetFullPath(args[i + 1]);
                }
            }

            return Path.GetFullPath(DefaultRelativeOutputPath);
        }
    }
}
