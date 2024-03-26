using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SharpSpy
{
    internal partial class Program : Form
    {
        static void Main(string[] args)
        {
            Config config = Config.Parse(new List<string>(args));
            if (config == null)
            {
                return;
            }

            Logger logger = new Logger(config.Verbose, config.LogFile, config.LogSocket);
            new Program(logger, config).Run();
        }

        private Logger logger;
        private Config config;
        private List<FilesystemMonitor> monitors = new List<FilesystemMonitor>();
        private ProcessMonitor processMonitor;

        public Program(Logger logger, Config config)
        {
            this.logger = logger;
            this.config = config;

            //Turn the child window into a message-only window (refer to Microsoft docs)
            NativeMethods.SetParent(Handle, NativeMethods.HWND_MESSAGE);

            if (config.EnableClipboard)
            {
                //Place window in the system-maintained clipboard format listener list
                NativeMethods.AddClipboardFormatListener(Handle);
            }
        }

        private void ShutdownTimerTick(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void poll(object sender, EventArgs e)
        {
            processMonitor.Update();
        }

        void Run()
        {
            if (config.Verbose)
            {
                Console.WriteLine($"Verbose: {config.Verbose}");
                Console.WriteLine($"Logfile: {config.LogFile}");
                Console.WriteLine($"Log socket: {config.LogSocket}");
                Console.WriteLine($"Time to live (ms): {config.TtlInMillis}");

                Console.WriteLine($"Process monitor: {config.EnableProcessMonitor}");
                Console.WriteLine($"Show kills: {config.ShowKills}");
                Console.WriteLine($"Show command line: {config.ShowCommandLine}");
                Console.WriteLine($"Process monitor sleep (ms): {config.ProcessMonitorSleepInMillis}");

                if (config.FsMonitorPaths.Count == 0)
                {
                    Console.WriteLine("No paths will be monitored");
                }
                else
                {
                    Console.WriteLine($"Paths to monitor: {string.Join(", ", config.FsMonitorPaths)}");
                }
                Console.WriteLine($"Filesystem events to monitor: {string.Join(", ", config.FsEvents)}");
                Console.WriteLine($"Filesystem filter: {config.FsFilter}");
                Console.WriteLine($"Filesystem recurse: {config.FsRecursive}");
            }

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(Cleanup);

            foreach (var path in config.FsMonitorPaths)
            {
                monitors.Add(new FilesystemMonitor(logger, config.Verbose, path, config.FsEvents, config.FsFilter, config.FsRecursive));
            }

            if (config.TtlInMillis > 0)
            {
                var shutdownTimer = new Timer();
                shutdownTimer.Interval = config.TtlInMillis;
                shutdownTimer.Tick += new EventHandler(ShutdownTimerTick);
                shutdownTimer.Start();
            }

            if (config.EnableProcessMonitor)
            {
                processMonitor = new ProcessMonitor(logger, config.ShowCommandLine, config.ShowKills);
                processMonitor.Update(); // we want to do this once first, to get all the processes.. the timer should only catch the updates

                var processMonitorTimer = new Timer();
                processMonitorTimer.Interval = config.ProcessMonitorSleepInMillis;
                processMonitorTimer.Tick += new EventHandler(poll);
                processMonitorTimer.Start();
            }

            Application.Run(this);
        }

        private void Cleanup(object sender, EventArgs e)
        {
            logger.Dispose();
            foreach (var monitor in monitors)
            {
                monitor.Dispose();
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                ClipboardUtil.LogClipboard(logger);
            }

            base.WndProc(ref m);
        }
    }
}