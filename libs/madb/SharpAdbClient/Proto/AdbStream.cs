using System;
using System.Net.Sockets;
using System.Threading.Tasks.Dataflow;

namespace SharpAdbClient.Proto
{
    public class AdbStream
    {
        public uint LocalId { get; set; }
        public uint RemoteId { get; set; }
        public IPropagatorBlock<byte[], byte[]> Block { get; private set; }
        private TcpSocket _sock;
        private BufferBlock<byte[]> _outputBlock;
        public event EventHandler<EventArgs> OnWriting;

        public AdbStream(TcpSocket sock, uint localId)
        {
            _sock = sock;
            this.LocalId = localId;

            _outputBlock = new BufferBlock<byte[]>();
            var inputBlock = new ActionBlock<byte[]>(bytes =>
            {
                var buff = Command.CreateWriteCommand(LocalId, RemoteId, bytes);
                OnWriting?.Invoke(this, EventArgs.Empty);
                if (RemoteId == 0)
                {
                    throw new Exception("Remote stream not established yet!");
                }
                _sock.Send(buff);
            });
            Block = DataflowBlock.Encapsulate(inputBlock, _outputBlock);
        }

        public void HandlePacket(AdbPacket newPacket)
        {
            if (RemoteId == 0)
            {
                RemoteId = newPacket.arg1;
            }
            var okay = Command.CreateOkCommand(LocalId, RemoteId);
            _sock.Send(okay);
            _outputBlock.Post(newPacket.Data);

        }
    }
}