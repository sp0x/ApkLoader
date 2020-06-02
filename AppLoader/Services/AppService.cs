using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LibAppDeployer;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using SharpAdbClient;

namespace AppLoader.Services
{
    public class AppService
    {
        private static PackageManager mPkgMgr;
        private static KeyHelper mKeyHelper;
        private static AdbManager mAdb;

        private static HashSet<string> mPackages = new HashSet<string>(new string[]
        {
            "com.autostart"
        });

        private static string mPackage;
        private ILogger<AppService> _logger;
        private SecureSettings _secureSettings;

        public AppService(ILogger<AppService> logger, IOptions<SecureSettings> secureSettings)
        {
            _logger = logger;
            _secureSettings = secureSettings.Value;
        }

        public void Run()
        {

            _logger.LogInformation(@"Copying keys..");
            mKeyHelper = new KeyHelper(true);
            string adbFile = Path.Combine(Directory.GetCurrentDirectory(), "platform_tools", "adb.exe");
            Uri apkStoreUri = new Uri(_secureSettings.ApkStoreHost!=null ? _secureSettings.ApkStoreHost : "https://apks.netlyt.io/");
            mPkgMgr = PackageManager.CreateWithUrlAndFetch(apkStoreUri, mPackages.ToArray());

            mPackage = AskForPackageName();
            mAdb = new AdbManager(adbFile);
            mAdb.ListenForDevices();
            mAdb.DeviceConnected += async (sender, e) =>
            {
                var d = e.Device;
                _logger.LogInformation($"Device connected: {d.Name}[{d.ToString()}]");
                await CheckPrerequisites(d);
                await InstallPackage(d, mPackage);
            };
            mAdb.DeviceDisconnected += (sender, e) =>
            {
                var d = e.Device;
                _logger.LogInformation($"Device disconnected: {d.Name}[{d.ToString()}]");
            };
            installToCurrentDevices(mPackage).Wait();

            while (true)
            {
                Console.ReadLine();
            }
        }

        private async Task installToCurrentDevices(string pkgname)
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
            }
        }

        private async Task InstallPackage(DeviceData dd, string pkgname)
        {
            _logger.LogInformation($"Installing to {dd}");
            var dev = mAdb.GetDevice(dd.Serial);
            var rcvr = new ConsoleOutputReceiver();
            //Uninstall it firstly.
            await dev.Uninstall(pkgname, rcvr);
            //Install it now
            using var pkgstrm = mPkgMgr.GetStream(pkgname);
            await dev.Install(pkgstrm);
            _logger.LogInformation($"Installation finished on {dd}");
            string activity = $"{pkgname}/.MainActivity";
            await dev.StartActivity(activity);
        }

        private string AskForPackageName()
        {
            string selectedPackage = null;
            while (true)
            {
                var i = 1;
                _logger.LogInformation("Select a package that you want to install.");
                foreach (var pkg in mPackages)
                {
                     _logger.LogInformation($"{i} - {pkg}");
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
            _logger.LogInformation(
                $"Connect your device to install `{selectedPackage}`.\nThe package will be installed to any currently connected device.");
            return selectedPackage;
        }
    }
}
