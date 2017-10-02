using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace coreadb
{
    public static class Extensions
    {
        public static string ExecuteCommand(this string cmd, string arguments)
        {
            var escapedArgs = arguments.Replace("\"", "\\\"");
            var binary = "";
            Process process = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                binary = "/bin/bash";
                escapedArgs = cmd + escapedArgs;
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
                        Arguments = arguments,
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
