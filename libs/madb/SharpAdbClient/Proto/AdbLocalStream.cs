using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using SharpAdbClient.Exceptions;

namespace SharpAdbClient.Proto
{
    public class AdbLocalStream
    {
        public uint LocalId { get; set; }
        public uint RemoteId { get; set; }
        public IPropagatorBlock<byte[], byte[]> Block { get; private set; }
        private IAdbSocket _sock;
        private BufferBlock<byte[]> _outputBlock;
        private ActionBlock<byte[]> _inputBlock;
        private Stream _oStream;
        public event EventHandler<EventArgs> OnWriting;

        public AdbLocalStream(IAdbSocket sock, uint localId, Encoding encoding, CancellationToken ct)
        {
            _sock = sock;
            _oStream = sock.GetShellStream();
            _outputBlock = new BufferBlock<byte[]>();
            _inputBlock = new ActionBlock<byte[]>(bytes =>
            {
                var buff = Command.CreateWriteCommand(LocalId, RemoteId, bytes);
                OnWriting?.Invoke(this, EventArgs.Empty);
                if (RemoteId == 0)
                {
                    throw new Exception("Remote stream not established yet!");
                }
                _sock.Send(buff, buff.Length);
            });
            Block = DataflowBlock.Encapsulate(_inputBlock, _outputBlock);

            HandleData(encoding, ct).ConfigureAwait(false);
        }

        private async Task HandleData(Encoding encoding, CancellationToken ct)
        {
            try
            {
                using (StreamReader reader = new StreamReader(_sock.GetShellStream(), encoding))
                {
                    // Previously, we would loop while reader.Peek() >= 0. Turns out that this would
                    // break too soon in certain cases (about every 10 loops, so it appears to be a timing
                    // issue). Checking for reader.ReadLine() to return null appears to be much more robust
                    // -- one of the integration test fetches output 1000 times and found no truncations.
                    while (!ct.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync().ConfigureAwait(false);

                        if (line == null)
                        {
                            break;
                        }
                        _outputBlock.Post(encoding.GetBytes(line));
                    }
                }
            }
            catch (Exception e)
            {
                // If a cancellation was requested, this main loop is interrupted with an exception
                // because the socket is closed. In that case, we don't need to throw a ShellCommandUnresponsiveException.
                // In all other cases, something went wrong, and we want to report it to the user.
                if (!ct.IsCancellationRequested)
                {
                    throw new ShellCommandUnresponsiveException(e);
                }
            }
        }

        public void Close()
        {
            if (Block != null)
            {
                Block.Complete();
            }
            _outputBlock.Complete();
            _inputBlock.Complete();
            LocalId = 0;
            RemoteId = 0;
            _sock = null;
        }
    }
}