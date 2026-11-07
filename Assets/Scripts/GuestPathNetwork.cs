using System;
using System.Collections.Generic;

// Walkway connections only; this contains no wave or needs simulation.
public sealed class GuestPathNetwork
{
    [Serializable]
    public struct Link { public int from; public int to; }
    private readonly List<int>[] neighbours;
    public int Count => neighbours.Length;

    public GuestPathNetwork(int count, Link[] links)
    {
        if (count < 2 || links == null) throw new ArgumentException("At least two connected path points are required.");
        neighbours = new List<int>[count];
        for (int i = 0; i < count; i++) neighbours[i] = new List<int>();
        foreach (var link in links)
        {
            if (link.from < 0 || link.to < 0 || link.from >= count || link.to >= count || link.from == link.to)
                throw new ArgumentException("Invalid walkway endpoints.");
            if (!neighbours[link.from].Contains(link.to)) neighbours[link.from].Add(link.to);
            if (!neighbours[link.to].Contains(link.from)) neighbours[link.to].Add(link.from);
        }
        for (int i = 1; i < count; i++)
            if (FindPath(0, i).Count == 0) throw new ArgumentException("All paths must be connected to the pier.");
    }

    public List<int> FindPath(int start, int destination)
    {
        var previous = new int[Count];
        for (int i = 0; i < Count; i++) previous[i] = -1;
        var queue = new Queue<int>();
        queue.Enqueue(start);
        previous[start] = start;
        while (queue.Count > 0 && previous[destination] < 0)
        {
            int current = queue.Dequeue();
            foreach (int next in neighbours[current])
            {
                if (previous[next] >= 0) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        var path = new List<int>();
        if (previous[destination] < 0) return path;
        for (int step = destination; step != start; step = previous[step]) path.Add(step);
        path.Reverse();
        return path;
    }

    public List<int> CreateTour(int start, Random random)
    {
        var points = new List<int>();
        for (int i = 0; i < Count; i++) if (i != start) points.Add(i);
        for (int i = points.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int value = points[i]; points[i] = points[j]; points[j] = value;
        }
        return points;
    }
}
