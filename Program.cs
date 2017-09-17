using System;
using System.Net;
using SharpAdbClient;

namespace coreadb
{
    class Program
    {
        private static AdbServer _server;
        private static string _adbLocation = "/usr/bin/adb";
        private static DeviceMonitor _monitor;
        static void Main(string[] args)
        {
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
            Console.WriteLine($"Device connected {dev.Name}[{dev.Model}/{dev.Serial}]");
        }

        private static void OnDeviceDisconnected(object sender, DeviceDataEventArgs args){
            var dev = args.Device;
            Console.WriteLine($"Device disconnected {dev.Name}[{dev.Model}/{dev.Serial}]");
        }
        private static void Startup(){
            _server = new AdbServer();
            _server.StartServer(_adbLocation, restartServerIfNewer: false);
        }
    }
}
