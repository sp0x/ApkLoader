using System;
using System.Net;
using SharpAdbClient;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

        public static void Main(string[] args)
        {
            // Program.exe <-g|--greeting|-$ <greeting>> [name <fullname>]
            // [-?|-h|--help] [-u|--uppercase]
            CommandLineApplication cli = new CommandLineApplication(throwOnUnexpectedArg: true);
            CommandArgument names = null; 
            
            CommandOption hostname = cli.Option("--host <host>","The host to connect to.",CommandOptionType.SingleValue);
            CommandOption opReboot = cli.Option("-b", "Reboots all devices", CommandOptionType.NoValue);
            CommandOption opRestart = cli.Option("-r", "Restarts all devices", CommandOptionType.NoValue);
            CommandOption opNoForward = cli.Option("--no-forward", "Disables forwarding", CommandOptionType.NoValue);
            CommandOption opScreenshot = cli.Option("--scr <devIp>", "Grabs a screenshot of the given device", CommandOptionType.SingleValue);
            CommandOption opWatch = cli.Option("--watch <devIp>", "Grabs a screenshot every 5 seconds of the given device", CommandOptionType.SingleValue);
            CommandOption opUpdate = cli.Option("--update <devIp>" , "Updates the given device", CommandOptionType.SingleValue);
            CommandOption opUpdateAll = cli.Option("--update-all" , "Updates all devices", CommandOptionType.NoValue);
            CommandOption opTouch = cli.Option("--touch <ip>,<x>,<y>", "Sends a touch event", CommandOptionType.MultipleValue);
            CommandOption opVerbose=  cli.Option("-v", "Turns on verbose mode", CommandOptionType.NoValue);
            CommandOption opVerifyUpdate =  cli.Option("--vu", "Verifies devices are with the latest versions available.", CommandOptionType.NoValue);

            //            CommandOption uppercase = cli.Option("-u | --uppercase", "Display the greeting in uppercase.",
            //                CommandOptionType.NoValue);
            cli.HelpOption("-? | --help"); 
            cli.OnExecute(() =>
            {
                Startup(hostname.Value());
                if(opNoForward.HasValue()) _manager.DisableForwarding();
                if(opVerbose.HasValue()) _manager.EnableVerbouseMode();
                if (opReboot.HasValue()) _manager.RebootAll().Wait();
                else if (opRestart.HasValue()) _manager.RestartAll().Wait();
                else if (opScreenshot.HasValue()) _manager.Screenshot(opScreenshot.Value()).Wait();
                else if (opWatch.HasValue()) _manager.Watch(opWatch.Value()).Wait();
                else if (opUpdate.HasValue()) _manager.Update(opUpdate.Value()).Wait();
                else if (opUpdateAll.HasValue()) _manager.UpdateAll().Wait();
                else if (opVerifyUpdate.HasValue()) _manager.VerifyUpdates().Wait();
                else if (opTouch.HasValue()) {
                    var touchVal = opTouch.Value().Split(',');
                    _manager.Touch(touchVal[0], int.Parse(touchVal[1]), int.Parse(touchVal[2])).Wait();
                }
                return 0;
            }); 
            var res = cli.Execute(args);
//            while (true)
//            {
//                var input = Console.ReadLine() + "\r\n";
//                SmDevice fdevice = _manager.ConnectedDevices.Values.First();
//                fdevice.Execute(input).Wait();
//            }
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
