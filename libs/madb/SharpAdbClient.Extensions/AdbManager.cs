namespace SharpAdbClient
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using SharpAdbClient.Proto;

    public class AdbManager
    {

        private Dictionary<string, DeviceData> _deviceList = new Dictionary<string, DeviceData>();
        private AdbServer _server;
        private DeviceMonitor _monitor;

        public AdbManager(string adbFile)
        {
            _deviceList = new Dictionary<string, DeviceData>();
            _server = new AdbServer();
            if (adbFile != null && File.Exists(adbFile))
            {
                _server.StartServer(adbFile, restartServerIfNewer: false);
            }
            else
            {
                _server.StartServer(GetAdbPath(), restartServerIfNewer: false);
            }
            
        }

        /// <include file='IDeviceMonitor.xml' path='/IDeviceMonitor/DeviceConnected/*'/>
        public event EventHandler<DeviceDataEventArgs> DeviceConnected;

        /// <include file='IDeviceMonitor.xml' path='/IDeviceMonitor/DeviceDisconnected/*'/>
        public event EventHandler<DeviceDataEventArgs> DeviceDisconnected;


        public List<DeviceData> GetDevicesInfos()
        {
            return AdbClient.Instance.GetDevices();
        }

        public void ListenForDevices()
        {
            var sock = new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort);
            _monitor = new DeviceMonitor(new AdbSocket(sock));
            _monitor.DeviceConnected += OnDeviceConnected;
            _monitor.DeviceDisconnected += OnDeviceDisconnected;
            _monitor.Start();
        }

        private void OnDeviceConnected(object sender, DeviceDataEventArgs args)
        {
            var dev = args.Device;
            CacheDeviceList();
            Debug.WriteLine($"Device connected {dev.Name}[{dev.Model}/{dev.Serial}]");
            DeviceConnected?.Invoke(this, args);
            //EchoTest(args.Device.Serial);
        }

        private void OnDeviceDisconnected(object sender, DeviceDataEventArgs args)
        {
            var dev = args.Device;
            Debug.WriteLine($"Device disconnected {dev.Name}[{dev.Model}/{dev.Serial}]");
            DeviceDisconnected?.Invoke(this, args);
        }

        private void CacheDeviceList()
        {
            foreach (var dev in AdbClient.Instance.GetDevices())
            {
                var serial = dev.Serial;
                try
                {
                    _deviceList[serial] = dev;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }
        }

        //        private static void ConnectNetworkDevice(string host, int port)
        //        {
        //            var addr = Dns.Resolve(host).AddressList[0];
        //            var devEndpoint = new IPEndPoint(addr, port);
        //
        //            //var adbClient = AdbClient.Instance;
        //            //adbClient.Connect(devEndpoint);
        //            //var devs = AdbClient.Instance.GetDevicesInfos();
        //            //devs = devs;
        //        }
        //
        //        public static void ListDir(string serial, string basePath)
        //        {
        //            var inst = AdbClient.Instance;
        //            var data = GetDeviceData(serial);
        //            var device = new Device(data);
        //            var screen = device.Screenshot;
        //            var receiver = new ConsoleOutputReceiver();
        //            inst.ExecuteRemoteCommand($"ls {basePath}", data, receiver);
        //            Console.WriteLine(receiver.ToString());
        //        }
        //
        //        private static void EchoTest(string serial)
        //        {
        //            DeviceData device = GetDeviceData(serial);
        //            var receiver = new ConsoleOutputReceiver();
        //            AdbClient.Instance.ExecuteRemoteCommand("echo Hello, World", device, receiver);
        //
        //            Console.WriteLine("The device responded:");
        //            Console.WriteLine(receiver.ToString());
        //        }

        public Device GetDevice(string serial)
        {
            var data = GetDeviceData(serial);
            var dev = new Device(data);
            return dev;
        }


        public DeviceData GetDeviceData(string serial)
        {
            if (!_deviceList.ContainsKey(serial))
            {
                CacheDeviceList();
            }
            if (!_deviceList.ContainsKey(serial)) return null;
            return _deviceList[serial];
        }

        public static string GetAdbPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "/usr/bin/adb";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {

                return FindExePath("adb.exe"); //@"D:\SDK\android-win\platform-tools\adb.exe";
            }
            else
            {
                throw new Exception("Unsupported OS!");
            }
        }





        /// <summary>
        /// Expands environment variables and, if unqualified, locates the exe in the working directory
        /// or the environment's path.
        /// </summary>
        /// <param name="exe">The name of the executable file</param>
        /// <returns>The fully-qualified path to the file</returns>
        /// <exception cref="System.IO.FileNotFoundException">Raised when the exe was not found</exception>
        private static string FindExePath(string exe)
        {
            exe = Environment.ExpandEnvironmentVariables(exe);
            if (!File.Exists(exe))
            {
                if (Path.GetDirectoryName(exe) == String.Empty)
                {
                    foreach (string test in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                    {
                        string path = test.Trim();
                        if (!String.IsNullOrEmpty(path) && File.Exists(path = Path.Combine(path, exe)))
                            return Path.GetFullPath(path);
                    }
                }
                return null;
            }
            return Path.GetFullPath(exe);
        }

    }
}
