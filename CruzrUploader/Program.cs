using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.FileProviders;
using SharpAdbClient;
using SharpAdbClient.DeviceCommands;

namespace CruzrUploader
{
    class Program
    {
        private static PackageManager mPkgMgr;
        private static KeyHelper mKeyHelper;
        private static AdbManager mAdb;

        private static HashSet<string> mPackages = new HashSet<string>(new string[]
        {
            "com.netlyt.cruzrdb",
            "com.netlyt.cruzrdb.mini"
        });

        private static string mPackage;


        static async Task Main(string[] args)
        {
            Console.WriteLine(@"Copying keys..");
            mKeyHelper = new KeyHelper();
            mKeyHelper.CopyAdbKeys();
            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "packages");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            mPkgMgr = new PackageManager("https://apks.netlyt.io/", outputDir);

            foreach (var pkg in mPackages)
            {
                Console.WriteLine($"Downloading package: {pkg}");
                mPkgMgr.AddPackage(pkg);
            }

            mPackage = AskForPackageName();
            mAdb = new AdbManager();
            mAdb.ListenForDevices();
            mAdb.DeviceConnected += (sender, e) =>
            {
                var d = e.Device;
                Console.WriteLine($"Device connected: {d.Name}[{d.ToString()}]");
                installPackage(d, mPackage);
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
                await installPackage(device, pkgname);
            }
        }

        private static async Task installPackage(DeviceData dd, string pkgname)
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