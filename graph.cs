using System;
using System.Collections.Generic;
using System.Linq;
public delegate void Notify();

namespace graph

{
    public class Graph
    {
        public event Notify changed;
        Dictionary<int, Dictionary<int, double>> d = new Dictionary<int, Dictionary<int, double>>();
        public Dictionary<int, double> this[int x] { get => d[x]; }
        public void removeAllVertices() { d.Clear(); }

        public int addVertex()
        {
            int currBiggestNum;
            if (d.Count == 0) currBiggestNum = 1;
            else currBiggestNum = d.Keys.Max() + 1;
            d.Add(currBiggestNum, new Dictionary<int, double>());
            changed();
            return currBiggestNum;
        }

        public void addVertex(int a) { if (!d.ContainsKey(a)) d.Add(a, new Dictionary<int, double>()); changed(); }

        public void addVertex(int from, int to, double wei)
        {
            if (!d.ContainsKey(from)) d.Add(from, new Dictionary<int, double>());
            d[from].Add(to, wei);
            if (!d.ContainsKey(to)) d.Add(to, new Dictionary<int, double>());
            changed();
        }

        public void changeWeight(int source, int dest, double wei) { d[source][dest] = wei; changed(); }
        public bool addEdge(int source, int dest, double wei)
        {
            if (!d[source].ContainsKey(dest)) { d[source].Add(dest, wei); changed(); return true; }
            changed();
            return false;
        }

        public void deleteEdge(int source, int dest) { if (d[source].ContainsKey(dest)) d[source].Remove(dest); changed(); }

        public void deleteVertex(int v)
        {
            if (d.ContainsKey(v)) d.Remove(v);
            foreach (int k in d.Keys)
            {
                if (d[k].ContainsKey(v)) d[k].Remove(v);
            }
            changed();
        }
    }
}
