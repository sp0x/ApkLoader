using System;
using System.Collections.Concurrent;
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
        public string Ip { get; set; }
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

        public async Task<string> KillProcess(string name)
        {
            return await Execute($"busybox pkill -9 {name}");
        }

        public async Task<string> KillSmApp()
        {
            return await KillProcess("com.intellitis.smartmodule.screens*");
        }

        public async Task<string> Touch(int x, int y){
            return await _device.Touch(x, y);
        }

        public async Task CleanupAppFiles(){
            await Execute("rm /smartmodule/SmartModule.apk; rm /etc/CheckInstallPackage.sh; rm /smartmodule/CheckInstallPackage.sh"); 
        }

        public async Task<string> Update(string updateUrl){
            await CleanupAppFiles();
            var packageChecker = $"{updateUrl}/CheckInstallPackage-Sigma.sh";
            var result = await Execute($"cd /smartmodule; busybox wget {packageChecker} -O CheckInstallPackage.sh; chmod +x CheckInstallPackage.sh; ./CheckInstallPackage.sh 1");
            return result;
        }

        public bool ConnectToAdb(string adbExec = null)
        {
            if(adbExec==null){
                adbExec = AdbManager.GetAdbPath();
            }
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="updateSums"></param>
        /// <returns></returns>
        public IDictionary<string, UpdateState> VerifyFiles(Dictionary<string, string> updateSums)
        {
            var basePath = "/smartmodule/"; 
            var filenames = updateSums.Keys;
            var output = new ConcurrentDictionary<string, UpdateState>();
            var result = Parallel.ForEach(filenames, path =>
            {
                var fullPath = $"{basePath}{path}";
                try
                {
                    var sum = _device.FileChecksum(fullPath).Result;
                    var isValid = sum == updateSums[path];
                    output[path] = isValid ? UpdateState.UpToDate : UpdateState.Old;
                }
                catch (Exception ex)
                {
                    output[path] = UpdateState.TimedOut;
                }
            });
            return output;
        }
    }

    public enum UpdateState
    {
        Old,
        UpToDate,
        TimedOut
    }
}
