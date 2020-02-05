using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using LibAppDeployer;
using Microsoft.Extensions.FileProviders;
using SharpAdbClient;

namespace WpfAppDeployer
{
    public class AndroidDevice
    {
        public string Serial { get; set; }
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private PackageManager mPkgMgr;
        private KeyHelper mKeyHelper;
        private AdbManager mAdb;
        private List<AndroidPackage> Packages { get; set; }
        private List<AndroidDevice> Devices { get; set; }
        
        private static HashSet<string> mPackages = new HashSet<string>(new string[]
        {
            "com.autostart",
            "com.netlyt.cruzrdb",
            "com.netlyt.cruzrdb.mini"
        });

        public MainWindow()
        {
            InitializeComponent();
            /**
             * TODO:
             * Copy keys
             * Load all packages
             * Download them
             * List connected devices
             * Ask for a package to be selected
             * Install to all current devices, also start listening for device connections
             */
            Packages = new List<AndroidPackage>();
            Devices = new List<AndroidDevice>();
            lblIcon.Content = "Loading keys.";
            mKeyHelper = new KeyHelper(true);
            lblIcon.Content = "Downloading packages";
            
            mPkgMgr = PackageManager.GetDefault();
            lstPackages.ItemsSource = Packages;
            cmbDevice.ItemsSource = Devices;
            mPkgMgr.PackageDownloaded += MPkgMgrOnPackageDownloaded;
            mPkgMgr.PackageLoaded += MPkgMgrOnPackageLoaded;
            mPkgMgr.AddAllAsync(mPackages.ToArray())
                .ContinueWith(OnInitalPackagesDone, TaskContinuationOptions.OnlyOnRanToCompletion)
                .ContinueWith(OnInitialPackagesFailed, TaskContinuationOptions.OnlyOnFaulted);
            string adbFile = Path.Combine(Directory.GetCurrentDirectory(), "platform_tools", "adb.exe");
            //Ask for the package to be selected..

            mAdb = new AdbManager(adbFile);
            StartListening();
            var devs = mAdb.GetDevicesInfos();
            foreach (var dev in devs)
            {
                var adev = new AndroidDevice();
                adev.Serial = dev.Serial;
                Devices.Add(adev);
            }
        }

        private void OnInitalPackagesDone(Task obj)
        {
            Application.Current.Dispatcher?.Invoke(new Action(() =>
            {
                loader.Visibility = Visibility.Hidden;
                MessageBox.Show("Please pick the package you want to install.");
                lblIcon.Content = "Please pick a package to be installed.";

            }));
        }

        private void OnInitialPackagesFailed(Task t)
        {
            Application.Current.Dispatcher?.Invoke(new Action(() =>
            {
                lblStatus.Text = "Initial package downloading failed";
            }));
        }

        private void MPkgMgrOnPackageDownloaded(object sender, PackageDownloadedEventArgs e)
        {
            lstPackages.Dispatcher?.Invoke(() =>
            {
                //lstPackages.Items.Add(e.Package);
                Packages.Add(e.Package);
            });
            
        }

        private void MPkgMgrOnPackageLoaded(object sender, PackageDownloadedEventArgs e)
        {
            lstPackages.Dispatcher?.Invoke(() =>
            {
                //lstPackages.Items.Add(e.Package);
                Packages.Add(e.Package);
            });

        }

        private string GetSelectedPackage()
        {
            AndroidPackage pkg = (AndroidPackage)lstPackages.SelectedItem;
            return pkg?.Name;
        }

        private void StartListening()
        {
            string mPackage = GetSelectedPackage();
            mAdb.ListenForDevices();
            mAdb.DeviceConnected += async (sender, e) =>
            {
                var d = e.Device;
                Console.WriteLine($"Device connected: {d.Name}[{d.ToString()}]");
                Application.Current.Dispatcher?.Invoke(new Action(() =>
                {
                    lblStatus.Text = $"Device connected: {d.Name}[{d.ToString()}]";
                }));
                var adev = new AndroidDevice();
                adev.Serial = d.Serial;
                Devices.Add(adev);
                if (mPackage == null)
                {
                    return;
                }

                await CheckPrerequisites(d);
                await InstallPackage(d, mPackage);
            };
            mAdb.DeviceDisconnected += (sender, e) =>
            {
                var d = e.Device;
                Console.WriteLine($"Device disconnected: {d.Name}[{d.ToString()}]");
                Application.Current.Dispatcher?.Invoke(new Action(() =>
                {
                    lblStatus.Text = $"Device disconnected: {d.Name}[{d.ToString()}]";
                }));
                //Remove it?
            };
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

        private async Task CheckPrerequisites(DeviceData dd)
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

        private async Task InstallPackage(DeviceData dd, string pkgname)
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
    }
}