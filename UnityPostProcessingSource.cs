using System;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace Landoria.ModelForge
{
    // Retrieves Unity's matching v1 post-processing sources when exporting a project.
    internal static class UnityPostProcessingSource
    {
        private const string Revision = "933df236f509ed64ae5763ed57af33f2342cd1c2";
        private const string ArchiveUrl =
            "https://codeload.github.com/Unity-Technologies/PostProcessing/zip/" + Revision;
        private const string ShaderPath = "/PostProcessing/Resources/Shaders/";

        internal static void CopyToProject(string project)
        {
            var folder = Path.Combine(project, "Assets", "Resources", "Shaders");
            if (File.Exists(Path.Combine(folder, "Uber.shader")) &&
                File.Exists(Path.Combine(folder, "Bloom.shader"))) { return; }
            var cache = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(project)),
                "UnityPostProcessing-v1-" + Revision + ".zip");
            if (!File.Exists(cache))
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (var client = new WebClient())
                { client.DownloadFile(ArchiveUrl, cache); }
            }
            Directory.CreateDirectory(folder);
            using (var archive = ZipFile.OpenRead(cache))
            {
                var count = 0;
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("/LICENSE", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.ExtractToFile(Path.Combine(folder, "UnityPostProcessing.LICENSE"), true);
                    }
                    var marker = entry.FullName.IndexOf(ShaderPath,
                        StringComparison.OrdinalIgnoreCase);
                    if (marker < 0 || entry.Name.Length == 0 ||
                        entry.Name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    { continue; }
                    var target = Path.Combine(folder, entry.Name);
                    entry.ExtractToFile(target, true);
                    count++;
                }
                if (count < 29) { throw new InvalidDataException(
                    "Unity post-processing archive has missing shader sources."); }
            }
        }
    }
}
