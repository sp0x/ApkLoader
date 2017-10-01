using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace coreadb
{
    public static class Extensions
    {
        public static string ExecuteCommand(this string cmd, string winArguments)
        {
            var escapedArgs = cmd.Replace("\"", "\\\"");
            var binary = "";
            Process process = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                binary = "/bin/bash";
                process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = binary,
                        Arguments = $"-c \"{escapedArgs}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                process = new Process()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = cmd,
                        Arguments = winArguments,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
            }
            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return result;
        }
    }
}
