// =============================================================================
//  RpaSftpAskPass.cs - OpenSSH SSH_ASKPASS helper (must be .exe, not .cmd).
//  Prints the password file named in RPA_SFTP_PWFILE with no extra newline.
// =============================================================================
using System;
using System.IO;
using System.Text;

namespace RpaTools
{
    internal static class RpaSftpAskPass
    {
        private static int Main()
        {
            try
            {
                string path = Environment.GetEnvironmentVariable("RPA_SFTP_PWFILE");
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) { return 1; }
                string text = File.ReadAllText(path, Encoding.UTF8);
                if (text == null) { text = ""; }
                text = text.TrimEnd('\r', '\n');
                Console.OutputEncoding = Encoding.UTF8;
                Console.Write(text);
                return 0;
            }
            catch
            {
                return 1;
            }
        }
    }
}
