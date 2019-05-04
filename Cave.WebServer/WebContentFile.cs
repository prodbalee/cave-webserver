using System;
using System.Diagnostics;
using System.IO;

namespace Cave.Web
{
    class WebContentFile
    {
        internal WebContentFile(WebServer server, string mainFolder, string url)
        {
            string fileName;
            if (Path.IsPathRooted(url))
            {
                fileName = Path.Combine(server.StaticFilesPath, "./" + url);
            }
            else
            {
                fileName = Path.Combine(mainFolder, url);
            }
            if (!FileSystem.IsRelative(fileName, server.StaticFilesPath))
            {
                throw new WebServerException(WebError.InternalServerError, 0, $"Content file {fileName} is not relative to static folder path!");
            }
            Trace.TraceInformation("Reloading content <cyan>{0}", url);
            Url = url;
            FileName = fileName;
            LastChanged = FileSystem.GetLastWriteTimeUtc(fileName);
            Content = File.ReadAllBytes(fileName);
        }

        internal string FileName { get; }
        internal string Url { get; }
        internal DateTime LastChanged { get; }
        internal byte[] Content { get; }

        public string LogSourceName => "CaveWebContentFile";
    }
}
