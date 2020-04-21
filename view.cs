using Cairo;
using Gdk;
using Gtk;
using System;
using System.Collections.Generic;
using System.IO;
using Window = Gtk.Window;
using System.Linq;
using static State;
enum State { New, Open, Save, QuitProgram };

namespace graph
{
    class View : Window
    {
        State state = New;
        PointD mouseLocation;
        Graph g;
        Dictionary<int, Node> vd = new Dictionary<int, Node>();
        Dictionary<int, Dictionary<int, PointD>> sq = new Dictionary<int, Dictionary<int, PointD>>();
        int selectedVert = 0, currBiggestNum = 0, destination = 0;
        string path;
        BinaryHeap heap;
        List<int> shortestPath = new List<int>();
        bool dotAlreadyUsed, updateWeight, blink, beingDragged, findPath, displayPath;
        Tuple<int, int> weiToChange = new Tuple<int, int>(0, 0);
        List<string> weiToAdd = new List<string>();
        bool openResponse = true, saveResponse = true, newResponse = true, saved = true;
        class Node
        {
            public int elem;
            public PointD pos { get; set; }
            public int radius { get; set; }
            public Node(int elem, PointD pos, int radius = 0) { this.elem = elem; this.pos = pos; this.radius = radius; }
        }

        public View() : base("(untitled)")
        {
            AddEvents((int)(EventMask.ButtonPressMask |
                     EventMask.ButtonReleaseMask |
                     EventMask.KeyPressMask |
                     EventMask.PointerMotionMask));
            Resize(1000, 1000);

            MenuItem makeItem(string name, EventHandler handler)
            {
                MenuItem i = new MenuItem(name);
                i.Activated += handler;
                return i;
            }

            EventHandler menuChanger(State s) { return (sender, args) => { state = s; handleStates(); QueueDraw(); }; }

            MenuItem[] items = {
            makeItem("New", menuChanger(New)),
            makeItem("Open", menuChanger(Open)),
            makeItem("Save", menuChanger(Save)),
            makeItem("Quit", (sender, args) => { state = QuitProgram;  checkIfSaved("Quit"); } )
        };

            Menu fileMenu = new Menu();
            foreach (MenuItem i in items) fileMenu.Append(i);
            MenuItem fileItem = new MenuItem("File");
            fileItem.Submenu = fileMenu;
            MenuBar bar = new MenuBar();
            bar.Append(fileItem);
            VBox vbox = new VBox();
            vbox.PackStart(bar, false, false, 0);
            Add(vbox);
            init();
        }

        void init(string text = "(untitled)")
        {
            currBiggestNum = 0;
            findPath = displayPath = false;
            shortestPath.Clear();
            g = new Graph();
            var l = text.Split('/');
            Title = l[l.Length - 1];
            path = "";
            g.removeAllVertices();
            vd.Clear();
            QueueDraw();
            g.changed += QueueDraw;
        }

        string getFileName(string a, FileChooserAction t)
        {
            string n = "";
            FileChooserDialog op = new FileChooserDialog(a, this, t, "Cancel", ResponseType.Cancel, a, ResponseType.Accept);
            if (op.Run() == (int)ResponseType.Accept) n = op.Filename;
            op.Destroy();
            return n;
        }

        void handleError(string s)
        {
            init(Title);
            MessageDialog md = new MessageDialog(this, DialogFlags.DestroyWithParent, MessageType.Error, ButtonsType.Close, s);
            md.Run();
            md.Destroy();
        }

        void handleStates()
        {
            if (state == New)
            {
                checkIfSaved("Start over");
                if (newResponse || saved) { init(); saved = true; }
                return;
            }
            if (state == Open) checkIfSaved("Continue");
            else if (state == Save && path != "") { writeToFile(path); return; }
            string fn = (state == Open && openResponse) ? getFileName("Open", FileChooserAction.Open) : (state == Save && saveResponse) ? getFileName("Save", FileChooserAction.Save) : "";
            if (fn != "")
            {
                if (state == Open) readGraph(fn);
                else writeToFile(fn);
                QueueDraw();
            }
        }

        void removeNode(int v) { vd.Remove(v); }
        bool withinBoundary(double a, double pos, int r) => (a >= (pos - r) && a <= (pos + r));
        protected override bool OnButtonReleaseEvent(EventButton evnt) { beingDragged = false; return true; }

