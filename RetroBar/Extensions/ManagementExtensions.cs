using System.Collections.Generic;
using System.IO;
using RetroBar.Utilities;

namespace RetroBar.Extensions
{
    internal static class ManagementExtensions
    {
        public static string InLocalAppData(this string path1, string path2 = "")
        {
            return Path.Combine(LocalAppDataPath.GetLocalAppDataAppPath(), path1, path2);
        }

        public static void AddFrom(this List<string> list, string path, string extension, bool skipExisting = false)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (string file in Directory.GetFiles(path, $"*.{extension}"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (skipExisting && list.Contains(fileName))
                {
                    continue;
                }
                list.Add(fileName);
            }
        }
    }
}