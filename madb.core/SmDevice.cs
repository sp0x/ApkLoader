using System;
using System.Collections.Generic;
using System.Text;
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


        public SmDevice(RemoteAdbDevice remDevice) 
            : base(remDevice.EndPoint.Address.Address.ToString())
        {
            _device = remDevice;
        }

        public void Reboot()
        { 
            
        }

        public async void GetShell()
        {
            _shellStream = await _device.CreateShellStream();
            var printer = new ActionBlock<byte[]>(x =>
            {
                var str = Encoding.UTF8.GetString(x);
                Console.WriteLine(str);
            });
            _shellStream.LinkTo(printer);
        }

        public void Execute(string command)
        {
            if (_shellStream == null) GetShell();
            var data = Encoding.UTF8.GetBytes(command);
            _shellStream.Post(data);
        }
    }
}
