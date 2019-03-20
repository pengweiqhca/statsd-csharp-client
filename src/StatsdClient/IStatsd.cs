using System;
using System.Buffers;
using System.Globalization;
using System.Threading.Tasks;

namespace StatsdClient
{
    public interface IStatsd
    {
        IStatsdBatch CreateBatch();

        void Send<TCommandType>(ReadOnlySpan<char> name, int value) where TCommandType : IAllowsInteger;
        Task SendAsync<TCommandType>(ReadOnlySpan<char> name, int value) where TCommandType : IAllowsInteger;

        void Send<TCommandType>(ReadOnlySpan<char> name, double value) where TCommandType : IAllowsDouble;
        Task SendAsync<TCommandType>(ReadOnlySpan<char> name, double value) where TCommandType : IAllowsDouble;

        void Send<TCommandType>(ReadOnlySpan<char> name, double value, bool isDeltaValue) where TCommandType : IAllowsDouble, IAllowsDelta;
        Task SendAsync<TCommandType>(ReadOnlySpan<char> name, double value, bool isDeltaValue) where TCommandType : IAllowsDouble, IAllowsDelta;

        void Send<TCommandType>(ReadOnlySpan<char> name, int value, double sampleRate) where TCommandType : IAllowsInteger, IAllowsSampleRate;
        Task SendAsync<TCommandType>(ReadOnlySpan<char> name, int value, double sampleRate) where TCommandType : IAllowsInteger, IAllowsSampleRate;

        void Send<TCommandType>(ReadOnlySpan<char> name, ReadOnlySpan<char> value) where TCommandType : IAllowsString;
        Task SendAsync<TCommandType>(ReadOnlySpan<char> name, ReadOnlySpan<char> value) where TCommandType : IAllowsString;

        void Send(Action actionToTime, ReadOnlySpan<char> statName, double sampleRate = 1);
        Task SendAsync(Func<Task> actionToTime, ReadOnlyMemory<char> statName, double sampleRate = 1);
    }

    public static class StatsdExtensions
    {
        public static void Send<TCommandType>(this IStatsd statsd, string name, int value) where TCommandType : IAllowsInteger =>
            statsd.Send<TCommandType>(name.AsSpan(), value);
        public static Task SendAsync<TCommandType>(this IStatsd statsd, string name, int value) where TCommandType : IAllowsInteger =>
            statsd.SendAsync<TCommandType>(name.AsSpan(), value);

        public static void Send<TCommandType>(this IStatsd statsd, string name, double value) where TCommandType : IAllowsDouble =>
            statsd.Send<TCommandType>(name.AsSpan(), value);

        public static Task SendAsync<TCommandType>(this IStatsd statsd, string name, double value) where TCommandType : IAllowsDouble =>
            statsd.SendAsync<TCommandType>(name.AsSpan(), value);

        public static void Send<TCommandType>(this IStatsd statsd, string name, double value, bool isDeltaValue) where TCommandType : IAllowsDouble, IAllowsDelta =>
            statsd.Send<TCommandType>(name.AsSpan(), value, isDeltaValue);
        public static Task SendAsync<TCommandType>(this IStatsd statsd, string name, double value, bool isDeltaValue) where TCommandType : IAllowsDouble, IAllowsDelta =>
            statsd.SendAsync<TCommandType>(name.AsSpan(), value, isDeltaValue);

        public static void Send<TCommandType>(this IStatsd statsd, string name, int value, double sampleRate) where TCommandType : IAllowsInteger, IAllowsSampleRate =>
            statsd.SendAsync<TCommandType>(name.AsSpan(), value, sampleRate);
        public static Task SendAsync<TCommandType>(this IStatsd statsd, string name, int value, double sampleRate) where TCommandType : IAllowsInteger, IAllowsSampleRate =>
            statsd.SendAsync<TCommandType>(name.AsSpan(), value, sampleRate);

        public static void Send<TCommandType>(this IStatsd statsd, string name, string value) where TCommandType : IAllowsString =>
            statsd.SendAsync<TCommandType>(name.AsSpan(), value.AsSpan());
        public static Task SendAsync<TCommandType>(this IStatsd statsd, string name, string value) where TCommandType : IAllowsString =>
            statsd.SendAsync<TCommandType>(name.AsSpan(), value.AsSpan());

        public static void Send(this IStatsd statsd, Action actionToTime, string statName, double sampleRate = 1) =>
            statsd.Send(actionToTime, statName.AsSpan(), sampleRate);
        public static Task SendAsync(this IStatsd statsd, Func<Task> actionToTime, string statName, double sampleRate = 1) =>
            statsd.SendAsync(actionToTime, statName.AsMemory(), sampleRate);
    }
}
