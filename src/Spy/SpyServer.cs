using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using StreamJsonRpc;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace MigrationToolkit.Spy
{
    /// <summary>
    /// Reverse-connection Spy server. The UWP app connects OUT to an external
    /// TCP listener (TestClient, MCP Server, FlowRunner) because UWP AppContainer
    /// blocks inbound loopback connections.
    ///
    /// Flow:
    /// 1. External tool starts a TcpListener on localhost:54321
    /// 2. SpyServer connects out to it (outbound loopback is allowed)
    /// 3. StreamJsonRpc attaches — Spy provides ISpyService methods
    /// 4. On disconnect, SpyServer retries every 3 seconds
    /// </summary>
    public static class SpyServer
    {
        public const int DefaultPort = 54321;
        private const int ReconnectDelayMs = 3000;

        private static CoreDispatcher? s_dispatcher;
        private static int s_port = DefaultPort;

        public static int Port => s_port;

        /// <summary>
        /// Start the spy connector. Must be called from the UI thread.
        /// </summary>
        public static void Start(int? port = null)
        {
            s_dispatcher = Window.Current?.Dispatcher
                ?? throw new InvalidOperationException(
                    "SpyServer.Start() must be called from the UI thread after Window.Current is available.");

            if (port.HasValue)
                s_port = port.Value;

            Debug.WriteLine($"SpyServer: will connect to tcp://localhost:{s_port} (reverse connection mode)");

            _ = Task.Run(ConnectLoopAsync);
        }

        private static async Task ConnectLoopAsync()
        {
            while (true)
            {
                StreamSocket? socket = null;
                JsonRpc? rpc = null;

                try
                {
                    socket = new StreamSocket();
                    Debug.WriteLine($"SpyServer: connecting to tcp://localhost:{s_port}...");

                    await socket.ConnectAsync(
                        new HostName("127.0.0.1"),
                        s_port.ToString());

                    Debug.WriteLine($"SpyServer: connected to tcp://localhost:{s_port}");

                    var inputStream = socket.InputStream.AsStreamForRead();
                    var outputStream = socket.OutputStream.AsStreamForWrite();
                    var duplexStream = new DuplexStream(inputStream, outputStream);

                    var service = new SpyService(s_dispatcher!);

                    // Increase MaxDepth — XF on UWP produces deep visual trees
                    // that exceed Newtonsoft.Json's default MaxDepth of 64
                    var formatter = new JsonMessageFormatter();
                    formatter.JsonSerializer.MaxDepth = 512;
                    var msgHandler = new HeaderDelimitedMessageHandler(
                        duplexStream, duplexStream, formatter);
                    rpc = new JsonRpc(msgHandler, service);
                    rpc.StartListening();

                    // Block until the client disconnects
                    await rpc.Completion;
                    Debug.WriteLine("SpyServer: client disconnected.");
                }
                catch (Exception ex)
                {
                    // Connection refused = external tool not running yet. Normal.
                    Debug.WriteLine($"SpyServer: {ex.Message}");
                }
                finally
                {
                    rpc?.Dispose();
                    try { socket?.Dispose(); }
                    catch { /* best-effort cleanup */ }
                }

                // Wait before retrying
                await Task.Delay(ReconnectDelayMs);
            }
        }
    }

    /// <summary>
    /// Combines separate read/write streams into a single duplex Stream
    /// for StreamJsonRpc which expects one bidirectional stream.
    /// </summary>
    internal class DuplexStream : Stream
    {
        private readonly Stream _read;
        private readonly Stream _write;

        public DuplexStream(Stream readStream, Stream writeStream)
        {
            _read = readStream;
            _write = writeStream;
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => _read.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count,
            System.Threading.CancellationToken cancellationToken)
            => _read.ReadAsync(buffer, offset, count, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count)
            => _write.Write(buffer, offset, count);

        public override Task WriteAsync(byte[] buffer, int offset, int count,
            System.Threading.CancellationToken cancellationToken)
            => _write.WriteAsync(buffer, offset, count, cancellationToken);

        public override void Flush() => _write.Flush();

        public override Task FlushAsync(System.Threading.CancellationToken cancellationToken)
            => _write.FlushAsync(cancellationToken);

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _read.Dispose();
                _write.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
