using System;
using System.IO;
using System.Threading;

namespace RetroBar.Utilities
{
    class LocalAppDataPath
    {
        static readonly Lazy<string> LazyLocalAppDataAppPath = new(() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RetroBar"), LazyThreadSafetyMode.ExecutionAndPublication);

        internal static string GetLocalAppDataAppPath() => LazyLocalAppDataAppPath.Value;
    }
}