using System;
using System.Threading.Tasks;

namespace StatsdClient
{
#if DEBUG
    public interface IStatsdClient : IDisposable
    {
        Task SendAsync(string command);
    }

    public static class StatsdClientExtensions
    {
        public static void Send(this IStatsdClient client, ReadOnlySpan<char> command)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            client.SendAsync(command.ToString()).GetAwaiter().GetResult();
        }

        public static Task SendAsync(this IStatsdClient client,  ReadOnlySpan<char> command)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            return client.SendAsync(command.ToString());
        }
    }
#else
    public interface IStatsdClient : IDisposable
    {
        Task SendAsync(ReadOnlySpan<char> command);
    }

    public static class StatsdClientExtensions
    {
        public static void Send(this IStatsdClient client, string command)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            client.SendAsync(command.AsSpan()).GetAwaiter().GetResult();;
        }

        public static Task SendAsync(this IStatsdClient client, string command)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            return client.SendAsync(command.AsSpan());
        }
    }
#endif
}
