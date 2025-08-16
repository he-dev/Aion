using System;
using System.Threading.Tasks;

namespace Aion.Util.Services;

public static class ExtensionChaining
{
    public static async Task<TResult> Let<T, TResult>(this Task<T> task, Func<T, TResult> block)
    {
        var value = await task;
        return block(value); // sync transform
    }

    public static async Task<TResult> Let<T, TResult>(this T value, Func<T, Task<TResult>> block)
    {
        return await block(value); // async transform
    }

    public static async Task<TResult> Let<T, TResult>(this Task<T> task, Func<T, Task<TResult>> block)
    {
        var value = await task;
        return await block(value); // async transform
    }

    public static async Task<T> Also<T>(this Task<T> task, Action<T> block)
    {
        var value = await task;
        block(value); // sync side effect
        return value;
    }

    public static async Task<T> Also<T>(this Task<T> task, Func<T, Task> block)
    {
        var value = await task;
        await block(value); // async side effect
        return value;
    }
}