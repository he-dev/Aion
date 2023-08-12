// See https://aka.ms/new-console-template for more information

namespace DummyApp;

internal static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine($"Hello {args[1]}!");
        Thread.Sleep(int.Parse(args[2]));
        return int.Parse(args[3]);
    }
}