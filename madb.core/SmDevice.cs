using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using SharpAdbClient;
using SharpAdbClient.Proto;

namespace coreadb
{
    public class SmDeviceInfo
    {
        public string Ip { get; private set; }
        public SmDeviceInfo(string ip)
        {
            this.Ip = ip;
        }
    }

    public class SmDevice : SmDeviceInfo
    {
        private RemoteAdbDevice _device;
        private IPropagatorBlock<byte[], byte[]> _shellStream;
        private bool _adbConnected = false;
        public IPEndPoint EndPoint { get; private set; }

        public SmDevice()
            : base(null)
        {
            
        }
        public SmDevice(RemoteAdbDevice remDevice) 
            : base(remDevice.EndPoint.Address.Address.ToString())
        {
            _device = remDevice;
            EndPoint = remDevice.EndPoint;
        }

        public async void Reboot()
        {
            Execute("busybox reboot");
        }

        public async void KillProcess(string name)
        {
            await Execute($"busybox pkill -9 {name}");
        }

        public async void KillSmApp()
        {
            KillProcess("com.intellitis.smartmodule.screens");
        }

        public bool ConnectToAdb(string adbExec = @"D:\SDK\android-win\platform-tools\adb.exe")
        {
            if (_adbConnected) return true;
            var command = adbExec;
            var returned = command.ExecuteCommand($" connect {_device.EndPoint}")?.Trim();
            if (!returned.Contains("connected"))
            {
                return false;
            }
            _adbConnected = true;
            return true;
        }

        public void Disconnect()
        {
            _device.Disconnect();
        }

        public async Task<IPropagatorBlock<byte[],byte[]>> GetShell()
        {
            _shellStream = await _device.CreateShellStream();
            var printer = new ActionBlock<byte[]>(x =>
            {
                var str = Encoding.UTF8.GetString(x);
                Console.WriteLine(str);
            });
            _shellStream.LinkTo(printer);
            return _shellStream;
        }

        public async void Screenshot(string filename)
        {
            ConnectToAdb();


            //Disabled for now
            //_device.CreateScreenshot(filename);
        }

        public async Task<string> Execute(string command)
        {
            return await _device.Execute(command);
        }
    }
}
