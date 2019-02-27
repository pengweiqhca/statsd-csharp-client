using StatsdClient;
using System;
using System.Threading.Tasks;

namespace Tests
{
    public static class StatsdClientExtensions
    {
        public static void Send(this IStatsdClient client, string command)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            client.Send(command.AsSpan());
        }

        public static Task SendAsync(this IStatsdClient client, string command)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));

            return client.SendAsync(command.AsSpan());
        }
    }
}
