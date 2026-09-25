using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;

class FileCompare : Form
{
    TextBox boxA, boxB, hashBox;
    Button compareBtn;
    ProgressBar bar;
    Label result, subResult;
    BackgroundWorker worker;

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        var f = new FileCompare();
        if (args.Length > 0) f.boxA.Text = args[0];
        if (args.Length > 1) f.boxB.Text = args[1];
        if (args.Length > 1) f.Shown += delegate { f.StartCompare(); };
        Application.Run(f);
    }

    FileCompare()
    {
        Text = "File Compare";
        ClientSize = new Size(620, 300);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        AllowDrop = true;
        DragEnter += OnDragEnter;
        DragDrop += OnFormDrop;

        boxA = AddRow("File 1:", 15);
        boxB = AddRow("File 2:", 55);

        compareBtn = new Button { Text = "Compare", Location = new Point(15, 95), Size = new Size(100, 30) };
        compareBtn.Click += delegate { StartCompare(); };
        Controls.Add(compareBtn);

        bar = new ProgressBar { Location = new Point(125, 99), Size = new Size(480, 22) };
        Controls.Add(bar);

        result = new Label { Location = new Point(15, 135), Size = new Size(590, 32), Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            Text = "Drop files here or use Browse" };
        Controls.Add(result);

        subResult = new Label { Location = new Point(17, 165), Size = new Size(590, 18), Font = new Font("Segoe UI", 9f), ForeColor = Color.FromArgb(40, 40, 40) };
        Controls.Add(subResult);

        hashBox = new TextBox { Location = new Point(15, 188), Size = new Size(590, 97), Multiline = true, ReadOnly = true,
            Font = new Font("Consolas", 9f), ScrollBars = ScrollBars.Vertical };
        Controls.Add(hashBox);

        worker = new BackgroundWorker { WorkerReportsProgress = true };
        worker.DoWork += DoCompare;
        worker.ProgressChanged += (s, e) => bar.Value = e.ProgressPercentage;
        worker.RunWorkerCompleted += OnDone;
    }

    TextBox AddRow(string label, int y)
    {
        Controls.Add(new Label { Text = label, Location = new Point(15, y + 4), AutoSize = true });
        var box = new TextBox { Location = new Point(65, y), Size = new Size(440, 25), AllowDrop = true };
        box.DragEnter += OnDragEnter;
        box.DragDrop += (s, e) => { var f = Dropped(e); if (f.Length > 0) box.Text = f[0]; };
        Controls.Add(box);
        var browse = new Button { Text = "Browse...", Location = new Point(515, y - 1), Size = new Size(90, 28) };
        browse.Click += delegate {
            using (var d = new OpenFileDialog()) if (d.ShowDialog() == DialogResult.OK) box.Text = d.FileName;
        };
        Controls.Add(browse);
        return box;
    }

    static string[] Dropped(DragEventArgs e) { return (e.Data.GetData(DataFormats.FileDrop) as string[]) ?? new string[0]; }

    void OnDragEnter(object s, DragEventArgs e)
    {
        e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
    }

    void OnFormDrop(object s, DragEventArgs e)
    {
        var files = Dropped(e);
        if (files.Length >= 2) { boxA.Text = files[0]; boxB.Text = files[1]; }
        else if (files.Length == 1) { if (boxA.Text == "") boxA.Text = files[0]; else boxB.Text = files[0]; }
        if (boxA.Text != "" && boxB.Text != "") StartCompare();
    }

    void StartCompare()
    {
        if (worker.IsBusy) return;
        if (!File.Exists(boxA.Text) || !File.Exists(boxB.Text))
        {
            result.ForeColor = Color.DarkOrange; result.Text = "Pick two existing files"; return;
        }
        compareBtn.Enabled = false; bar.Value = 0; hashBox.Text = ""; subResult.Text = "";
        result.ForeColor = SystemColors.ControlText; result.Text = "Comparing...";
        worker.RunWorkerAsync(new[] { boxA.Text, boxB.Text });
    }

    void DoCompare(object s, DoWorkEventArgs e)
    {
        var p = (string[])e.Argument;
        long lenA = new FileInfo(p[0]).Length, lenB = new FileInfo(p[1]).Length;
        long total = lenA + lenB, done = 0;
        var hashes = new string[2];
        for (int i = 0; i < 2; i++)
        {
            using (var sha = SHA256.Create())
            using (var fs = new FileStream(p[i], FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
            {
                var buf = new byte[1 << 20]; int n, last = -1;
                while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    sha.TransformBlock(buf, 0, n, null, 0);
                    done += n;
                    int pct = total == 0 ? 100 : (int)(done * 100 / total);
                    if (pct != last) { worker.ReportProgress(pct); last = pct; }
                }
                sha.TransformFinalBlock(buf, 0, 0);
                hashes[i] = BitConverter.ToString(sha.Hash).Replace("-", "");
            }
        }
        e.Result = new object[] { lenA, lenB, hashes[0], hashes[1] };
    }

    void OnDone(object s, RunWorkerCompletedEventArgs e)
    {
        compareBtn.Enabled = true;
        if (e.Error != null) { result.ForeColor = Color.DarkOrange; result.Text = "Error"; hashBox.Text = e.Error.Message; return; }
        var r = (object[])e.Result;
        long lenA = (long)r[0], lenB = (long)r[1];
        string hA = (string)r[2], hB = (string)r[3];
        bar.Value = 100;
        if (hA == hB)
        {
            result.ForeColor = Color.ForestGreen; result.Text = "It's a match!";
            subResult.Text = "You can safely delete one of them";
            hashBox.Text = "Size: " + lenA.ToString("N0") + " bytes";
        }
        else
        {
            result.ForeColor = Color.Firebrick;
            result.Text = "False  (" + (lenA != lenB ? "sizes differ" : "contents differ") + ")";
            hashBox.Text = "File 1: " + lenA.ToString("N0") + " bytes\r\nSHA256: " + hA +
                "\r\n\r\nFile 2: " + lenB.ToString("N0") + " bytes\r\nSHA256: " + hB;
        }
    }
}
