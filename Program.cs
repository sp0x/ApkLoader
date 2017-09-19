using System;
using System.Net;
using SharpAdbClient;
using System.Linq;
using System.Collections.Generic;

namespace coreadb
{
    class Program
    {
        private static AdbServer _server;
        private static string _adbLocation = "/usr/bin/adb";
        private static Dictionary<string, DeviceData> _deviceList;
        private static DeviceMonitor _monitor;
        static void Main(string[] args)
        {
            _deviceList =  new Dictionary<string, DeviceData>();
            Startup();
            ListenForDevices();
            while(true) Console.ReadLine();
        }

        private static void ListenForDevices(){
            var sock = new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort);
            _monitor = new DeviceMonitor(new AdbSocket(sock));
            _monitor.DeviceConnected += OnDeviceConnected;
            _monitor.DeviceDisconnected += OnDeviceDisconnected;
            _monitor.Start();
        }

        private static void OnDeviceConnected(object sender, DeviceDataEventArgs args){
            var dev = args.Device;
            CacheDeviceList();
            Console.WriteLine($"Device connected {dev.Name}[{dev.Model}/{dev.Serial}]");
            ListDir(dev.Serial, "/");
            //EchoTest(args.Device.Serial);
        }

        private static void OnDeviceDisconnected(object sender, DeviceDataEventArgs args){
            var dev = args.Device;
            Console.WriteLine($"Device disconnected {dev.Name}[{dev.Model}/{dev.Serial}]");
        }

        private static void CacheDeviceList(){
            foreach(var dev in AdbClient.Instance.GetDevices()){
                _deviceList[dev.Serial] = dev;
            }
        }

        private static DeviceData GetDevice(string serial){
            if(!_deviceList.ContainsKey(serial)){
                CacheDeviceList();
            }
            if(!_deviceList.ContainsKey(serial)) return null;
            return _deviceList[serial];
        }

        public static void ListDir(string serial, string basePath){
            var inst = AdbClient.Instance;
            var dev = GetDevice(serial);
            var receiver = new ConsoleOutputReceiver();
            inst.ExecuteRemoteCommand($"ls {basePath}", dev, receiver);
            Console.WriteLine(receiver.ToString());
        }

        private static void EchoTest(string serial)
        {
            var device = GetDevice(serial);
            var receiver = new ConsoleOutputReceiver();
            AdbClient.Instance.ExecuteRemoteCommand("echo Hello, World", device, receiver);
            Console.WriteLine("The device responded:");
            Console.WriteLine(receiver.ToString());
        }

        private static void Startup(){
            _server = new AdbServer();
            _server.StartServer(_adbLocation, restartServerIfNewer: false);
        }
    }
}
