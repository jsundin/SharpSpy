using System;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace SharpSpy
{
    internal class Logger : IDisposable
    {
        private string logFile = null;
        private Socket logSocket;
        private bool verbose;

        public Logger(bool verbose, string logFile, string logUrl)
        {
            this.verbose = verbose;
            this.logFile = logFile;

            if (logUrl != null)
            {
                var splut = logUrl.Split(new string[] { "://", ":" }, StringSplitOptions.None);
                if (splut.Length != 3)
                {
                    throw new ArgumentException("Logging URL must be in the form protocol://host:port");
                }
                var proto = splut[0];
                var host = splut[1];
                var port = int.Parse(splut[2]);

                IPAddress ipAddress = IPAddress.Parse(host);
                IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, port);

                switch (proto)
                {
                    case "tcp":
                        logSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        break;
                    case "udp":
                        logSocket = new Socket(ipAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                        break;
                    default:
                        throw new ArgumentException($"Dont know the protocol {proto}, try udp or tcp");
                }
                logSocket.Connect(ipEndPoint);

            }
        }

        public void Dispose()
        {
            if (logSocket != null)
            {
                logSocket.Close();
                logSocket.Dispose();
                logSocket = null;
            }
        }
        
        private void Write(string data)
        {
            if (logFile != null)
            {
                File.AppendAllText(logFile, data);
            }

            if (logSocket != null)
            {
                logSocket.Send(Encoding.ASCII.GetBytes($"{data}\n"));
            }

            if (verbose)
            {
                Console.WriteLine(data);
            }
        }

        public void Log(string source, string payload)
        {
            var timestamp = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            var message = $"{source} {timestamp} -- {payload}";
            Write(message);
        }
    }
}
