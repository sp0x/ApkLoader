using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

namespace coreadb
{
    public class DomainManager
    {
        private AdbManager _manager;
        private NetworkManager _netManager;
        private IConfigurationRoot _config;
        private MySqlConnection _dbConnection;
        public Dictionary<string, SmDevice> ConnectedDevices { get; private set; }
        private bool _shouldForward;
        private bool _verbose;
        private string _updateUrl;

        public DomainManager(IConfigurationRoot config, AdbManager manager, NetworkManager netManager)
        {
            _verbose = false;
            _shouldForward=true;
            ConnectedDevices = new Dictionary<string, SmDevice>();
            _config = config;
            _manager = manager;
            _netManager = netManager;
            //Setup db config
            var updateSection = config.GetSection("update");
            _updateUrl = updateSection["sigma_base_url"];
            var dbSection = config.GetSection("db");
            var user = dbSection["user"];
            var pass = dbSection["pass"];
            var database = dbSection["database"];
            var host = dbSection["host"];
            host = !string.IsNullOrEmpty(host) ? host : _netManager.TargetHost.ToString();
            var isSecure = dbSection["secure"]!=null;
            var secureSuffix = isSecure ? "" : ";SslMode=none";
            var timeout = ";Connection Timeout=15";
            var constr =
                $"server={host};user id={user};password={pass};persistsecurityinfo=True;port=3306;database={database}{secureSuffix}{timeout}";
            _dbConnection = new MySqlConnection()
            {
                ConnectionString = constr
                
            };
            
            _dbConnection.Open();
        }
        public void DisableForwarding(){
            _shouldForward = false;
        }

        public void EnableVerbouseMode(){
            _verbose = true;
        }

        private void NoticeDevice(SmDevice newDevice)
        {
            if (newDevice != null && newDevice.Ip!=null)
            {
                ConnectedDevices.Add(newDevice.Ip, newDevice);
            }
            
        }

        public async Task RebootAll()
        {
            var rebootBuff = new BufferBlock<SmDeviceInfo>();
            var connectorBlock = new TransformBlock<SmDeviceInfo, SmDevice>(new System.Func<SmDeviceInfo, Task<SmDevice>>(ConnectToDevice));
            var rebootBlock = new ActionBlock<SmDevice>(x => x?.Reboot()); 
            rebootBuff.LinkTo(connectorBlock, new DataflowLinkOptions {PropagateCompletion = true});
            connectorBlock.LinkTo(rebootBlock, new DataflowLinkOptions {PropagateCompletion = true});
            foreach (var device in GetDevices())
            {
                rebootBuff.Post(device);
            }
            rebootBuff.Complete();
            await rebootBlock.Completion;
        }
        public async Task RestartAll()
        {
            var restartBuff = new BufferBlock<SmDeviceInfo>();
            var connectorBlock = new TransformBlock<SmDeviceInfo, SmDevice>(new System.Func<SmDeviceInfo, Task<SmDevice>>(ConnectToDevice));
            var rebootBlock = new ActionBlock<SmDevice>(x => {
                var ret = x?.KillSmApp().Result;
                Console.WriteLine($"{x.EndPoint} Restarted app: {ret}");
            });
            restartBuff.LinkTo(connectorBlock, new DataflowLinkOptions { PropagateCompletion = true });
            connectorBlock.LinkTo(rebootBlock, new DataflowLinkOptions { PropagateCompletion = true });
            foreach (var device in GetDevices())
            {
                restartBuff.Post(device);
            }
            restartBuff.Complete();
            await rebootBlock.Completion;
        }

        public async Task Screenshot(string id)
        {
            var deviceInfo = GetDevice(id);
            var device = await CreateDevice(deviceInfo);
            _manager.Screenshot(device, $"{id}.png");
        }

        public async Task Watch(string id, int interval = 5000)
        {
            var deviceInfo = GetDevice(id);
            var device = await CreateDevice(deviceInfo);
            while(true){
                _manager.Screenshot(device, $"{id}.png");
                Console.WriteLine($"{DateTime.Now} Taken screenshot");
                await Task.Delay(interval);
            }
        }

        public async Task Update(string ip){
            var deviceInfo = GetDevice(ip);
            var device = await ConnectToDevice(deviceInfo);
            var result = await device.Update(_updateUrl);
            Console.WriteLine(result);
            Console.WriteLine($"Updated: {device.EndPoint}");
        }

        public async Task UpdateAll(){
            var updateBuff = new BufferBlock<SmDeviceInfo>();
            var connectorBlock = new TransformBlock<SmDeviceInfo, SmDevice>(new System.Func<SmDeviceInfo, Task<SmDevice>>(ConnectToDevice));
            var updaterBlock = new ActionBlock<SmDevice>(x => {
                x?.Update(_updateUrl).ContinueWith((t)=>{
                    var str = x.EndPoint;
                    Console.WriteLine($"Updated {str}");
                });
            }); 
            updateBuff.LinkTo(connectorBlock, new DataflowLinkOptions {PropagateCompletion = true});
            connectorBlock.LinkTo(updaterBlock, new DataflowLinkOptions {PropagateCompletion = true});
            foreach (var device in GetDevices())
            {
                updateBuff.Post(device);
            }
            updateBuff.Complete();
            await updaterBlock.Completion;
        }

        public async Task Touch(string ip, int x, int y){
            var deviceInfo = GetDevice(ip);
            var device = await ConnectToDevice(deviceInfo);
            var result = await device.Touch(x, y);
            Console.WriteLine(result);
        }

        private async Task<SmDevice> ConnectToDevice(SmDeviceInfo info)
        {
            SmDevice device = null;
            if(_shouldForward){
                var port = _netManager.Forward(info.Ip, 5555);
                device = await _manager.ConnectDevice("127.0.0.1", port, _verbose);
            }else{
                uint port = 5555;
                device = await _manager.ConnectDevice(info.Ip, port, _verbose);
            } 
            NoticeDevice(device);
            return device;
        }

        private async Task<SmDevice> CreateDevice(SmDeviceInfo info)
        {
            SmDevice device = null;
            if(_shouldForward){
                var port = _netManager.Forward(info.Ip, 5555);
                device = _manager.CreateDevice("127.0.0.1", port);
            }else{
                uint port = 5555;
                device = _manager.CreateDevice(info.Ip, port);
            }
            NoticeDevice(device);
            return device;
        }

        public IEnumerable<SmDeviceInfo>  GetDevices()
        {
            var listCommand = new MySqlCommand("SELECT * from sm_devices where work_or_not=1 AND is_active=1", _dbConnection);
            using (var reader = listCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var deviceIp = reader["ip"].ToString();
                    var device = new SmDeviceInfo(deviceIp);
                    yield return device;
                }
            }
        }
        public SmDeviceInfo GetDevice(string ip)
        {
            var listCommand = new MySqlCommand($"SELECT * from sm_devices where ip=\"{ip}\" AND work_or_not=1 AND is_active=1", _dbConnection);
            using (var reader = listCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    var deviceIp = reader["ip"].ToString();
                    var device = new SmDeviceInfo(deviceIp);
                    return device;
                }
            }
            return null;
        }

    } 
}