﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
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
        private Dictionary<string[], string> _updateSums;

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
            _updateSums = new Dictionary<string[], string>();
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
            CalculateUpdateChecksums();
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

        private void CalculateUpdateChecksums()
        {
            var files = new string[][] {
                new string[]{"SmartModule_Sigma.apk" , "SmartModule.apk" }
            };
            var sums = new Dictionary<string[], string>();
            foreach (var file in files)
            {
                var fileUrl = $"{_updateUrl}/{file[0]}";
                var request = WebRequest.Create(fileUrl);
                var response = request.GetResponse();
                using (var stream = response.GetResponseStream())
                {
                    using (var md5 = MD5.Create())
                    { 
                        var md5Sum = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
                        sums.Add(file, md5Sum);
                    }
                }
            }
            _updateSums = sums;
        }

        public async Task Update(string ips)
        {
            var ipsplit = ips.Split(",");
            var devices = ipsplit.Select(x => SmDeviceInfo.FromIp(x));
            var smDeviceInfos = devices.Where(x=>x!=null).ToArray();
            await UpdateAll(smDeviceInfos);
//            var deviceInfo = GetDevice(ip);
//            var device = await ConnectToDevice(deviceInfo);
//            var result = await device.Update(_updateUrl);
//            Console.WriteLine(result);
//            Console.WriteLine($"Updated: {device.EndPoint}");
        }

        public async Task UpdateFloor(string floor)
        {
            var devices = this.GetDevicesOnFloor(floor).ToArray();
            await UpdateAll(devices);
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
            if(device!=null){
                device.Ip = info.Ip;
                device.Id = info.Id;
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
                    var device = new SmDeviceInfo(deviceIp, long.Parse(reader["id"].ToString()));
                    yield return device;
                }
            }
        }

        public IEnumerable<SmDeviceInfo> GetDevicesOnFloor(string floor)
        {
            var listCommand = new MySqlCommand($@"SELECT DISTINCT
	sm_devices.*
FROM
	sm_devices
JOIN beds ON beds.id = sm_devices.bed_id
JOIN hospitals_sections ON hospitals_sections.id = beds.section_id
WHERE
	work_or_not = 1
AND is_active = 1
and hospitals_sections.floor = {floor}", _dbConnection);
            using (var reader = listCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    var deviceIp = reader["ip"].ToString();
                    var device = new SmDeviceInfo(deviceIp, long.Parse(reader["id"].ToString()));
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
                    var device = new SmDeviceInfo(deviceIp, long.Parse(reader["id"].ToString()));
                    return device;
                }
            }
            return null;
        }

        public async Task ResetAllFloor(string floor)
        {
            var devs = GetDevicesOnFloor(floor);
            await ResetAll(devs.ToArray());
        }

        public async Task ResetAll(SmDeviceInfo[] devices)
        {
            var options = new ExecutionDataflowBlockOptions { BoundedCapacity = 15 };
            var restartBuff = new BufferBlock<SmDeviceInfo>(options);
            var connectorBlock = new TransformBlock<SmDeviceInfo, SmDevice>(new System.Func<SmDeviceInfo, Task<SmDevice>>(ConnectToDevice), options);
            var taskLock = new object();
            var restartTasks = new List<Task>();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var passed = 0; 
            var restartBlock = new ActionBlock<SmDevice>(x => {
                var task = x?.KillSmApp();
                if (task == null) return;
                lock (taskLock)
                {
                    restartTasks.Add(task.ContinueWith(taskRes =>
                    {
                        Interlocked.Increment(ref passed);
                        var perc = 100.0d * ((double)passed / (double)devices.Length);
                        Console.WriteLine($"{perc:0.00}% Restarted app {x.Ip}"); 
                    }));
                }
            }, new ExecutionDataflowBlockOptions { BoundedCapacity = _capacity, MaxDegreeOfParallelism = 20 });
            restartBuff.LinkTo(connectorBlock, new DataflowLinkOptions { PropagateCompletion = true });
            connectorBlock.LinkTo(restartBlock, new DataflowLinkOptions { PropagateCompletion = true });
            foreach (var device in devices)
            {
                var posted = false;
                while (!posted) posted = restartBuff.SendAsync(device).Result;
            }
            restartBuff.Complete();
            await restartBlock.Completion; 
            Task.WaitAll(restartTasks.ToArray());
            watch.Stop();
            var timeSpent = watch.Elapsed.TotalSeconds;
            var perDevice = timeSpent / devices.Length;
            Console.WriteLine($"Spent total {timeSpent:0.000} with {perDevice:0.000}");
        }

        public async Task UpdateAll(SmDeviceInfo[] devices)
        {
            var options = new ExecutionDataflowBlockOptions { BoundedCapacity = _capacity };
            var updateBuff = new BufferBlock<SmDeviceInfo>(options);
            var connectorBlock = new TransformBlock<SmDeviceInfo, SmDevice>(new System.Func<SmDeviceInfo, Task<SmDevice>>(ConnectToDevice), options);
            var taskLock = new object();
            var updateTasks = new List<Task>();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var passed = 0;
            var updaterBlock = new ActionBlock<SmDevice>(x => {
                try
                {

                    var percCr = 100.0d * ((double) passed / (double) devices.Length);
                    Console.WriteLine($"{percCr:0.00}% Updating {x.Ip}");

                    var updateTask = x?.Update(_updateUrl).ContinueWith((t) =>
                    {
                        Interlocked.Increment(ref passed);
                        var perc = 100.0d * ((double) passed / (double) devices.Length);
                        Console.WriteLine($"{perc:0.00}% Updated {x.Ip}");
                    });
                    lock (taskLock)
                    {
                        updateTasks.Add(updateTask);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{x.Ip} Error: " + e.Message);
                }
            }, new ExecutionDataflowBlockOptions { BoundedCapacity = _capacity, MaxDegreeOfParallelism = 20 });
            updateBuff.LinkTo(connectorBlock, new DataflowLinkOptions { PropagateCompletion = true });
            connectorBlock.LinkTo(updaterBlock, new DataflowLinkOptions { PropagateCompletion = true });
            foreach (var device in devices)
            {
                var posted = false;
                while (!posted)
                {
                    posted = updateBuff.SendAsync(device).Result;
                }
            }
            updateBuff.Complete();
            await updaterBlock.Completion;
            Task.WaitAll(updateTasks.ToArray());
            watch.Stop();
            var timeSpent = watch.Elapsed.TotalSeconds;
            var perDevice = timeSpent / devices.Length;
            Console.WriteLine($"Spent total {timeSpent:0.000} with {perDevice:0.000}");
        }


        private int _capacity = 120;

        public async Task VerifyUpdatesOnFloor(string floor)
        {
            var devices = GetDevicesOnFloor(floor);
            await VerifyUpdates(devices.ToArray());
        }
        public async Task VerifyUpdates(SmDeviceInfo[] devices)
        {
            var options = new ExecutionDataflowBlockOptions { BoundedCapacity = _capacity};
            var updateBuff = new BufferBlock<SmDeviceInfo>(options);
            var connectorBlock = new TransformBlock<SmDeviceInfo, SmDevice>(new System.Func<SmDeviceInfo, Task<SmDevice>>(ConnectToDevice), options);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var passed = 0;
            var taskLock = new object();
            var verifyTasks = new List<Task>();
            var validatorBlock = new ActionBlock<SmDevice>(x =>
            {
                var newSums = new Dictionary<string, string>();
                foreach (var pair in _updateSums) newSums.Add(pair.Key[1], pair.Value);
                var resultsTask = Task<IDictionary<string, UpdateState>>.Factory.StartNew(()=> {
                    if(x!=null) Console.WriteLine($"#{x.Id} {x.Ip} Verifying..");
                    var result = x?.VerifyFiles(newSums);
                    return result;
                });
                lock (taskLock)
                {
                    verifyTasks.Add(resultsTask.ContinueWith(y =>
                    {
                        var result = y.Result;
                        Interlocked.Increment(ref passed);
                        var perc = 100.0d * ((double)passed / (double)devices.Length);
                        if (result != null)
                        {
                            var hasOldFiles = result.Values.Any(f => f== UpdateState.Old);
                            var hasTimedOut = result.Values.Any(f => f== UpdateState.TimedOut);
                            if (hasTimedOut)
                            {
                                Console.WriteLine($"#{x.Id} {perc:0.00}%[timeout] {x.Ip}");
                            }
                            else
                            {
                                Console.WriteLine($"#{x.Id} {perc:0.00}%[{(hasOldFiles ? "old" : "cur")}] {x.Ip}");
                            }
                            
                        }
                    }));
                }
            }, new ExecutionDataflowBlockOptions{ BoundedCapacity = _capacity, MaxDegreeOfParallelism = 20 });
            updateBuff.LinkTo(connectorBlock, new DataflowLinkOptions { PropagateCompletion = true });
            connectorBlock.LinkTo(validatorBlock, new DataflowLinkOptions { PropagateCompletion = true }); 
            foreach (var device in devices)
            {
                var posted = false;
                while (!posted)
                {
                    posted = updateBuff.SendAsync(device).Result;
                }
            }
            Console.WriteLine($"Processing devices: {devices.Length}");
            updateBuff.Complete();
            await validatorBlock.Completion;
            Task.WaitAll(verifyTasks.ToArray());
            watch.Stop();
            var timeSpent = watch.Elapsed.TotalSeconds;
            var perDevice = timeSpent / devices.Length;
            Console.WriteLine($"Spent total {timeSpent:0.000} with {perDevice:0.000}");
        }
    } 
}