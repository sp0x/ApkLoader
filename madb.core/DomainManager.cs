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

        public DomainManager(IConfigurationRoot config, AdbManager manager, NetworkManager netManager)
        {
            ConnectedDevices = new Dictionary<string, SmDevice>();
            _config = config;
            _manager = manager;
            _netManager = netManager;
            //Setup db config
            var dbSection = config.GetSection("db");
            var user = dbSection["user"];
            var pass = dbSection["pass"];
            var database = dbSection["database"];
            var isSecure = dbSection["secure"]!=null;
            var secureSuffix = isSecure ? "" : ";SslMode=none";
            _dbConnection = new MySqlConnection()
            {
                ConnectionString =
                    $"server={_netManager.TargetHost};user id={user};password={pass};persistsecurityinfo=True;port=3306;database={database}{secureSuffix}"
            };
            _dbConnection.Open();
        }

        private void NoticeDevice(SmDevice newDevice)
        {
            if (newDevice != null && newDevice.Ip!=null)
            {
                ConnectedDevices.Add(newDevice.Ip, newDevice);
            }
            
        }

        public async void RebootAll()
        {
            var rebootBuff = new BufferBlock<SmDeviceInfo>();
            var connectorBlock = new TransformBlock<SmDeviceInfo, SmDevice>(new System.Func<SmDeviceInfo, Task<SmDevice>>(CreateDevice));
            var rebootBlock = new ActionBlock<SmDevice>(x =>
            {
                if (x != null)
                {
                    //_manager.Screenshot(x, "scrfile.png");
                    x.Reboot();
                    //x.ConnectToAdb();
                }
            }); 
            rebootBuff.LinkTo(connectorBlock, new DataflowLinkOptions {PropagateCompletion = true});
            connectorBlock.LinkTo(rebootBlock, new DataflowLinkOptions {PropagateCompletion = true});
            foreach (var device in GetDevices().Take(1))
            {
                rebootBuff.Post(device);
            }
            rebootBuff.Complete();
            //await rebootBlock.Completion;
        }
        public async void RestartAll()
        {
            var rebootBuff = new BufferBlock<SmDeviceInfo>();
            var connectorBlock = new TransformBlock<SmDeviceInfo, SmDevice>(new System.Func<SmDeviceInfo, Task<SmDevice>>(CreateDevice));
            var rebootBlock = new ActionBlock<SmDevice>(x =>
            {
                if (x != null)
                {
                    //_manager.Screenshot(x, "scrfile.png");
                    x.KillSmApp();
                    //x.ConnectToAdb();
                }
            });
            rebootBuff.LinkTo(connectorBlock, new DataflowLinkOptions { PropagateCompletion = true });
            connectorBlock.LinkTo(rebootBlock, new DataflowLinkOptions { PropagateCompletion = true });
            foreach (var device in GetDevices().Take(1))
            {
                rebootBuff.Post(device);
            }
            rebootBuff.Complete();
            //await rebootBlock.Completion;
        }

        public async void Screenshot(string id)
        {
            var deviceInfo = GetDevice(id);
            var device = await CreateDevice(deviceInfo);
            _manager.Screenshot(device, $"{id}.png");
        }

        private async Task<SmDevice> ConnectToDevice(SmDeviceInfo arg)
        {
            var port = _netManager.Forward(arg.Ip, 5555);
            var device = await _manager.ConnectDevice("127.0.0.1", port);
            NoticeDevice(device);
            return device;
        }

        private async Task<SmDevice> CreateDevice(SmDeviceInfo info)
        {
            var port = _netManager.Forward(info.Ip, 5555);
            var device = _manager.CreateDevice("127.0.0.1", port);
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