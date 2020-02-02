using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using SharpAdbClient;
using SharpAdbClient.Extensions;
using SharpAdbClient.Proto;

namespace coreadb
{
    public class SmAdbManager
    {
        private AdbManager _manager;

        public SmAdbManager(AdbManager adb)
        {
            _manager = adb;
        }

        public async Task<SmDevice> GetSmDevice(string ip, uint port)
        {
            var data = await RemoteAdbDevice.Factory.Create(ip, port);
            var dev = new SmDevice(data);
            return dev;
        }

        public async Task<SmDevice> ConnectDevice(string s, uint port, bool verboseMode = false)
        {
            var adbClient = AdbClient.Instance;
            RemoteAdbDevice remoteDev = await RemoteAdbDevice.Factory.Create(s, port, verboseMode);
            var smDevice = new SmDevice(remoteDev);
            return smDevice;
        }

        public void Screenshot(SmDevice device, string filename)
        {
            var serial = device.EndPoint.ToString();
            device.ConnectToAdb();
            var deviceData = _manager.GetDeviceData(serial);
            var adbDev = new Device(deviceData);
            Console.WriteLine($"Fetching screenshot from {serial} ..");
            var screen = adbDev.Screenshot;
            screen.Save(filename);
        }

        public SmDevice CreateDevice(string s, uint port)
        {
            var endpoint = new IPEndPoint(Dns.Resolve(s).AddressList[0], (int)port);
            var remoteDev = new RemoteAdbDevice(endpoint);
            var smDev = new SmDevice(remoteDev);
            return smDev;
        }
    }
}
