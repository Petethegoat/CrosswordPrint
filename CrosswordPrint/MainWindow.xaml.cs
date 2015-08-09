using System;
using System.Net;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using Microsoft.Win32;

namespace CrosswordPrint
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        BackgroundWorker worker = new BackgroundWorker();
        private const string registry = "SOFTWARE\\CrosswordPrint";

        public MainWindow()
        {
            InitializeComponent();
            RegistryCheck();
            worker.WorkerReportsProgress = true;
            worker.DoWork += worker_DoWork;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
        }

        private void RegistryCheck()
        {
            RegistryKey rk = Registry.CurrentUser.CreateSubKey(registry);
            rk = Registry.CurrentUser.OpenSubKey(registry, false);
            if(rk != null)
            {
                object o = rk.GetValue("LastPrinted");
                if(o != null)
                {
                    Start.Text = o.ToString();
                    End.Text = o.ToString();
                }
            }
        }

        private void PreviewNumericInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = IsNumeric(e.Text);
        }

        private static bool IsNumeric(string text)
        {
            Regex r = new Regex("[0-9]+");
            return !r.IsMatch(text);
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            if(!worker.IsBusy)
            {
                ArgumentObj o = new ArgumentObj();
                o.url = URL.Text;
                o.start = Start.Text;
                o.end = End.Text;
                int min;
                int max;
                int.TryParse(Start.Text, out min);
                Progress.Minimum = min;
                int.TryParse(End.Text, out max);
                Progress.Maximum = max;
                worker.RunWorkerAsync(o);
                TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
            }
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Progress.Value = e.ProgressPercentage;
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                TaskbarItemInfo.ProgressValue = ((e.ProgressPercentage - Progress.Minimum) / (Progress.Maximum - Progress.Minimum));
            }));
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            using(WebClient c = new WebClient())
            {
                ArgumentObj o = e.Argument as ArgumentObj;
                string[] parts = Regex.Split(o.url, "\\*n\\*");
                int start = 0;
                int.TryParse(o.start, out start);
                int end = 1;
                int.TryParse(o.end, out end);
                string concatenate = "<head><meta charset='utf-8'><link rel='stylesheet' type='text/css' href='http://static.guim.co.uk/static/7ad1ce8f9c824be3436d34f657365865449b7caa/common/styles/print.css' media='screen,print' class='contrast'><link rel='stylesheet' type='text/css' href='http://combo.guim.co.uk/7ad1ce8f9c824be3436d34f657365865449b7caa/m-141~css/crossword-8-columns+m-141~css/crossword-search-4+m-141~css/print-crossword.css'></head>";
                concatenate += "<style>.crosswords-print-sponsorship{display:none;}</style>";
                for(int i = start; i <= end; i++)
                {
                    concatenate += "<div class='crossword' style='page-break-after: always;'>";
                    string page = c.DownloadString(parts[0] + i + parts[1]);
                    int endLine = FindLine(page, "<p class=\"crossword-spoiler\">SPOILER ALERT: Comments below may contain the answers and clues to this crossword.</p>");
                    concatenate += GetLines(page, 1099, endLine - 1);
                    concatenate += "</div></div></div></div></div></div>";  //fucking divs
                    (sender as BackgroundWorker).ReportProgress(i);
                }
                StreamWriter file = new StreamWriter(System.IO.Path.GetTempPath() + "\\print.html");
                file.Write(concatenate);
                file.Close();
            }
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Progress.Value = Progress.Minimum;
            TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
            System.Diagnostics.Process.Start(System.IO.Path.GetTempPath() + "\\print.html");
            int next = int.Parse(End.Text);
            next++;
            Start.Text = next.ToString();
            RegistryKey rk = Registry.CurrentUser.OpenSubKey(registry, true);
            if(rk != null)
            {
                rk.SetValue("LastPrinted", next.ToString());
            }
        }

        private void Latest_Click(object sender, RoutedEventArgs e)
        {
            Regex r = new Regex("Quick crossword No ([1-9][0-9],[0-9][0-9][0-9])");
            using(WebClient c = new WebClient())
            {
                string s = c.DownloadString("http://www.theguardian.com/crosswords/series/quick/rss");
                Match m = r.Match(s);
                if(m.Success)
                {
                    End.Text = m.Groups[1].ToString().Replace(",", "");
                }
            }
        }

        string GetLine(string text, int lineNo)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            return lines.Length >= lineNo ? lines[lineNo - 1] : null;
        }

        string GetLines(string text, int lineStart, int lineEnd)    //inclusive
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            string result = "";
            for(int i = 0; i < lines.Length; i++)
            {
                if(i > lineEnd)
                {
                    break;
                }
                if(i < lineStart)
                {
                    continue;
                }
                result += lines[i];
                result = result.Replace("â€”", "&#8212;");  //fix em dashes
            }
            return result;
        }

        int FindLine(string text, string find)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            for(int i = 0; i < lines.Length; i++)
            {
                if(lines[i].Contains(find))
                {
                    return i;
                }
            }
            return 0;
        }
    }

    class ArgumentObj
    {
        public string url;
        public string start;
        public string end;
    }
}
