using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpSpy
{
    internal class FilesystemMonitor : IDisposable
    {
        private const string LOG_SOURCE = "fs";

        public const string EV_CHANGE = "change";
        public const string EV_DELETE = "delete";
        public const string EV_CREATE = "create";
        public const string EV_RENAME = "rename";

        private Logger logger;
        private FileSystemWatcher watcher;

        public FilesystemMonitor(Logger logger, bool verbose, string path, IReadOnlyCollection<string> events, string filter, bool recursive)
        {
            this.logger = logger;
            var watcher = new FileSystemWatcher(path);
            watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName;

            if (events.Contains(EV_RENAME))
            {
                watcher.Changed += OnChanged;
            }
            if (events.Contains(EV_DELETE))
            {
                watcher.Deleted += OnDeleted;
            }
            if (events.Contains(EV_CREATE))
            {
                watcher.Created += OnCreated;
            }
            if (events.Contains(EV_RENAME))
            {
                watcher.Renamed += OnRenamed;
            }
            if (verbose)
            {
                watcher.Error += OnError;
            }
            if (filter != null && filter != "")
            {
                watcher.Filter = filter;
            }
            if (recursive)
            {
                watcher.IncludeSubdirectories = true;
            }
            watcher.EnableRaisingEvents = true;
            if (verbose)
            {
                Console.WriteLine($"Watching directory: {path}");
            }
            this.watcher = watcher;
        }

        public void Dispose()
        {
            if (watcher != null)
            {
                watcher.Dispose();
                watcher = null;
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            logger.Log(LOG_SOURCE, new
            {
                ev = EV_CHANGE,
                path = e.FullPath
            });
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            logger.Log(LOG_SOURCE, new
            {
                ev = EV_DELETE,
                path = e.FullPath
            });
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            logger.Log(LOG_SOURCE, new
            {
                ev = EV_CREATE,
                path = e.FullPath
            });
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            logger.Log(LOG_SOURCE, new
            {
                ev = EV_RENAME,
                from = e.OldFullPath,
                to = e.FullPath
            });
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            logger.Log(LOG_SOURCE, new
            {
                ev = "error",
                error = e.ToString()
            });
        }
    }
}
