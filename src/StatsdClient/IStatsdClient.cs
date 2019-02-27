using System;
using System.Threading.Tasks;

namespace StatsdClient
{
    public interface IStatsdClient : IDisposable
    {
        void Send(ReadOnlySpan<char> command);
        Task SendAsync(ReadOnlySpan<char> command);
    }
}
