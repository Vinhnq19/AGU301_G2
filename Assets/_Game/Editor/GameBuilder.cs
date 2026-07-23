using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DungeonBuilder.Editor
{
    /// <summary>
    /// Tính năng Build Game (Windows x64) kèm icon ứng dụng.
    /// - Menu "Dungeon Builder/Build/Build Windows (x64)": build ra Builds/Windows.
    /// - Menu "Set Game Icon From Selected Texture": đặt icon app = Texture2D đang chọn.
    /// Icon mặc định lấy từ <see cref="IconPath"/> (thay art riêng của bạn vào đó là được).
    /// </summary>
    public static class GameBuilder
    {
        private const string IconPath = "Assets/Sprite/Icon/icon_dungeon_builder.png";
        private const string OutputDir = "Builds/Windows";

        [MenuItem("Dungeon Builder/Build/Build Windows (x64)")]
        public static void BuildWindows()
        {
            ApplyIcon(null);

            string[] scenes = GetEnabledExistingScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[GameBuilder] Không có scene hợp lệ nào (enabled + tồn tại) trong Build Settings.");
                return;
            }

            Directory.CreateDirectory(OutputDir);
            string exeName = string.IsNullOrEmpty(PlayerSettings.productName) ? "Game" : PlayerSettings.productName;
            string outPath = Path.Combine(OutputDir, exeName + ".exe");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            Debug.Log($"[GameBuilder] Bắt đầu build {scenes.Length} scene -> {outPath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[GameBuilder] Build THÀNH CÔNG: {outPath} " +
                          $"({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0}s).");
                EditorUtility.RevealInFinder(outPath);
            }
            else
            {
                Debug.LogError($"[GameBuilder] Build THẤT BẠI: {summary.result}, {summary.totalErrors} lỗi.");
            }
        }

        [MenuItem("Dungeon Builder/Build/Set Game Icon From Selected Texture")]
        public static void SetIconFromSelection()
        {
            var tex = Selection.activeObject as Texture2D;
            if (tex == null)
            {
                EditorUtility.DisplayDialog("Set Game Icon",
                    "Hãy chọn 1 Texture2D trong cửa sổ Project trước, rồi chạy lại menu này.", "OK");
                return;
            }

            ApplyIcon(tex);
            Debug.Log($"[GameBuilder] Đã đặt icon ứng dụng (Standalone) = '{tex.name}'.");
        }

        [MenuItem("Dungeon Builder/Build/Clean Missing Scenes From Build Settings")]
        public static void CleanMissingScenes()
        {
            var kept = new List<EditorBuildSettingsScene>();
            int removed = 0;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (File.Exists(s.path)) kept.Add(s);
                else removed++;
            }

            EditorBuildSettings.scenes = kept.ToArray();
            Debug.Log($"[GameBuilder] Đã gỡ {removed} scene chết khỏi Build Settings.");
        }

        /// <summary>Gán icon app cho Standalone. overrideTex != null thì dùng nó, ngược lại lấy từ IconPath.</summary>
        private static void ApplyIcon(Texture2D overrideTex)
        {
            Texture2D icon = overrideTex != null
                ? overrideTex
                : AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);

            if (icon == null)
            {
                Debug.LogWarning($"[GameBuilder] Không tìm thấy icon tại '{IconPath}' — build sẽ dùng icon Unity mặc định.");
                return;
            }

            var nbt = NamedBuildTarget.Standalone;
            int[] sizes = PlayerSettings.GetIconSizes(nbt, IconKind.Application);
            var arr = new Texture2D[sizes.Length > 0 ? sizes.Length : 1];
            for (int i = 0; i < arr.Length; i++) arr[i] = icon;
            PlayerSettings.SetIcons(nbt, arr, IconKind.Application);
        }

        private static string[] GetEnabledExistingScenes()
        {
            var list = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.enabled && File.Exists(s.path)) list.Add(s.path);
            }
            return list.ToArray();
        }
    }
}
