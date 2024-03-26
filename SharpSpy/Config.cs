using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpSpy
{
    internal partial class Program
    {
        internal class Config
        {
            public bool Verbose { get; private set; } = false;
            public int TtlInMillis { get; private set; } = 0;

            public bool EnableProcessMonitor { get; private set; } = true;
            public bool ShowKills { get; private set; } = false;
            public bool ShowCommandLine { get; private set; } = false;
            public int ProcessMonitorSleepInMillis { get; private set; } = 500;

            public IReadOnlyList<string> FsMonitorPaths { get; private set; }
            public IReadOnlyCollection<string> FsEvents { get; private set; }
            public string FsFilter { get; private set; } = null;
            public bool FsRecursive { get; private set; } = false;

            public bool EnableClipboard { get; private set; } = false;

            public string LogFile { get; private set; } = null;
            public string LogSocket { get; private set; } = null;

            private Config()
            {
            }

            public static Config Parse(List<string> args)
            {
                var config = new Config();
                
                var fsMonitorPaths = new List<string>();
                var fsEvents = new HashSet<string>(new String[] {FilesystemMonitor.EV_CREATE, FilesystemMonitor.EV_DELETE, FilesystemMonitor.EV_RENAME, FilesystemMonitor.EV_CHANGE});
                config.FsMonitorPaths = fsMonitorPaths.AsReadOnly();
                config.FsEvents = fsEvents;

                while (args.Count > 0)
                {
                    var arg = args[0];
                    args = args.Skip(1).ToList();

                    switch (arg)
                    {
                        case "-h":
                            Help();
                            return null;

                        case "-ttl":
                            config.TtlInMillis = int.Parse(args[0]);
                            args = args.Skip(1).ToList();
                            break;

                        case "-v":
                            config.Verbose = true;
                            break;

                        case "-np":
                            config.EnableProcessMonitor = false;
                            break;

                        case "-k":
                            config.ShowKills = true;
                            break;

                        case "-cmd":
                            config.ShowCommandLine = true;
                            break;

                        case "-pt":
                            config.ProcessMonitorSleepInMillis = int.Parse(args[0]);
                            args = args.Skip(1).ToList();
                            break;

                        case "-fp":
                            fsMonitorPaths.Add(args[0]);
                            args = args.Skip(1).ToList();
                            break;

                        case "-fe":
                            var evs = args[0].Split(',');
                            args = args.Skip(1).ToList();
                            fsEvents.Clear();

                            foreach(var ev in evs)
                            {
                                switch (ev)
                                {
                                    case "ch": case FilesystemMonitor.EV_CHANGE: fsEvents.Add(FilesystemMonitor.EV_CHANGE); break;
                                    case "cr": case FilesystemMonitor.EV_CREATE: fsEvents.Add(FilesystemMonitor.EV_CREATE); break;
                                    case "del": case FilesystemMonitor.EV_DELETE: fsEvents.Add(FilesystemMonitor.EV_DELETE); break;
                                    case "ren": case FilesystemMonitor.EV_RENAME: fsEvents.Add(FilesystemMonitor.EV_RENAME); break;
                                    default:
                                        Console.WriteLine($"Unknown filesystem event: {ev}");
                                        return null;
                                }
                            }
                            break;

                        case "-ff":
                            config.FsFilter = args[0];
                            args = args.Skip(1).ToList();
                            break;

                        case "-fr":
                            config.FsRecursive = true;
                            break;

                        case "-c":
                            config.EnableClipboard = true;
                            break;

                        case "-log":
                            config.LogFile = args[0];
                            args = args.Skip(1).ToList();
                            break;

                        case "-logs":
                            config.LogSocket = args[0];
                            args = args.Skip(1).ToList();
                            break;

                        default:
                            Console.WriteLine($"Unknown option: {arg}");
                            return null;
                    }
                }

                return config;
            }

            private static void Help()
            {
                Console.WriteLine("Usage: SharpSpy [params...]");
                Console.WriteLine("  -v                  Verbose output");
                Console.WriteLine("  -ttl <millis>       How long should the application execute for, in millis (default: forever)");
                Console.WriteLine("  -np                 Disable process monitor (default: on)");
                Console.WriteLine("  -k                  Show the pid of killed processes (default: off)");
                Console.WriteLine("  -cmd                Attempt to show command line of processes (default: off) -- slows us down!");
                Console.WriteLine("  -pt <millis>        Time to sleep between process checks (default: 500ms)");
                Console.WriteLine("  -fp <path>          File system path to monitor");
                Console.WriteLine("  -fe <ev1,ev2,..>    Events to monitor (allowed: change,create,rename,delete, default: all of them)");
                Console.WriteLine("  -ff <filter>        File system glob filter (e.g *.txt)");
                Console.WriteLine("  -fr                 Scan filesystem recursively");
                Console.WriteLine("  -c                  Listen for clipboard events");
                Console.WriteLine("  -log file           Write log to file instead of stdout");
                Console.WriteLine("  -logs conn          Write log to a socket (e.g tcp:10.11.12.13:4444)");
            }
        }
    }
}