        void updateList(string s, bool backSpace)
        {
            if (backSpace && weiToAdd.Count != 0)
            {
                if (weiToAdd[weiToAdd.Count - 1] == ".") dotAlreadyUsed = false;
                weiToAdd.RemoveAt(weiToAdd.Count - 1);
            }
            else if ((s != "") || (s == "." && !dotAlreadyUsed)) weiToAdd.Add(s);
            string prio = String.Join("", weiToAdd);
            if (prio == "") prio = "0";
            g.changeWeight(weiToChange.Item1, weiToChange.Item2, double.Parse(prio));
            saved = false;
            QueueDraw();
        }

        protected override bool OnKeyPressEvent(EventKey e)
        {
            if (updateWeight)
            {
                if (e.Key == Gdk.Key.Return)
                {
                    updateWeight = false;
                    weiToChange = Tuple.Create(0, 0);
                    return true;
                }
                if (e.Key == Gdk.Key.period && !dotAlreadyUsed)
                {
                    updateList(".", false);
                    dotAlreadyUsed = true;
                }
                if (int.TryParse(e.Key.ToString().Last().ToString(), out int i)) { updateList(i.ToString(), false); }
                if (e.Key == Gdk.Key.BackSpace) { updateList("", true); }
            }

            if (selectedVert > 0 && e.Key == Gdk.Key.d)
            {
                removeNode(selectedVert);
                g.deleteVertex(selectedVert);
                selectedVert = 0;
                QueueDraw();
            }
            return true;
        }

        protected override bool OnButtonPressEvent(EventButton evnt)
        {
            double a = mouseLocation.X, b = mouseLocation.Y;
            foreach (int k in vd.Keys)
            {
                if ((withinBoundary(a, vd[k].pos.X, vd[k].radius) && (withinBoundary(b, vd[k].pos.Y, vd[k].radius))))
                {
                    if (selectedVert != k && evnt.State == ModifierType.ShiftMask)
                    {
                        displayPath = true;
                        heap = new BinaryHeap();
                        foreach (int i in vd.Keys)
                        {
                            if (i == selectedVert) heap.add(i, 0);
                            else heap.add(i, double.PositiveInfinity);
                        }
                        heap.dijkstra(g, currBiggestNum, selectedVert);
                        shortestPath = heap.display(selectedVert, k);
                        QueueDraw();
                        return true;
                    }

                    if (evnt.State == ModifierType.ControlMask && selectedVert != k && selectedVert != 0)
                    {
                        if (!g.addEdge(selectedVert, k, 0)) g.deleteEdge(selectedVert, k);
                        saved = false;
                        QueueDraw();
                        return true;
                    }
                    beingDragged = true;
                    selectedVert = k;
                    saved = updateWeight = false;
                    weiToChange = Tuple.Create(0, 0);
                    QueueDraw();
                    return true;
                }
            }

            foreach (int k in sq.Keys)
            {
                foreach (KeyValuePair<int, PointD> i in sq[k])
                {
                    if (withinBoundary(a, i.Value.X, 30) && withinBoundary(b, i.Value.Y, 21))
                    {
                        if (weiToChange.Item1 != k || weiToChange.Item2 != i.Key)
                        {
                            updateWeight = true;
                            startTimer();
                            weiToChange = Tuple.Create(k, i.Key);
                            weiToAdd = new List<string>(g[k][i.Key].ToString().Select(c => c.ToString()));
                            beingDragged = dotAlreadyUsed = false;
                            selectedVert = 0;

                            if (weiToAdd.Contains("E"))
                            {
                                string prio;
                                try
                                {
                                    prio = decimal.Parse(String.Join("", weiToAdd), System.Globalization.NumberStyles.Float).ToString();

                                }
                                catch (OverflowException) { prio = Decimal.MaxValue.ToString(); }
                                weiToAdd = new List<string>(prio.Select(c => c.ToString()));
                            }
                            if (weiToAdd.Contains(".")) dotAlreadyUsed = true;
                            QueueDraw();
                        }
                        return true;
                    }
                }
            }
            if (evnt.State == ModifierType.ShiftMask)
            {
                if (!updateWeight)
                {
                    saved = false;
                    int currNo = g.addVertex();
                    currBiggestNum = currNo;
                    vd.Add(currNo, new Node(currNo, mouseLocation));
                    selectedVert = currNo;
                    beingDragged = true;
                    QueueDraw();
                    return true;
                }
            }
            weiToChange = new Tuple<int, int>(0, 0);
            updateWeight = blink = false;
            weiToAdd.Clear();
            if (selectedVert > 0) selectedVert = 0;
            shortestPath.Clear();
            displayPath = false;
            QueueDraw();
            return true;
        }

