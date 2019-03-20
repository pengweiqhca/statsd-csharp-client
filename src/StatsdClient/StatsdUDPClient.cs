using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace StatsdClient
{
    public class StatsdUDPClient : IStatsdClient
    {
        private readonly Task<IPEndPoint> _ipEndpoint;
        private readonly int _maxUdpPacketSizeBytes;
        private readonly Socket _clientSocket;
        private readonly Encoding _encoding;

        /// <summary>
        /// Creates a new StatsdUDP class for lower level access to statsd.
        /// </summary>
        /// <param name="name">Hostname or IP (v4) address of the statsd server.</param>
        /// <param name="port">Port of the statsd server. Default is 8125.</param>
        /// <param name="maxUdpPacketSizeBytes">Max packet size, in bytes. This is useful to tweak if your MTU size is different than normal. Set to 0 for no limit. Default is MetricsConfig.DefaultStatsdMaxUDPPacketSize.</param>
        public StatsdUDPClient(string name, int port = 8125, int maxUdpPacketSizeBytes = MetricsConfig.DefaultStatsdMaxUDPPacketSize)
            : this(Encoding.UTF8, name, port, maxUdpPacketSizeBytes) { }

        /// <summary>
        /// Creates a new StatsdUDP class for lower level access to statsd.
        /// </summary>
        /// <param name="encoding">message encoding</param>
        /// <param name="name">Hostname or IP (v4) address of the statsd server.</param>
        /// <param name="port">Port of the statsd server. Default is 8125.</param>
        /// <param name="maxUdpPacketSizeBytes">Max packet size, in bytes. This is useful to tweak if your MTU size is different than normal. Set to 0 for no limit. Default is MetricsConfig.DefaultStatsdMaxUDPPacketSize.</param>
        public StatsdUDPClient(Encoding encoding, string name, int port = 8125, int maxUdpPacketSizeBytes = MetricsConfig.DefaultStatsdMaxUDPPacketSize)
        {
            _encoding = encoding ?? Encoding.UTF8;
            _maxUdpPacketSizeBytes = maxUdpPacketSizeBytes;

            _clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            _ipEndpoint = AddressResolution.GetIpv4EndPoint(name, port);
        }
#if DEBUG
        public Task SendAsync(string command) => SendAsync(command.AsSpan());
#endif
        public Task SendAsync(ReadOnlySpan<char> command) => SendAsync(GetBytes(command, out var count), count);

        public async Task SendAsync(IMemoryOwner<byte> owner, int count)
        {
            using (owner)
                await SendAsync(owner.Memory.Slice(0, count)).ConfigureAwait(false);
        }

        private IMemoryOwner<byte> GetBytes(ReadOnlySpan<char> command, out int count)
        {
            var owner = MemoryPool<byte>.Shared.Rent(count = _encoding.GetByteCount(command));

            count = _encoding.GetBytes(command, owner.Memory.Span);

            return owner;
        }

        private async Task SendAsync(ReadOnlyMemory<byte> encodedCommand)
        {
            if (_maxUdpPacketSizeBytes > 0 && encodedCommand.Length > _maxUdpPacketSizeBytes)
            {
                // If the command is too big to send, linear search backwards from the maximum
                // packet size to see if we can find a newline delimiting two stats. If we can,
                // split the message across the newline and try sending both componenets individually
                for (var i = _maxUdpPacketSizeBytes; i > 0; i--)
                {
                    if (encodedCommand.Span[i] != '\n') continue;

                    await SendAsync(encodedCommand.Slice(0, i)).ConfigureAwait(false);

                    if (encodedCommand.Length - i > 1)
                        await SendAsync(encodedCommand.Slice(i + 1)).ConfigureAwait(false);

                    return;
                }
            }

            if (MemoryMarshal.TryGetArray(encodedCommand, out var data))
                await _clientSocket.SendToAsync(data, SocketFlags.None, await _ipEndpoint.ConfigureAwait(false)).ConfigureAwait(false);
        }

        //reference : https://lostechies.com/chrispatterson/2012/11/29/idisposable-done-right/
        private bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~StatsdUDPClient()
        {
            // Finalizer calls Dispose(false)
            Dispose(false);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_clientSocket != null)
                {
                    try
                    {
#if NETFRAMEWORK
                        _clientSocket.Close();
#else
                        _clientSocket.Dispose();
#endif
                    }
                    catch (Exception)
                    {
                        //Swallow since we are not using a logger, should we add LibLog and start logging??
                    }

                }
            }

            _disposed = true;
        }
    }
}
#if NET45
namespace System.Net.Sockets
{
    public static class SocketTaskExtensions
    {
        public static Task<int> SendToAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags, EndPoint remoteEP)
        {
            return Task.Factory.FromAsync((arg1, arg2, arg3, callback, state) => socket.BeginSendTo(arg1.Array, arg1.Offset, arg1.Count, arg2, arg3, callback, state),
                socket.EndSendTo, buffer, socketFlags, remoteEP, null);
        }
    }
}
#endif
#if !NETSTANDARD2_1
namespace System.Text
{
    public static class EncodingExtensions
    {
        public static unsafe int GetByteCount(this Encoding encoding, ReadOnlySpan<char> data)
        {
            fixed (char* chars = &data.GetPinnableReference())
                return encoding.GetByteCount(chars, data.Length);
        }

        public static unsafe int GetBytes(this Encoding encoding, ReadOnlySpan<char> data, Span<byte> span)
        {
            fixed (char* chars = &data.GetPinnableReference())
            fixed (byte* bytes = &span.GetPinnableReference())
                return encoding.GetBytes(chars, data.Length, bytes, span.Length);
        }
    }
}
#endif
