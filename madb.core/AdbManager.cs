using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SharpAdbClient;
using SharpAdbClient.Proto;

namespace coreadb
{
    public class AdbManager
    {

        private Dictionary<string, DeviceData>  _deviceList =  new Dictionary<string, DeviceData>();
        private AdbServer _server;
        private DeviceMonitor _monitor;

        public AdbManager()
        {
            _server = new AdbServer();
            _server.StartServer(GetAdbPath(), restartServerIfNewer: false);
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
            Console.WriteLine($"Device connected {dev.Name}[{dev.Model}/{dev.Serial}]"); 
            //EchoTest(args.Device.Serial);
        }

        private void OnDeviceDisconnected(object sender, DeviceDataEventArgs args)
        {
            var dev = args.Device;
            Console.WriteLine($"Device disconnected {dev.Name}[{dev.Model}/{dev.Serial}]");
        }
        private void CacheDeviceList()
        {
            foreach (var dev in AdbClient.Instance.GetDevices())
            {
                _deviceList[dev.Serial] = dev;
            }
        }

//        private static void ConnectNetworkDevice(string host, int port)
//        {
//            var addr = Dns.Resolve(host).AddressList[0];
//            var devEndpoint = new IPEndPoint(addr, port);
//
//            //var adbClient = AdbClient.Instance;
//            //adbClient.Connect(devEndpoint);
//            //var devs = AdbClient.Instance.GetDevices();
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

        private Device GetDevice(string serial)
        {
            var data = GetDeviceData(serial);
            var dev = new Device(data);
            return dev;
        }

        public async Task<SmDevice> GetSmDevice(string ip, uint port)
        {
            var data = await RemoteAdbDevice.Factory.Create(ip, port);
            var dev = new SmDevice(data);
            return dev;
        }


        private DeviceData GetDeviceData(string serial)
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
                return @"D:\SDK\android-win\platform-tools\adb.exe";
            }
            else
            {
                throw new Exception("Unsupported OS!");
                return null;
            }
        }


        public async Task<SmDevice> ConnectDevice(string s, uint port)
        {
            var adbClient = AdbClient.Instance;
            RemoteAdbDevice remoteDev = await RemoteAdbDevice.Factory.Create(s, port);
            var smDevice = new SmDevice(remoteDev);
            return smDevice;
        }
    }
}
