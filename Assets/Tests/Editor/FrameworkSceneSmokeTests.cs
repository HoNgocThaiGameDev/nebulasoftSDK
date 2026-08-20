using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NebulaSoft.Tests
{
    public sealed class FrameworkSceneSmokeTests
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Project Files/Game/Scenes/Init.unity",
            "Assets/Project Files/Game/Scenes/Menu.unity",
            "Assets/Project Files/Game/Scenes/Game.unity"
        };

        [Test]
        public void BuildSettings_UsesTheFrameworkSceneFlow()
        {
            Assert.That(EditorBuildSettings.scenes, Has.Length.EqualTo(ScenePaths.Length));

            for (int i = 0; i < ScenePaths.Length; i++)
            {
                Assert.That(EditorBuildSettings.scenes[i].enabled, Is.True);
                Assert.That(EditorBuildSettings.scenes[i].path, Is.EqualTo(ScenePaths[i]));
            }
        }

        [Test]
        public void FrameworkScenes_HaveNoMissingScripts()
        {
            List<string> missingScripts = new List<string>();

            foreach (string scenePath in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                    CollectMissingScripts(root.transform, scenePath, missingScripts);
            }

            Assert.That(missingScripts, Is.Empty, string.Join("\n", missingScripts));
        }

        [Test]
        public void PopupContract_RemainsAvailable()
        {
            Assert.That(HasPublicMethod(typeof(UIController), "ShowPage"), Is.True);
            Assert.That(HasPublicMethod(typeof(UIController), "HidePage"), Is.True);
            Assert.That(HasPublicMethod(typeof(UIController), "WaitForPopupsClose"), Is.True);
            Assert.That(typeof(IPopupWindow).IsInterface, Is.True);
            Assert.That(typeof(IPausePopup).IsInterface, Is.True);
        }

        [Test]
        public void LocalLeaderboard_ProvidesOfflineSampleData()
        {
            Assert.That(LocalLeaderboardService.PreloadLeaderboardsAsync().Result, Is.True);

            Assert.That(LocalLeaderboardService.TryGetCachedGlobalPlayers(out List<LeaderboardEntry> global), Is.True);
            Assert.That(global, Is.Not.Null.And.Not.Empty);
            Assert.That(global.Exists(entry => entry.IsCurrentPlayer), Is.True);

            Assert.That(LocalLeaderboardService.TryGetCachedLeaguePlayers(out List<LeaderboardEntry> league), Is.True);
            Assert.That(league, Is.Not.Null.And.Not.Empty);
            Assert.That(league[0].SeasonId, Is.EqualTo(LocalLeaderboardService.CurrentSeasonId));
        }

        private static void CollectMissingScripts(
            Transform current,
            string scenePath,
            ICollection<string> missingScripts)
        {
            foreach (Component component in current.GetComponents<Component>())
            {
                if (component == null)
                    missingScripts.Add(scenePath + ": " + GetHierarchyPath(current));
            }

            for (int i = 0; i < current.childCount; i++)
                CollectMissingScripts(current.GetChild(i), scenePath, missingScripts);
        }

        private static string GetHierarchyPath(Transform current)
        {
            string path = current.name;
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "/" + path;
            }

            return path;
        }

        private static bool HasPublicMethod(System.Type type, string name)
        {
            foreach (var method in type.GetMethods())
            {
                if (method.Name == name)
                    return true;
            }

            return false;
        }
    }
}