        protected override bool OnMotionNotifyEvent(EventMotion e)
        {
            mouseLocation = new PointD(e.X, e.Y);
            if (beingDragged) { vd[selectedVert].pos = mouseLocation; QueueDraw(); }
            return true;
        }

        void addMidPoint(int from, int to, PointD mid)
        {
            if (!sq.ContainsKey(from)) sq.Add(from, new Dictionary<int, PointD>());
            else
            {
                if (!sq[from].ContainsKey(to)) sq[from].Add(to, mid);
                else sq[from][to] = mid;
            }
        }

        void startTimer()
        {
            GLib.Timeout.Add(300, delegate {
                blink = !blink;
                QueueDraw();
                return updateWeight;
            });
        }

        protected override bool OnExposeEvent(EventExpose e)
        {
            using (Context c = CairoHelper.Create(GdkWindow))
            {
                foreach (int k in vd.Keys)
                {
                    foreach (KeyValuePair<int, double> i in g[k])
                    {
                        c.SetSourceRGB(0.0, 0.0, 0.0);
                        c.MoveTo(vd[k].pos);
                        PointD p = vd[i.Key].pos;
                        c.LineTo(p);
                        c.Stroke();
                        PointD midP = new PointD((vd[k].pos.X + p.X) / 2, (vd[k].pos.Y + p.Y) / 2);
                        PointD midP2 = new PointD((vd[i.Key].pos.X + midP.X) / 2, (vd[i.Key].pos.Y + midP.Y) / 2);
                        addMidPoint(k, i.Key, midP2);

                        PointD inter, p1, p2;
                        Intersection(vd[i.Key].radius, vd[k].pos, vd[i.Key].pos, out inter, out p1, out p2);
                        c.SetSourceRGB(0.0, 0.0, 0.0);
                        c.MoveTo(inter);
                        c.LineTo(p1);
                        c.LineTo(p2);
                        c.ClosePath();
                        c.Fill();

                        c.SetFontSize(18);
                        string st = i.Value.ToString();

                        if (updateWeight && weiToChange.Item1 == k && weiToChange.Item2 == i.Key)
                        {
                            st = String.Join("", weiToAdd);
                            if (blink) st += "|";
                            c.SetSourceRGB(0.6, 0.4, 0.5);
                        }
                        else c.SetSourceRGB(0.0, 0.0, 0.0);

                        TextExtents te = c.TextExtents(st);
                        PointD mp = new PointD(10 + midP2.X - (te.Width / 2 + te.XBearing), 10 + midP2.Y - (te.Height / 2 + te.YBearing));
                        c.MoveTo(mp);
                        c.ShowText(st);
                        c.Stroke();
                    }
                }

                if (displayPath)
                {
                    c.SetSourceRGB(0.0, 0.6, 0.0);
                    for (int v = 0; v <= shortestPath.Count - 2; v++)
                    {
                        PointD from = vd[shortestPath[v]].pos;
                        PointD to = vd[shortestPath[v + 1]].pos;
                        c.MoveTo(from);
                        c.LineTo(to);
                        c.Stroke();
                    }
                }

                foreach (int k in vd.Keys)
                {
                    string s = k.ToString();
                    int radius = s.Length > 1 ? 10 * s.Length : 15;
                    vd[k].radius = radius;
                    c.MoveTo(vd[k].pos);
                    c.SetSourceRGB(1.0, 1.0, 1.0);
                    c.Arc(vd[k].pos.X, vd[k].pos.Y, radius, 0.0, 2 * Math.PI);
                    c.Fill();
                    c.SetSourceRGB(0.0, 0.0, 0.0);
                    c.Arc(vd[k].pos.X, vd[k].pos.Y, radius, 0.0, 2 * Math.PI);
                    if (k == selectedVert) c.Fill();
                    else c.Stroke();
                    c.SetFontSize(30);
                    var t = (k == selectedVert) ? (1.0, 1.0, 1.0) : (0.0, 0.0, 0.0);
                    c.SetSourceRGB(t.Item1, t.Item2, t.Item3);
                    TextExtents te = c.TextExtents(s);
                    c.MoveTo(vd[k].pos.X - (te.Width / 2 + te.XBearing), vd[k].pos.Y - (te.Height / 2 + te.YBearing));
                    c.ShowText(s);
                    c.Fill();
                }
            }
            return true;
        }

