using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StatsdClient
{
    public class NullStatsd : IStatsd, IStatsdBatch
    {
#if NET45
        private static readonly Task CompletedTask = Task.FromResult<object>(null);
#else
        private static readonly Task CompletedTask = Task.CompletedTask;
#endif
        public void Send<TCommandType>(ReadOnlySpan<char> name, int value) where TCommandType : IAllowsInteger
        {
        }

        public IReadOnlyList<ReadOnlyMemory<char>> Commands => new ReadOnlyMemory<char>[0];

        public void Add<TCommandType>(ReadOnlySpan<char> name, int value) where TCommandType : IAllowsInteger
        {
        }

        public void Send<TCommandType>(ReadOnlySpan<char> name, double value) where TCommandType : IAllowsDouble
        {
        }

        public void Add<TCommandType>(ReadOnlySpan<char> name, double value) where TCommandType : IAllowsDouble
        {
        }

        public void Send<TCommandType>(ReadOnlySpan<char> name, int value, double sampleRate)
            where TCommandType : IAllowsInteger, IAllowsSampleRate
        {
        }

        public void Add<TCommandType>(ReadOnlySpan<char> name, int value, double sampleRate)
            where TCommandType : IAllowsInteger, IAllowsSampleRate
        {
        }

        public void Send<TCommandType>(ReadOnlySpan<char> name, ReadOnlySpan<char> value) where TCommandType : IAllowsString
        {
        }

        public void Send()
        {
        }

        public void Add(Action actionToTime, ReadOnlySpan<char> statName, double sampleRate = 1)
        {
            actionToTime();
        }

        public void Send(Action actionToTime, ReadOnlySpan<char> statName, double sampleRate = 1)
        {
            actionToTime();
        }

        public Task SendAsync<TCommandType>(ReadOnlySpan<char> name, int value) where TCommandType : IAllowsInteger
        {
            return CompletedTask;
        }

        public Task SendAsync<TCommandType>(ReadOnlySpan<char> name, double value) where TCommandType : IAllowsDouble
        {
            return CompletedTask;
        }

        public Task SendAsync<TCommandType>(ReadOnlySpan<char> name, double value, bool isDeltaValue) where TCommandType : IAllowsDouble, IAllowsDelta
        {
            return CompletedTask;
        }

        public Task SendAsync<TCommandType>(ReadOnlySpan<char> name, int value, double sampleRate) where TCommandType : IAllowsInteger, IAllowsSampleRate
        {
            return CompletedTask;
        }

        public Task SendAsync<TCommandType>(ReadOnlySpan<char> name, ReadOnlySpan<char> value) where TCommandType : IAllowsString
        {
            return CompletedTask;
        }

        public Task SendAsync()
        {
            return CompletedTask;
        }

        public Task AddAsync(Func<Task> actionToTime, ReadOnlyMemory<char> statName, double sampleRate = 1)
        {
            return actionToTime();
        }

        public Task SendAsync(Func<Task> actionToTime, ReadOnlyMemory<char> statName, double sampleRate = 1)
        {
            return actionToTime();
        }

        public void Send<TCommandType>(ReadOnlySpan<char> name, double value, bool isDeltaValue) where TCommandType : IAllowsDouble, IAllowsDelta
        {
        }

        public IStatsdBatch CreateBatch() => this;
    }
}
