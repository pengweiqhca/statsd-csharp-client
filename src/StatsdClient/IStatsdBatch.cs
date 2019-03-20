using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StatsdClient
{
    public interface IStatsdBatch
    {
        IReadOnlyList<ReadOnlyMemory<char>> Commands { get; }

        void Add<TCommandType>(ReadOnlySpan<char> name, int value) where TCommandType : IAllowsInteger;
        void Add<TCommandType>(ReadOnlySpan<char> name, double value) where TCommandType : IAllowsDouble;
        void Add<TCommandType>(ReadOnlySpan<char> name, int value, double sampleRate) where TCommandType : IAllowsInteger, IAllowsSampleRate;
        void Add(Action actionToTime, ReadOnlySpan<char> statName, double sampleRate = 1);
        Task AddAsync(Func<Task> actionToTime, ReadOnlyMemory<char> statName, double sampleRate = 1);

        void Send();
        Task SendAsync();
    }

    public static class StatsdBatchExtensions
    {
        public static void Add<TCommandType>(this IStatsdBatch batch, string name, int value) where TCommandType : IAllowsInteger =>
            batch.Add<TCommandType>(name.AsSpan(), value);
        public static void Add<TCommandType>(this IStatsdBatch batch, string name, double value) where TCommandType : IAllowsDouble =>
            batch.Add<TCommandType>(name.AsSpan(), value);
        public static void Add<TCommandType>(this IStatsdBatch batch, string name, int value, double sampleRate) where TCommandType : IAllowsInteger, IAllowsSampleRate =>
            batch.Add<TCommandType>(name.AsSpan(), value, sampleRate);
        public static void Add(this IStatsdBatch batch, Action actionToTime, string statName, double sampleRate = 1) =>
            batch.Add(actionToTime, statName.AsSpan(), sampleRate);
        public static Task AddAsync(this IStatsdBatch batch, Func<Task> actionToTime, string statName, double sampleRate = 1) =>
            batch.AddAsync(actionToTime, statName.AsMemory(), sampleRate);
    }
}
