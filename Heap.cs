using System;
using System.Collections.Generic;
using System.Linq;
namespace graph
{
    public interface PriorityQueue
    {
        int count { get; }
        void add(int elem, double priority);
        (int, double) extractMin();
    }
    public class BinaryHeap : PriorityQueue
    {
        class Node
        {
            public int elem;
            public double pr;
            public Node(int elem, double prio) { this.elem = elem; this.pr = prio; }
        }

        List<Node> lis = new List<Node>();
        Dictionary<int, int> dict = new Dictionary<int, int>();
        static int[] prev;
        static double[] distFromSource;
        static int parent(int i) => (i - 1) / 2;
        static int left(int i) => 2 * i + 1;
        static int right(int i) => 2 * i + 2;
        static int c;
        public int count => c;

        public void dijkstra(Graph g, int num, int begin)
        {
            prev = new int[num + 1];
            distFromSource = new double[num + 1];
            for (int i = 1; i <= num; i++)
            {
                double val = double.PositiveInfinity;
                if (i == begin) val = 0;
                distFromSource[i] = val;
            }
            while (this.count > 0)
            {
                (int v, double v_dist) = extractMin();
                foreach (KeyValuePair<int, double> a in g[v])
                {
                    var newPriority = v_dist + a.Value;
                    if (distFromSource[a.Key] > newPriority)
                    {
                        distFromSource[a.Key] = newPriority;
                        lis[dict[a.Key]].pr = newPriority;
                        prev[a.Key] = v;
                        upHeap(dict[a.Key]);
                    }
                }
            }
        }

        void upHeap(int i)
        {
            while (true)
            {
                var m = lis[i].pr;
                if (0 <= parent(i)) m = Math.Min(m, lis[parent(i)].pr);
                if (m == lis[parent(i)].pr) break;
                swap(i, parent(i));
                i = parent(i);
            }
        }

        public void add(int elem, double prio)
        {
            lis.Add(new Node(elem, prio)); c++;
            int i = this.count - 1;
            dict.Add(elem, i);
            upHeap(i);
        }

        void swap(int a, int b)
        {
            var val = lis[a];
            lis[a] = lis[b];
            lis[b] = val;
            var k = dict[lis[a].elem];
            dict[lis[a].elem] = dict[lis[b].elem];
            dict[lis[b].elem] = k;
        }

        public List<int> display(int n, int end)
        {
            List<int> d = new List<int>();
            int i = end;
            if (distFromSource[i] != double.PositiveInfinity)
            {
                string st = i.ToString();
                var j = i;
                while (true)
                {
                    string currVert = prev[j].ToString();
                    if (j == n) break;
                    st = currVert + " " + st;
                    j = prev[j];
                }
                d = st.Split().ToList().Select(int.Parse).ToList();
            }
            return d;
        }

        public (int, double) extractMin()
        {
            var retVal = lis[0]; c--;
            dict.Remove(lis[0].elem);
            if (this.count == 0) { lis.RemoveAt(0); return (retVal.elem, retVal.pr); }
            var lastVal = lis[lis.Count - 1]; lis.RemoveAt(lis.Count - 1);
            lis[0] = lastVal; int i = 0;
            dict[lis[0].elem] = i;
            while (true)
            {
                var m = lis[i].pr;
                if (left(i) < lis.Count) m = Math.Min(m, lis[left(i)].pr);
                if (right(i) < lis.Count) m = Math.Min(m, lis[right(i)].pr);
                if (m == lis[i].pr) break;
                if (m == lis[left(i)].pr) { swap(i, left(i)); i = left(i); }
                else { swap(i, right(i)); i = right(i); }
            }
            return (retVal.elem, retVal.pr);
        }
    }
}
