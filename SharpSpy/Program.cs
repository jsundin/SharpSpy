using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SharpSpy
{
    internal class Program
    {
        private const string EV_CHANGE = "change";
        private const string EV_DELETE = "delete";
        private const string EV_CREATE = "create";
        private const string EV_RENAME = "rename";

        private bool verbose = false;
        private int ttlInMillis = 0;

        private bool monitorProcesses = true;
        private bool showKills = false;
        private bool showCommandLine = false;
        private int procSleepTimeInMillis = 500;

        private readonly HashSet<string> fsPaths = new HashSet<string>();
        private readonly HashSet<String> fsEvents = new HashSet<String>(new String[] {EV_CHANGE, EV_CREATE, EV_DELETE, EV_RENAME});
        private string fsFilter = null;
        private bool fsRecursive = false;
        private string logFile = null;

        private List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        private Socket logSocket;

        static void Main(string[] args)
        {
            var p = new Program();
            if (p.ParseArgs(new List<string>(args)))
            {
                p.Run();
            }
        }

        void Run() {
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(Cleanup);

            if (verbose)
            {
                Console.WriteLine($"Verbose: {verbose}");

                Console.WriteLine($"Monitor processes: {monitorProcesses}");
                Console.WriteLine($"Show process kills: {showKills}");
                Console.WriteLine($"Show command line: {showCommandLine}");
                Console.WriteLine($"Time to sleep between process checks: {procSleepTimeInMillis}ms");

                if (fsPaths.Count == 0)
                {
                    Console.WriteLine("Paths will not be monitored");
                }
                Console.WriteLine($"Paths to monitor: {string.Join(", ", fsPaths)}");
                Console.WriteLine($"Filesystem events monitored: {string.Join(", ", fsEvents)}");
                Console.WriteLine($"Filesystem filter: {fsFilter}");
                Console.WriteLine($"Filesystem recurse: {fsRecursive}");
            }

            foreach (var item in fsPaths)
            {
                watchers.Add(CreateWatcher(item, fsEvents, fsFilter, fsRecursive));
            }

            if (monitorProcesses)
            {
                DateTime? timeToDie = null;

                if (ttlInMillis > 0)
                {
                    timeToDie = DateTime.Now.AddMilliseconds(ttlInMillis);
                }
                var pids = UpdateProcessMonitor(null, false, showCommandLine);
                while (true)
                {
                    pids = UpdateProcessMonitor(pids, showKills, showCommandLine);
                    System.Threading.Thread.Sleep(procSleepTimeInMillis);
                    if (timeToDie != null && DateTime.Now.CompareTo(timeToDie) > 0)
                    {
                        break;
                    }
                }
            }
            else
            {
                if (ttlInMillis == 0)
                {
                    if (verbose)
                    {
                        Console.WriteLine("Press enter to exit");
                    }
                    Console.ReadLine();
                }
                else
                {
                    System.Threading.Thread.Sleep(ttlInMillis);
                }
            }
        }

        void Cleanup(object sender, EventArgs e)
        {
            if (logSocket != null)
            {
                logSocket.Close();
                logSocket.Dispose();
            }

            foreach (var watcher in watchers)
            {
                watcher.Dispose();
            }
        }

        private HashSet<int> UpdateProcessMonitor(HashSet<int> oldPids, bool showKills, bool showCommandLine) {
            var processList = Process.GetProcesses();
            var newPids = new HashSet<int>();

            foreach (var process in processList)
            {
                if (oldPids == null || !oldPids.Contains(process.Id))
                {
                    var cmdl = "";
                    if (showCommandLine)
                    {
                        cmdl = " [" + GetArgumentsForPid(process.Id) + "]";
                    }

                    Log($"process: {process.Id}/{process.ProcessName}{cmdl}");
                }
                newPids.Add(process.Id);
            }

            if (showKills && oldPids != null)
            {
                foreach (var pid in oldPids)
                {
                    if (!newPids.Contains(pid))
                    {
                        Log($"killed: {pid}");
                    }
                }
            }

            return newPids;
        }

        // https://askguanyu.wordpress.com/2019/03/06/c-tricky-issue-how-to-get-command-line-arguments-of-another-process/
        private string GetArgumentsForPid(int pid)
        {
            try
            {
                using (var mos = new ManagementObjectSearcher("select commandline from win32_process where processid=" + pid))
                using (var objects = mos.Get())
                {
                    var res = objects.Cast<ManagementBaseObject>().SingleOrDefault();
                    return res?["CommandLine"]?.ToString() ?? "";
                }
            }
            catch { }
            return "";
        }

        private FileSystemWatcher CreateWatcher(string path, HashSet<string> events, string filter, bool recursive)
        {
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
            return watcher;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            Log($"fs change: {e.FullPath}");
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            Log($"fs delete: {e.FullPath}");
        }

        private void OnCreated(object sender, FileSystemEventArgs e)
        {
            Log($"fs create: {e.FullPath}");
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            Log($"fs rename: {e.OldFullPath} -> {e.FullPath}");
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            Log($"fs error: {e}");
        }

        private void Log(string message)
        {
            var now = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            var formattedMessage = $"{now} {message}";
            if (logFile != null)
            {
                File.AppendAllText(logFile, $"{formattedMessage}\n");
            }
            if (logSocket != null)
            {
                logSocket.Send(Encoding.ASCII.GetBytes($"{formattedMessage}\n"));
            }

            if (verbose)
            {
                Console.WriteLine(formattedMessage);
            }
        }

        private void Help()
        {
            Console.WriteLine("Usage: SharpSpy [params...]");
            Console.WriteLine("  -v                  Verbose output");
            Console.WriteLine("  -ttl <millis>       How long should the application execute for, in millis (default: forever)");
            Console.WriteLine("  -np                 Disable process monitor (default: on)");
            Console.WriteLine("  -k                  Show the pid of killed processes (default: off)");
            Console.WriteLine("  -cmd                Attempt to show command line of processes (default: off) -- slows us down!");
            Console.WriteLine("  -t <millis>         Time to sleep between process checks (default: 500ms)");
            Console.WriteLine("  -fp <path>          File system path to monitor");
            Console.WriteLine("  -fe <ev1,ev2,..>    Events to monitor (allowed: change,create,rename,delete, default: all of them)");
            Console.WriteLine("  -ff <filter>        File system glob filter (e.g *.txt)");
            Console.WriteLine("  -fr                 Scan filesystem recursively");
            Console.WriteLine("  -log file           Write log to file instead of stdout");
            Console.WriteLine("  -logs conn          Write log to a socket (e.g tcp:10.11.12.13:4444)");
        }

        private bool ParseArgs(List<string> args)
        {
            while (args.Count > 0)
            {
                var arg = args[0];
                args = args.Skip(1).ToList();

                switch (arg)
                {
                    case "-h":
                        Help();
                        return false;

                    case "-ttl":
                        ttlInMillis = int.Parse(args[0]);
                        args = args.Skip(1).ToList();
                        break;

                    case "-v":
                        verbose = true;
                        break;

                    case "-np":
                        monitorProcesses = false;
                        break;

                    case "-k":
                        showKills = true;
                        break;

                    case "-cmd":
                        showCommandLine = true;
                        break;

                    case "-t":
                        procSleepTimeInMillis = int.Parse(args[0]);
                        args = args.Skip(1).ToList();
                        break;

                    case "-fp":
                        fsPaths.Add(args[0]);
                        args = args.Skip(1).ToList();
                        break;

                    case "-fe":
                        var evs = args[0].Split(',');
                        fsEvents.Clear();

                        foreach (var ev in evs)
                        {
                            switch (ev)
                            {
                                case "ch": case EV_CHANGE: fsEvents.Add(EV_CHANGE); break;
                                case "cr": case EV_CREATE: fsEvents.Add(EV_CREATE); break;
                                case "del": case EV_DELETE: fsEvents.Add(EV_DELETE); break;
                                case "ren": case EV_RENAME: fsEvents.Add(EV_RENAME); break;
                                default:
                                    Console.WriteLine($"Unknown filesystem event: {ev}");
                                    return false;

                            }
                        }
                        args = args.Skip(1).ToList();
                        break;

                    case "-ff":
                        fsFilter = args[0];
                        args = args.Skip(1).ToList();
                        break;

                    case "-fr":
                        fsRecursive = true;
                        break;

                    case "-log":
                        logFile = args[0];
                        args = args.Skip(1).ToList();
                        break;

                    case "-logs":
                        var dst = args[0].Split(':');
                        args = args.Skip(1).ToList();

                        IPAddress ipAddress = IPAddress.Parse(dst[1]);
                        IPEndPoint ipEndpoint = new IPEndPoint(ipAddress, int.Parse(dst[2]));

                        switch (dst[0])
                        {
                            case "tcp":
                                logSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                                break;
                            case "udp":
                                logSocket = new Socket(ipAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                                break;
                            default:
                                Console.WriteLine($"Don't know what this is: {dst[0]} (tcp or udp expected)");
                                return false;
                        }

                        logSocket.Connect(ipEndpoint);
                        break;

                    default:
                        Console.WriteLine($"Unknown option: {arg}");
                        return false;
                }
            }
            return true;
        }
    }
}
