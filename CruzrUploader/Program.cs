using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LibAppDeployer;
using Microsoft.Extensions.FileProviders;
using SharpAdbClient;

namespace CruzrUploader
{
    class Program
    {
        private static PackageManager mPkgMgr;
        private static KeyHelper mKeyHelper;
        private static AdbManager mAdb;

        private static HashSet<string> mPackages = new HashSet<string>(new string[]
        {
            "com.autostart",
            "com.netlyt.cruzrdb",
            "com.netlyt.cruzrdb.mini"
        });

        private static string mPackage;


        static async Task Main(string[] args)
        {
            Console.WriteLine(@"Copying keys..");
            mKeyHelper = new KeyHelper(true);
            string adbFile = Path.Combine(Directory.GetCurrentDirectory(), "platform_tools", "adb.exe");
            mPkgMgr = PackageManager.Initialize(mPackages.ToArray());

            mPackage = AskForPackageName();
            mAdb = new AdbManager(adbFile);
            mAdb.ListenForDevices();
            mAdb.DeviceConnected += async (sender, e) =>
            {
                var d = e.Device;
                Console.WriteLine($"Device connected: {d.Name}[{d.ToString()}]");
                await CheckPrerequisites(d);
                await InstallPackage(d, mPackage);
            };
            mAdb.DeviceDisconnected += (sender, e) =>
            {
                var d = e.Device;
                Console.WriteLine($"Device disconnected: {d.Name}[{d.ToString()}]");
            };
            await installToCurrentDevices(mPackage);

            while (true)
            {
                Console.ReadLine();
            }
        }

        private static async Task installToCurrentDevices(string pkgname)
        {
            var devs = mAdb.GetDevicesInfos();
            foreach (var device in devs)
            {
                await CheckPrerequisites(device);
                await InstallPackage(device, pkgname);
            }
        }

        private static async Task CheckPrerequisites(DeviceData dd)
        {
            var dev = mAdb.GetDevice(dd.Serial);
            bool hasAutostart = await dev.HasPackage("com.autostart");
            if (!hasAutostart)
            {
                using var pkgstrm = mPkgMgr.GetStream("com.autostart");
                await dev.Install(pkgstrm);
                AdbClient.Instance.Root(dd);
                var embeddedProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
                using Stream autostartConfig = embeddedProvider.GetFileInfo("assets\\autostart.xml").CreateReadStream();
//                string rempath = "/data/data/com.autostart/shared_prefs/autostart.xml";
//                //await dev.Upload(autostartConfig, rempath);
//                dev.SyncService.Push(autostartConfig, rempath, 644, DateTime.Now, null, CancellationToken.None);
//                //Start the app
//                await dev.StartActivity("com.autostart/com.autostart.AutoStartActivity");
                //Add the autostart
            }

        }

        private static async Task InstallPackage(DeviceData dd, string pkgname)
        {
            Console.WriteLine($"Installing to {dd}");
            var dev = mAdb.GetDevice(dd.Serial);
            var rcvr = new ConsoleOutputReceiver();
            //Uninstall it firstly.
            await dev.Uninstall(pkgname, rcvr);
            Console.WriteLine(rcvr);
            //Install it now
            using var pkgstrm = mPkgMgr.GetStream(pkgname);
            await dev.Install(pkgstrm);
            Console.WriteLine($"Installation finished on {dd}");
            string activity = $"{pkgname}/.MainActivity";
            await dev.StartActivity(activity);
        }

        private static string AskForPackageName()
        {
            string selectedPackage = null;
            while (true)
            {
                var i = 1;
                Console.WriteLine("Select a package that you want to install.");
                foreach (var pkg in mPackages)
                {
                    Console.WriteLine($"{i} - {pkg}");
                    i++;
                }

                string resp = Console.ReadLine();
                int pkgi = 0;
                if (Int32.TryParse(resp, out pkgi))
                {
                    selectedPackage = mPackages.ToList()[pkgi - 1];
                    break;
                }
            }

            ;
            Console.WriteLine(
                $"Connect your device to install `{selectedPackage}`.\nThe package will be installed to any currently connected device.");
            return selectedPackage;
        }
    }
}