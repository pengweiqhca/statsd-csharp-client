using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StatsdClient
{
    public interface IAllowsSampleRate { }
    public interface IAllowsDelta { }

    public interface IAllowsDouble { }
    public interface IAllowsInteger { }
    public interface IAllowsString { }

    public class Statsd : IStatsd
    {
#if NET45
        private static readonly Task CompletedTask = Task.FromResult<object>(null);
#else
        private static readonly Task CompletedTask = Task.CompletedTask;
#endif

        private IStopWatchFactory StopwatchFactory { get; }
        private IStatsdClient StatsdClient { get; }
        private IRandomGenerator RandomGenerator { get; }

        private readonly ReadOnlyMemory<char> _prefix;

        public class Counting : IAllowsSampleRate, IAllowsInteger { }
        public class Timing : IAllowsSampleRate, IAllowsInteger { }
        public class Gauge : IAllowsDouble, IAllowsDelta { }
        public class Histogram : IAllowsInteger { }
        public class Meter : IAllowsInteger { }
        public class Set : IAllowsString { }

        private readonly IDictionary<Type, ReadOnlyMemory<char>> _commandToUnit =
            new Dictionary<Type, ReadOnlyMemory<char>>
            {
                {typeof(Counting), new ReadOnlyMemory<char>(new[] {'c'})},
                {typeof(Timing), new ReadOnlyMemory<char>(new[] {'m', 's'})},
                {typeof(Gauge), new ReadOnlyMemory<char>(new[] {'g'})},
                {typeof(Histogram), new ReadOnlyMemory<char>(new[] {'h'})},
                {typeof(Meter), new ReadOnlyMemory<char>(new[] {'m'})},
                {typeof(Set), new ReadOnlyMemory<char>(new[] {'s'})}
            };

        public Statsd(IStatsdClient statsdClient, IRandomGenerator randomGenerator, IStopWatchFactory stopwatchFactory, ReadOnlyMemory<char> prefix)
        {
            StopwatchFactory = stopwatchFactory;
            StatsdClient = statsdClient;
            RandomGenerator = randomGenerator;
            _prefix = prefix;
        }

        public Statsd(IStatsdClient statsdClient, IRandomGenerator randomGenerator, IStopWatchFactory stopwatchFactory)
            : this(statsdClient, randomGenerator, stopwatchFactory, ReadOnlyMemory<char>.Empty) { }

        public Statsd(IStatsdClient statsdClient)
            : this(statsdClient, new RandomGenerator(), new StopWatchFactory(), ReadOnlyMemory<char>.Empty) { }

        public IStatsdBatch CreateBatch() => new StatsdBatch(this);

        public void Send<TCommandType>(ReadOnlySpan<char> name, int value) where TCommandType : IAllowsInteger =>
            SendAsync<TCommandType>(name, value).GetAwaiter().GetResult();

        public Task SendAsync<TCommandType>(ReadOnlySpan<char> name, int value) where TCommandType : IAllowsInteger =>
            SendSingleAsync(GetCommand(_prefix.Span, name, value.ToString(CultureInfo.InvariantCulture), _commandToUnit[typeof(TCommandType)], 1));

        public void Send<TCommandType>(ReadOnlySpan<char> name, double value) where TCommandType : IAllowsDouble =>
            SendAsync<TCommandType>(name, value).GetAwaiter().GetResult();

        public Task SendAsync<TCommandType>(ReadOnlySpan<char> name, double value) where TCommandType : IAllowsDouble
        {
            var formattedValue = string.Format(CultureInfo.InvariantCulture, "{0:F15}", value);

            return SendSingleAsync(GetCommand(_prefix.Span, name, formattedValue, _commandToUnit[typeof(TCommandType)], 1));
        }

        public void Send<TCommandType>(ReadOnlySpan<char> name, double value, bool isDeltaValue) where TCommandType : IAllowsDouble, IAllowsDelta =>
            SendAsync<TCommandType>(name, value, isDeltaValue).GetAwaiter().GetResult();

        public Task SendAsync<TCommandType>(ReadOnlySpan<char> name, double value, bool isDeltaValue) where TCommandType : IAllowsDouble, IAllowsDelta
        {
            if (isDeltaValue)
            {
                // Sending delta values to StatsD requires a value modifier sign (+ or -) which we append
                // using this custom format with a different formatting rule for negative/positive and zero values
                // https://msdn.microsoft.com/en-us/library/0c899ak8.aspx#SectionSeparator

                return SendSingleAsync(GetCommand(_prefix.Span, name, string.Format(CultureInfo.InvariantCulture, "{0:+#.###;-#.###;+0}", value), _commandToUnit[typeof(TCommandType)], 1));
            }

            return SendAsync<TCommandType>(name, value);
        }

        public void Send<TCommandType>(ReadOnlySpan<char> name, ReadOnlySpan<char> value) where TCommandType : IAllowsString =>
            SendAsync<TCommandType>(name, value).GetAwaiter().GetResult();

        public Task SendAsync<TCommandType>(ReadOnlySpan<char> name, ReadOnlySpan<char> value) where TCommandType : IAllowsString =>
            SendSingleAsync(GetCommand(_prefix.Span, name, value, _commandToUnit[typeof(TCommandType)], 1));

        public void Send<TCommandType>(ReadOnlySpan<char> name, int value, double sampleRate) where TCommandType : IAllowsInteger, IAllowsSampleRate =>
            SendAsync<TCommandType>(name, value, sampleRate).GetAwaiter().GetResult();

        public Task SendAsync<TCommandType>(ReadOnlySpan<char> name, int value, double sampleRate) where TCommandType : IAllowsInteger, IAllowsSampleRate =>
            RandomGenerator.ShouldSend(sampleRate)
                ? SendSingleAsync(GetCommand(_prefix.Span, name, value.ToString(CultureInfo.InvariantCulture), _commandToUnit[typeof(TCommandType)], sampleRate))
                : CompletedTask;

        public void Send(Action actionToTime, ReadOnlySpan<char> statName, double sampleRate = 1)
        {
            var stopwatch = StopwatchFactory.Get();

            try
            {
                stopwatch.Start();
                actionToTime();
            }
            finally
            {
                stopwatch.Stop();

                if (RandomGenerator.ShouldSend(sampleRate))
                    Send<Timing>(statName, stopwatch.ElapsedMilliseconds);
            }
        }

        public async Task SendAsync(Func<Task> actionToTime, ReadOnlyMemory<char> statName, double sampleRate = 1)
        {
            var stopwatch = StopwatchFactory.Get();

            try
            {
                stopwatch.Start();
                await actionToTime().ConfigureAwait(false);
            }
            finally
            {
                stopwatch.Stop();

                if (RandomGenerator.ShouldSend(sampleRate))
                    await SendAsync<Timing>(statName.Span, stopwatch.ElapsedMilliseconds).ConfigureAwait(false);
            }
        }

        private async Task SendSingleAsync(MemoryString command)
        {
            try
            {
                await StatsdClient.SendAsync(command.Memory.Span).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            finally
            {
                command.Dispose();
            }
        }

        private static MemoryString GetCommand(ReadOnlySpan<char> prefix, ReadOnlySpan<char> name, string value, ReadOnlyMemory<char> unit, double sampleRate) =>
            GetCommand(prefix, name, value.AsSpan(), unit, sampleRate);

        private static MemoryString GetCommand(ReadOnlySpan<char> prefix, ReadOnlySpan<char> name, ReadOnlySpan<char> value, ReadOnlyMemory<char> unit, double sampleRate)
        {
            var rate = Math.Abs(sampleRate - 1) < 0.00000001 ? ReadOnlyMemory<char>.Empty : sampleRate.ToString(CultureInfo.InvariantCulture).AsMemory();

            var array = ArrayPool<char>.Shared.Rent(prefix.Length + name.Length + 1 + value.Length + 1 + unit.Length + (rate.IsEmpty ? 0 : rate.Length + 2));
            var memory = new Memory<char>(array);

            int length;
            if (prefix.IsEmpty)
            {
                name.CopyTo(memory.Span);
                length = name.Length;
            }
            else
            {
                prefix.CopyTo(memory.Span);
                name.CopyTo(memory.Slice(prefix.Length).Span);
                length = prefix.Length + name.Length;
            }

            memory.Slice(length++, 1).Span.Fill(':');
            value.CopyTo(memory.Slice(length, value.Length).Span);
            length += value.Length;
            memory.Slice(length++, 1).Span.Fill('|');
            unit.CopyTo(memory.Slice(length, unit.Length));
            length += unit.Length;

            if (rate.IsEmpty) return new MemoryString(array, length);

            memory.Slice(length++, 1).Span.Fill('|');
            memory.Slice(length++, 1).Span.Fill('@');
            rate.CopyTo(memory.Slice(length, rate.Length));
            length += rate.Length;

            return new MemoryString(array, length);
        }

        private class MemoryString : IDisposable
        {
            private readonly char[] _data;
            public ReadOnlyMemory<char> Memory { get; }

            public MemoryString(char[] data, int length)
            {
                _data = data;
                Memory = new ReadOnlyMemory<char>(data, 0, length);
            }

            public void Dispose() => ArrayPool<char>.Shared.Return(_data);
        }

        private class StatsdBatch : IStatsdBatch
        {
            private readonly Statsd _statsd;
            private ConcurrentQueue<MemoryString> _commands;

            public IReadOnlyList<ReadOnlyMemory<char>> Commands => _commands.Select(m => m.Memory).ToArray();

            public StatsdBatch(Statsd statsd)
            {
                _statsd = statsd;

                _commands = new ConcurrentQueue<MemoryString>();
            }

            public void Add<TCommandType>(ReadOnlySpan<char> name, int value) where TCommandType : IAllowsInteger =>
                _commands.Enqueue(GetCommand(_statsd._prefix.Span, name, value.ToString(CultureInfo.InvariantCulture), _statsd._commandToUnit[typeof(TCommandType)], 1));

            public void Add<TCommandType>(ReadOnlySpan<char> name, double value) where TCommandType : IAllowsDouble =>
                _commands.Enqueue(GetCommand(_statsd._prefix.Span, name, string.Format(CultureInfo.InvariantCulture, "{0:F15}", value), _statsd._commandToUnit[typeof(TCommandType)], 1));

            public void Add<TCommandType>(ReadOnlySpan<char> name, int value, double sampleRate) where TCommandType : IAllowsInteger, IAllowsSampleRate
            {
                if (_statsd.RandomGenerator.ShouldSend(sampleRate))
                    _commands.Enqueue(GetCommand(_statsd._prefix.Span, name, value.ToString(CultureInfo.InvariantCulture), _statsd._commandToUnit[typeof(TCommandType)], sampleRate));
            }

            public void Add(Action actionToTime, ReadOnlySpan<char> statName, double sampleRate = 1)
            {
                var stopwatch = _statsd.StopwatchFactory.Get();

                try
                {
                    stopwatch.Start();
                    actionToTime();
                }
                finally
                {
                    stopwatch.Stop();

                    if (_statsd.RandomGenerator.ShouldSend(sampleRate))
                        Add<Timing>(statName, stopwatch.ElapsedMilliseconds);
                }
            }

            public async Task AddAsync(Func<Task> actionToTime, ReadOnlyMemory<char> statName, double sampleRate = 1)
            {
                var stopwatch = _statsd.StopwatchFactory.Get();

                try
                {
                    stopwatch.Start();
                    await actionToTime().ConfigureAwait(false);
                }
                finally
                {
                    stopwatch.Stop();

                    if (_statsd.RandomGenerator.ShouldSend(sampleRate))
                        Add<Timing>(statName.Span, stopwatch.ElapsedMilliseconds);
                }
            }

            public void Send() => SendAsync().GetAwaiter().GetResult();

            public async Task SendAsync()
            {
                try
                {
                    var commands = Interlocked.Exchange(ref _commands, new ConcurrentQueue<MemoryString>()).ToArray();

                    using (var array = MemoryPool<char>.Shared.Rent(commands.Sum(c => c.Memory.Length + 1)))
                    {
                        var index = 0;
                        foreach (var command in commands)
                            using (command)
                            {
                                command.Memory.CopyTo(array.Memory.Slice(index));
                                index += command.Memory.Length + 1;

                                array.Memory.Span.Slice(index - 1, 1).Fill('\n');
                            }

                        await _statsd.StatsdClient.SendAsync(array.Memory.Span.Slice(0, index - 1)).ConfigureAwait(false);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
        }
    }
}
