using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aion.Util;

public interface IDirectoryTree : IEnumerable<DirectoryTree.Branch>
{
    string Path { get; }
}

public class DirectoryTree(string path) : IDirectoryTree
{
    public string Path { get; } = Directory.Exists(path) ? path : throw new DirectoryNotFoundException($"Directory '{path}' not found.");

    public IEnumerator<Branch> GetEnumerator()
    {
        var branches = new Queue<Branch> { new Branch(path, path) };

        foreach (var branch in branches.Consume())
        {
            if (branch.Path == path)
            {
                yield return branch;
            }

            foreach (var directory in branch.Directories())
            {
                yield return new Branch(path, directory).EnqueueOn(branches);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public record Branch(string Root, string Path)
    {
        public int Depth => Path.Split(System.IO.Path.DirectorySeparatorChar).Length - Path.Split(Root).Length;
    }
}

public static class DirectoryTreeBranchExtensions
{
    public static IEnumerable<string> Directories(this DirectoryTree.Branch branch)
    {
        return
            from path in Directory.EnumerateDirectories(branch.Path)
            select path;
    }

    public static IEnumerable<string> Files(this DirectoryTree.Branch branch)
    {
        return
            from path in Directory.EnumerateFiles(branch.Path)
            select path;
    }

    internal static DirectoryTree.Branch EnqueueOn(this DirectoryTree.Branch branch, Queue<DirectoryTree.Branch> queue)
    {
        queue.Enqueue(branch);
        return branch;
    }
}

internal static class QueueExtensions
{
    public static void Add<T>(this Queue<T> queue, T item)
    {
        queue.Enqueue(item);
    }

    public static IEnumerable<T> Consume<T>(this Queue<T> queue)
    {
        while (queue.Count > 0)
        {
            yield return queue.Dequeue();
        }
    }
}