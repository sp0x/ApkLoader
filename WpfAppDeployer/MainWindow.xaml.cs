using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using LibAppDeployer;
using Microsoft.Extensions.FileProviders;
using SharpAdbClient;

namespace WpfAppDeployer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private PackageManager mPkgMgr;
        private KeyHelper mKeyHelper;
        private AdbManager mAdb;
        private ObservableCollection<AndroidPackage> Packages { get; set; }
        private ObservableCollection<DeviceData> Devices { get; set; }
        
        private static HashSet<string> mPackages = new HashSet<string>(new string[]
        {
            "com.autostart"
        });

        private AndroidPackage _mSelectedPackage;

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
            Packages = new ObservableCollection<AndroidPackage>();
            Devices = new ObservableCollection<DeviceData>();
            lblIcon.Content = "Loading keys.";
            mKeyHelper = new KeyHelper(true);
            lblIcon.Content = "Downloading packages";
            cmbDevice.ItemsSource = Devices;
            mPkgMgr = PackageManager.CreateWithUrlAndFetch(null);
            lstPackages.ItemsSource = Packages;
            mPkgMgr.PackageDownloaded += MPkgMgrOnPackageDownloaded;
            mPkgMgr.PackageLoaded += MPkgMgrOnPackageLoaded;
            mPkgMgr.AddAllAsync(mPackages.ToArray())
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        OnInitialPackagesFailed(t);
                    }
                    else
                    {
                        OnInitalPackagesDone(t);
                    }
                });
            string adbFile = Path.Combine(Directory.GetCurrentDirectory(), "platform_tools", "adb.exe");
            //Ask for the package to be selected..
            
            mAdb = new AdbManager(adbFile);
            StartListening();
            var devs = mAdb.GetDevicesInfos();
            foreach (var dev in devs)
            {
                Devices.Add(dev);
            }
            if (devs.Count == 0)
            {
                lblStatus.Text = "No devices connected.";
            }
            else
            {
                lblStatus.Text = $"Devices connected: {devs.Count}";
            }
        }

        private void OnInitalPackagesDone(Task obj)
        {
            Application.Current.Dispatcher?.Invoke(new Action(() =>
            {
                loader.Visibility = Visibility.Hidden;
                lblIcon.Content = "Please pick a package and a device.";
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
            AndroidPackage pkg = (AndroidPackage)_mSelectedPackage;
            return pkg?.Name;
        }

        private DeviceData GetSelectedDevice()
        {
            DeviceData d = (DeviceData)cmbDevice.SelectedItem;
            return d;
        }

        private void UpdateDevices(DeviceData dd, bool isRemoved=false)
        {
            if (isRemoved)
            {
                Devices.Remove(dd);
                return;
            }
            if (Devices.Contains(dd))
            {
                return;
            }
            Devices.Add(dd);
        }

        private void StartListening()
        {
            mAdb.ListenForDevices();
            mAdb.DeviceConnected += async (sender, e) =>
            {
                var d = e.Device;
                string mPackage = GetSelectedPackage();
                Console.WriteLine($"Device connected: {d.Model} {d.Name}[{d.ToString()}]");
                Application.Current.Dispatcher?.Invoke(new Action(() =>
                {
                    lblStatus.Text = $"Device connected: {d.Name}[{d.ToString()}]";
                    var sp = GetSelectedPackage();
                    if (GetSelectedDevice() == null && sp!=null)
                    {
                        lblIcon.Content = $"To install `{sp}`, select a device.";
                    }
                    UpdateDevices(d);
                }));
                if (mPackage == null) return;
                //Mark that we're installing
                Application.Current.Dispatcher?.Invoke(new Action(() =>
                {
                    var dlbl = $"{d.Model} [{d.Serial}]";
                    NotifyInstallStarted(mPackage, dlbl);
                    lblIcon.Content = $"Installation started on {dlbl}";
                }));
                    
                await CheckPrerequisites(d);
                await InstallPackage(d, mPackage);
            };
            mAdb.DeviceDisconnected += (sender, e) =>
            {
                var d = e.Device;
                Console.WriteLine($"Device disconnected: {d.Name}[{d.ToString()}]");
                Application.Current.Dispatcher?.Invoke(new Action(() =>
                {
                    UpdateDevices(d, true);
                    lblStatus.Text = $"Device disconnected: {d.Name}[{d.ToString()}]";
                }));
            };
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



        private void BtnInstall_OnClick(object sender, RoutedEventArgs e)
        {
            string mPackage = GetSelectedPackage();
            DeviceData d = GetSelectedDevice();
            if (mPackage == null) return;
            if (d == null) return;
            string dlbl = $"{d.Model} {d.Serial}]";
            Application.Current.Dispatcher?.Invoke(new Action(() => { NotifyInstallStarted(mPackage, dlbl); }));
            CheckPrerequisites(d).ContinueWith((t) =>
            {
                InstallPackage(d, mPackage)
                    .ContinueWith((t1) =>
                    {
                        if (t1.IsFaulted)
                        {
                            Application.Current.Dispatcher?.Invoke(new Action(() =>
                            {
                                lblStatus.Text = $"Install of `{mPackage}` has failed. Error: {t1.Exception.Message}" ;
                                imgIcon.Source = UiExtensions.GetImage("Resources/installfailed.png");
                                imgIcon.Visibility = Visibility.Visible;
                                lblIcon.Content = "Install failed. Reconnect your device and try again.";
                                btnInstall.IsEnabled = true;
                            }));
                        }
                        else
                        {
                            Application.Current.Dispatcher?.Invoke(new Action(() =>
                            {
                                lblStatus.Text = $"Installation done.";
                                imgIcon.Visibility = Visibility.Hidden;
                                btnInstall.IsEnabled = true;
                            }));
                        }
                        
                    });
            });
            

        }

        private void NotifyInstallStarted(string mPackage, string dlbl)
        {
            lblStatus.Text = $"Starting install of `{mPackage}` to `{dlbl}`.";
            imgIcon.Source = UiExtensions.GetImage("Resources/installpkg.png");
            imgIcon.Visibility = Visibility.Visible;
            btnInstall.IsEnabled = false;
        }

        private void CmbDevice_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var pkg = GetSelectedPackage();
            if (pkg==null || e.AddedItems.Count == 0)
            {
                btnInstall.IsEnabled = false;
            }
            else
            {
                var item = e.AddedItems[0];
                btnInstall.IsEnabled = true;
            }
            
        }

        private void LstPackages_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var dev = GetSelectedDevice();
            if (e.AddedItems.Count > 0)
            {
                _mSelectedPackage = (AndroidPackage)e.AddedItems[0];
            }
            if (dev == null || e.AddedItems.Count == 0)
            {
                btnInstall.IsEnabled = false;
                return;
            }
            
            if (dev != null && e.AddedItems.Count > 0)
            {
                btnInstall.IsEnabled = true;
            }
        }
    }
}