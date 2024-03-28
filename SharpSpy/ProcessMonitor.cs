using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;

namespace SharpSpy
{
    internal class ProcessMonitor
    {
        private const string LOG_SOURCE = "process";

        private Logger logger;
        private HashSet<int> lastPids;
        private bool showCommandLine;
        private bool showKills;

        public ProcessMonitor(Logger logger, bool showCommandLine, bool showKills)
        { 
            this.logger = logger;
            this.showCommandLine = showCommandLine;
            this.showKills = showKills;
        }

        public void Update()
        {
            var processList = Process.GetProcesses();
            var newPids = new HashSet<int>();

            foreach (var process in processList)
            {
                if (lastPids == null || !lastPids.Contains(process.Id))
                {
                    string cmdLine = "";
                    if (showCommandLine)
                    {
                        cmdLine = $": {GetArgumentsForPid(process.Id)}";
                    }
                    logger.Log(LOG_SOURCE, $"created: {process.Id}/{process.ProcessName}{cmdLine}");
                }
                newPids.Add(process.Id);
            }

            if (showKills && lastPids != null)
            {
                foreach (var pid in lastPids)
                {
                    if (!newPids.Contains(pid))
                    {
                        logger.Log(LOG_SOURCE, $"killed: {pid}");
                    }
                }
            }

            lastPids = newPids;
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
    }
}
