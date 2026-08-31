using AutoScheduler.Core.IO;
using AutoScheduler.Core.Store;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AutoScheduler.Core.Services
{
    public sealed class UserSessionState
    {
        public string LastProjectPath { get; set; }
        public string LastFolderPath { get; set; }
        public List<string> RecentProjectPaths { get; set; } = new List<string>();
        public bool SkipWelcomeTutorial { get; set; }
        public bool HasShownWelcomeTutorial { get; set; }
    }

    public static class UserSessionService
    {
        private const int MaxRecentProjects = 8;

        private static string SessionDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sapsoft", "AutoScheduler");

        private static string SessionFilePath => Path.Combine(SessionDirectory, "user-session.json");
        private static string AutoSaveFilePath => Path.Combine(SessionDirectory, "autosave.autosched.json");

        public static UserSessionState Load()
        {
            try
            {
                if (!File.Exists(SessionFilePath))
                    return new UserSessionState();

                var json = File.ReadAllText(SessionFilePath);
                var state = JsonConvert.DeserializeObject<UserSessionState>(json);
                return state ?? new UserSessionState();
            }
            catch
            {
                return new UserSessionState();
            }
        }

        public static void Save(UserSessionState state)
        {
            try
            {
                Directory.CreateDirectory(SessionDirectory);
                var json = JsonConvert.SerializeObject(state ?? new UserSessionState(), Formatting.Indented);
                File.WriteAllText(SessionFilePath, json);
            }
            catch
            {
            }
        }

        public static void RememberProject(UserSessionState state, string filePath)
        {
            if (state == null || string.IsNullOrWhiteSpace(filePath))
                return;

            state.LastProjectPath = filePath;
            state.LastFolderPath = Path.GetDirectoryName(filePath);

            var items = state.RecentProjectPaths ?? new List<string>();
            items.RemoveAll(x => string.Equals(x, filePath, StringComparison.OrdinalIgnoreCase));
            items.Insert(0, filePath);
            state.RecentProjectPaths = items.Where(File.Exists).Take(MaxRecentProjects).ToList();
        }

        public static void RememberFolder(UserSessionState state, string folderPath)
        {
            if (state == null || string.IsNullOrWhiteSpace(folderPath))
                return;

            state.LastFolderPath = folderPath;
        }

        public static void SaveAutoBackup(ProjectStore store)
        {
            try
            {
                Directory.CreateDirectory(SessionDirectory);
                ProjectSerializer.Save(store, AutoSaveFilePath);
            }
            catch
            {
            }
        }

        public static bool TryRestoreAutoBackup(ProjectStore store)
        {
            try
            {
                if (!File.Exists(AutoSaveFilePath))
                    return false;

                ProjectSerializer.Load(store, AutoSaveFilePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string GetAutoBackupPath()
        {
            return AutoSaveFilePath;
        }
    }
}
