using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace SharpAdbClient.Proto
{
    public class RemoteAdbDevice : IAdbDeviceData
    {

        public event EventHandler Connected;
        private uint _streamId = 12345;
        private Task _readerTask;
        private TcpSocket _socket;
        public bool _waitingForHeader = true;
        private AdbPacket _lastPacket;
        private bool Suspended { get; set; }

        public uint HostVersion { get; private set; }
        public uint HostMaxData { get; private set; }
        public ConnectionState State { get; private set; }
        public string[] Banner
        {
            get
            {
                return _lastPacket.DataString()?.Split(':');
            }
        }
        public IPEndPoint EndPoint { get; private set; }
        public Dictionary<uint, AdbStream> OpenStreams { get; private set; }
        private CancellationToken _readCancellation;
        public bool PrintMessages {get;set;}

        public RemoteAdbDevice(IPEndPoint endpoint, CancellationToken? cancellationToken = null)
            : this(cancellationToken)
        {
            this.EndPoint = endpoint;
        }
        public RemoteAdbDevice(CancellationToken? cancellationToken = null)
        {
            OpenStreams = new Dictionary<uint, AdbStream>();
            _readCancellation = cancellationToken ?? CancellationToken.None;
        }

        private async Task<RemoteAdbDevice> Connect(IPEndPoint endpoint)
        {
            this.EndPoint = endpoint;
            _socket = new TcpSocket();
            _socket.Connect(endpoint);
            var cmd = Command.CreateConnectCommand();
            _socket.Send(cmd);
            var connectionPacket = await ReceivePacket(true);
            StartReading();
            return this;
        }

        public async Task<IPropagatorBlock<byte[], byte[]>> CreateStream(string path)
        {
            var streamId = _streamId++;
            var buff = Command.CreateOpenCommand(path, streamId);
            _socket.Send(buff);
            var stream = new AdbStream(_socket, streamId);
            stream.OnWriting += StreamWriting;
            OpenStreams.Add(streamId, stream);
            StartReading();
            return stream.Block;
        }

        public string GetEndpoint() => EndPoint.ToString();

        private void StreamWriting(object sender, EventArgs e)
        {
            //throw new NotImplementedException();
        }

        public async Task<IPropagatorBlock<byte[], byte[]>> CreateShellStream()
        {
            return await CreateStream("shell:");
        }

        public async void CreateScreenshot(string filename)
        {
            var file = File.Open(filename, FileMode.Create);
            var stream = await CreateStream("framebuffer:");
            var writer = new ActionBlock<byte[]>(bytes =>
            {
                file.Write(bytes, 0, bytes.Length);
            });
            stream.LinkTo(writer, new DataflowLinkOptions{PropagateCompletion = true});
            await stream.Completion;
            file.Flush();
        }

        public async Task<String> Execute(string command)
        {
            var stream = await CreateStream($"shell:{command}");
            var buffer = new MemoryStream();
            var binWr = new BinaryWriter(buffer);
            var action = new ActionBlock<byte[]>(x =>
            {
                binWr.Write(x);
            });
            stream.LinkTo(action, new DataflowLinkOptions {PropagateCompletion = true});
            await action.Completion;
            var output = Encoding.UTF8.GetString(buffer.ToArray());
            return output;
        }

        public async Task<string> Touch(int x, int y){
            var touch = SharpAdbClient.Proto.Touch.CreateTouch(x,y);
            var result = await Execute(touch);
            return result;
        }

        private void StartReading()
        {
            if ((_readerTask!=null && !_readerTask.IsCompleted)) return;
            _readerTask = new Task(() =>
            {
                while (true)
                {
                    ReceivePacket().Wait();
                    Thread.Sleep(1);
                }
            });
            _readerTask.Start();
        }
          

        private void OnPacket(AdbPacket packet)
        {
            _lastPacket = packet==null ? _lastPacket : packet;
            if(PrintMessages){
                Console.WriteLine($"Received command {_lastPacket}");
            }else{
                #if DEBUG
                Debug.WriteLine($"Received command {_lastPacket}");
                #endif
            }
            
            if (_lastPacket.Command == Command.CNXN)
            {
                HostVersion = _lastPacket.arg1;
                HostMaxData = _lastPacket.arg2;
                State = ConnectionState.Connected;
                Connected?.Invoke(this, EventArgs.Empty);
            }
            else if (_lastPacket.Command == Command.OKAY)
            {
                var streamId = _lastPacket.arg2;
                if (OpenStreams.ContainsKey(streamId))
                {
                    var stream = OpenStreams[streamId];
                    stream.RemoteId = _lastPacket.arg1;
                    //stream.HandlePacket(_lastPacket);
                }
            }
            else if (_lastPacket.Command == Command.WRTE)
            {
                var streamId = _lastPacket.arg2; 
                if (OpenStreams.ContainsKey(streamId))
                {
                    var stream = OpenStreams[streamId];
                    stream.HandlePacket(_lastPacket);
                    //Suspended = true;
                }
            }
            else if (_lastPacket.Command == Command.CLSE)
            {
                var streamId = _lastPacket.arg2;
                if (OpenStreams.ContainsKey(streamId))
                {
                    OpenStreams[streamId].Close();
                    OpenStreams.Remove(streamId);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="singleRun">Quit reading the packet after data has been received</param>
        /// <returns></returns>
        private async Task<AdbPacket> ReceivePacket(bool singleRun = false)
        {
            AdbPacket currentPacket = null;
            while (true)
            {
                while (Suspended)
                {
                    Thread.Sleep(1);
                }
                if (_waitingForHeader)
                {
                    byte[] header = new byte[24];
                    var readBytes = await _socket.ReceiveAsync(header, cancellationToken: _readCancellation);
                    if (readBytes == 0)
                    {
                        break;
                    }
                   
                    currentPacket = AdbPacket.FromBuffer(header);
                    if (currentPacket.DataLength == 0)
                    {
                        OnPacket(currentPacket);
                    }
                    else
                    {
                        _waitingForHeader = false;
                    }
                }
                else
                {
                    Debug.Assert(currentPacket != null, nameof(currentPacket) + " != null");
                    currentPacket.Data = new byte[currentPacket.DataLength];
                    _socket.Receive(currentPacket.Data);
                    OnPacket(currentPacket); 
                    //Toggle back for header handling
                    _waitingForHeader = true;
                    if (singleRun) break;
                }
            }
            return currentPacket;
        }


        public class Factory
        {
            public static async Task<RemoteAdbDevice> Create(string host, uint port, bool verboseMode = false)
            {
                var remotedev = new RemoteAdbDevice();
                remotedev.PrintMessages = verboseMode;
                //var cmdOpen = Command.CreateOpenCommand();

                //adbClient.Connect(endpoint);
                //adbClient.SetDevice() 
                var endpoint = new IPEndPoint(Dns.Resolve(host).AddressList[0], (int)port);
                return await remotedev.Connect(endpoint);
            }
        }

        public void Disconnect()
        {
            OpenStreams.Clear();
            _socket.Dispose();
        }
    }
}