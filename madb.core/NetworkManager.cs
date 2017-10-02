using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace coreadb
{
    public class NetworkManager
    {
        private IConfigurationRoot _config;
        private string _targetHost;
        private string _targetHostUser;
        private PrivateKeyFile _privateKey;
        private ConnectionInfo _connection;
        private SshClient _client;
        public object TargetHost => _targetHost;

        public NetworkManager(IConfigurationRoot config, string targetHost = null)
        {
            this._config = config;
            _targetHost = _config["targetHost"];
            if (!string.IsNullOrEmpty(targetHost)) _targetHost = targetHost;

            _targetHostUser = _config["targetHostUser"];
            _privateKey = new PrivateKeyFile(_config["privateKey"]);

            //Create tunnel
            _connection = new ConnectionInfo(_targetHost,
                _targetHostUser,
                new PrivateKeyAuthenticationMethod(_targetHostUser, _privateKey));
            
            _client = new SshClient(_connection);
            _client.Connect();

            var command = _client.CreateCommand("whoami");
            var whoami = command.Execute().Trim();
            Console.WriteLine($"[{_targetHost}] Loggedin in as: {whoami}");
        }


        public uint Forward(string host, uint port)
        {
            uint rndPort = (uint)new Random().Next(1025, 65000);
            var newPort = new ForwardedPortLocal("127.0.0.1", rndPort, host, port);
            newPort.Exception += delegate (object sender, ExceptionEventArgs e)
            {
                Console.WriteLine($"pf[adb:{newPort.Host}:{newPort.Port}]" + e.Exception.ToString());
            };
            _client.AddForwardedPort(newPort);
            newPort.Start();
            Console.WriteLine($"Forwarded {rndPort}->{host}:{port}");
            return rndPort;
        }
    }
}
