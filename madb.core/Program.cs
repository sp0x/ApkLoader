using System;
using System.Net;
using SharpAdbClient;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Renci.SshNet;
using Renci.SshNet.Common;


namespace coreadb
{
    class Program
    { 
        private static AdbManager _adbManager;
        private static NetworkManager _netManager;
        private static DomainManager _manager;

        static void Main(string[] args)
        {
            // Program.exe <-g|--greeting|-$ <greeting>> [name <fullname>]
            // [-?|-h|--help] [-u|--uppercase]
            CommandLineApplication cli = new CommandLineApplication(throwOnUnexpectedArg: true);
            CommandArgument names = null; 
            
            CommandOption hostname = cli.Option("--host <host>","The host to connect to.",CommandOptionType.SingleValue);
            CommandOption opReboot = cli.Option("-b", "Reboots all devices", CommandOptionType.NoValue);
            CommandOption opRestart = cli.Option("-r", "Restarts all devices", CommandOptionType.NoValue);
            CommandOption opScreenshot = cli.Option("--scr <devId>", "Grabs a screenshot of the given device", CommandOptionType.SingleValue);
            //            CommandOption uppercase = cli.Option("-u | --uppercase", "Display the greeting in uppercase.",
            //                CommandOptionType.NoValue);
            cli.HelpOption("-? | --help"); 
            cli.OnExecute(() =>
            {
                Startup(hostname.Value());
                if (opReboot.HasValue()) _manager.RebootAll();
                else if (opRestart.HasValue()) _manager.RestartAll();
                else if (opScreenshot.HasValue()) _manager.Screenshot(opScreenshot.Value());
                
                var x = 123;
                return 0;
            }); 
            var res = cli.Execute(args);
            while (true)
            {
                var input = Console.ReadLine() + "\r\n";
                SmDevice fdevice = _manager.ConnectedDevices.Values.First();
                fdevice.Execute(input);
            }
        } 

       

        private static void Startup(string targetHostname = null)
        { 
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            var config = builder.Build();
            _netManager = new NetworkManager(config, targetHostname); 
            _adbManager = new AdbManager();
            _adbManager.ListenForDevices();
            _manager = new DomainManager(config, _adbManager, _netManager);
        }
    }
}