        void checkIfSaved(string s)
        {
            if (!saved)
            {
                MessageDialog md = new MessageDialog(this, DialogFlags.DestroyWithParent, MessageType.Question, ButtonsType.YesNo, $"File not saved. Are you sure you want to {s}?");
                if ((ResponseType)md.Run() == ResponseType.Yes)
                {
                    if (state == QuitProgram) Application.Quit();
                    else if (state == Open) openResponse = true;
                    else if (state == Save) saveResponse = true;
                    else newResponse = true;
                    md.Destroy(); return;
                }
                else
                {
                    if (state == Open) openResponse = false;
                    else if (state == Save) saveResponse = false;
                    else if (state == New) newResponse = false;
                    md.Destroy(); return;
                }
            }
            if (state == QuitProgram) Application.Quit();
        }

        protected override bool OnDeleteEvent(Event ev)
        {
            state = QuitProgram;
            checkIfSaved("Quit");
            return true;
        }

        void Intersection(double radius, PointD P, PointD Q, out PointD i, out PointD p1, out PointD p2)
        {
            var vecPQ = new PointD(Q.X - P.X, Q.Y - P.Y);
            var lenPQ = Math.Sqrt(vecPQ.X * vecPQ.X + vecPQ.Y * vecPQ.Y);
            var vecTQ = new PointD((radius / lenPQ) * vecPQ.X, (radius / lenPQ) * vecPQ.Y);
            i = new PointD(Q.X - vecTQ.X, Q.Y - vecTQ.Y);
            var po = new PointD(i.X - vecTQ.X / 1.5, i.Y - vecTQ.Y / 1.5);
            var perpVec = new PointD(1, -(vecPQ.X / vecPQ.Y));
            var magnitude = Math.Sqrt(1 + perpVec.Y * perpVec.Y);
            var unVec = new PointD(1 / magnitude, perpVec.Y / magnitude);
            p1 = new PointD(po.X + 7 * unVec.X, po.Y + 7 * unVec.Y);
            p2 = new PointD(po.X - 7 * unVec.X, po.Y - 7 * unVec.Y);
        }

        void writeToFile(string fileName)
        {
            if (fileName == "") return;
            path = fileName;
            var a = fileName.Split('.');
            string fileFormat = a[a.Length - 1];
            if (fileFormat != "txt" && fileFormat != "docx") { handleError("Error writing to file"); return; }
            using (StreamWriter r = new StreamWriter(fileName))
            {
                foreach (int i in vd.Keys)
                {
                    int count = 0;
                    string s = $"{i} {vd[i].pos.X} {vd[i].pos.Y}";
                    foreach (KeyValuePair<int, double> j in g[i])
                    {
                        r.WriteLine($"{i} {j.Key} {j.Value} {vd[i].pos.X} {vd[i].pos.Y} {vd[j.Key].pos.X} {vd[j.Key].pos.Y}");
                        count++;
                    }
                    if (count == 0) r.WriteLine(s);
                }
            }
            var l = fileName.Split('/');
            Title = l[l.Length - 1];
            saved = true;
        }

        void addToDict(int v, PointD p) { if (!vd.ContainsKey(v)) vd.Add(v, new Node(v, p)); }

        void readGraph(string fileName)
        {
            if (fileName == "") return;
            string t = Title;
            init(fileName);
            using (StreamReader r = new StreamReader(fileName))
            {
                try
                {
                    while (r.ReadLine() is string s)
                    {
                        string[] words = s.Split();
                        int from = int.Parse(words[0]);
                        if (words.Length == 7)
                        {
                            int to = int.Parse(words[1]);
                            double weight = double.Parse(words[2]);
                            double x = double.Parse(words[3]), y = double.Parse(words[4]);
                            double a = double.Parse(words[5]), b = double.Parse(words[6]);
                            g.addVertex(from, to, weight);
                            addToDict(from, new PointD(x, y));
                            addToDict(to, new PointD(a, b));
                            currBiggestNum = Math.Max(to, Math.Max(from, currBiggestNum));
                        }
                        else
                        {
                            double x = double.Parse(words[1]), y = double.Parse(words[2]);
                            g.addVertex(from);
                            if (!vd.ContainsKey(from)) vd.Add(from, new Node(from, new PointD(x, y)));
                            currBiggestNum = Math.Max(from, currBiggestNum);
                        }
                    }
                }
                catch (FormatException) { handleError("Error loading file"); Title = t; return; }
            }

            QueueDraw();
        }

        static void Main()
        {
            Application.Init();
            View w = new View();
            w.ShowAll();
            Application.Run();
        }
    }
}
