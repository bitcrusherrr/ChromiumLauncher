using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChromiumLauncher
{
    class Program
    {
        private static string _chromePath;
        private static string _lastBuild;

        [STAThread]
        static void Main(string[] args)
        {
            ProcessArgs(args);
            System.Threading.Thread.Sleep(5000);//Just here for "debug"
        }

        private static void Cleanup()
        {
            Console.WriteLine("Removing old Chromium");
            if (!string.IsNullOrEmpty(LauncherConfig.Default.LastVersion))
                Directory.Delete(Path.Combine(_chromePath, LauncherConfig.Default.LastVersion), true);

            LauncherConfig.Default.LastVersion = _lastBuild;
            LauncherConfig.Default.Save();
        }

        private static void ExtractChrome()
        {
            Console.WriteLine("Unpacking Chromium");
            ZipFile.ExtractToDirectory(Path.Combine(_chromePath, "chrome-win32.zip"), Path.Combine(_chromePath, _lastBuild));
        }

        private static void DownloadNewChrome()
        {
            Console.WriteLine("Downloading Chromium");
            using (var client = new WebClient())
            {
                string downloadURL = @"https://commondatastorage.googleapis.com/chromium-browser-snapshots/Win/" + _lastBuild + "/chrome-win32.zip";
                client.DownloadFile(downloadURL, Path.Combine(_chromePath, "chrome-win32.zip"));
            }
        }

        private static void CheckConfig()
        {
            _chromePath = LauncherConfig.Default.ChromiumPath;
            if (!Directory.Exists(_chromePath))
            {
                FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
                folderBrowserDialog1.Description = "Navigate to where you want to keep Chromium";
                if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                {
                    LauncherConfig.Default.ChromiumPath = _chromePath = folderBrowserDialog1.SelectedPath;
                    LauncherConfig.Default.Save();
                }
            }
        }

        private static bool NewBuildAvaliable()
        {
            bool result = false;

            //Get last build number from snapshots
            HttpWebRequest request = (HttpWebRequest) WebRequest.Create("http://commondatastorage.googleapis.com/chromium-browser-snapshots/Win/LAST_CHANGE");
            HttpWebResponse response = (HttpWebResponse) request.GetResponse();
            Stream resStream = response.GetResponseStream();

            using (StreamReader reader = new StreamReader(resStream, Encoding.UTF8))
            {
                _lastBuild = reader.ReadToEnd();
            }

            if (_lastBuild.CompareTo(LauncherConfig.Default.LastVersion) != 0)
            {
                result = true;
                Console.WriteLine("New Chromium version is found, build number: " + _lastBuild);
            }
            else
                Console.WriteLine("No new version found");

            return result;
        }

        private static void LaunchChrome()
        {
            Console.WriteLine("Starting Chromium");
            ProcessStartInfo chromeProc = new ProcessStartInfo();
            chromeProc.FileName = Path.Combine(_chromePath, LauncherConfig.Default.LastVersion, @"chrome-win32\chrome.exe");

            Process.Start(chromeProc);
        }

        private static void ProcessArgs(string[] args)
        {
            //Deal with arguments, what should we accpet?

            //Reset - will remove application parameters (chromium location)

            //ForceUpdate - will redownload (new) version again

            //NoCheck - skip the checks for faster launch

            //Background - Run the check in the background, notify user if new version exists and ask for installation 
            //Should this be default?

            if (args.Count() == 0)
            {
                CheckConfig();

                if (NewBuildAvaliable())
                {
                    DownloadNewChrome();
                    ExtractChrome();
                    Cleanup();
                }

                LaunchChrome();
            }
            else
            {
                switch (args[0].ToLower())
                {
                    case "reset":
                        _chromePath = string.Empty;
                        CheckConfig();

                        if (NewBuildAvaliable())
                        {
                            DownloadNewChrome();
                            ExtractChrome();
                            Cleanup();
                        }

                        LaunchChrome();
                        break;
                    case "forceupdate":
                        NewBuildAvaliable();//Still have to call it to refresh build number
                        DownloadNewChrome();
                        ExtractChrome();
                        Cleanup();
                        LaunchChrome();
                        break;
                    case "nocheck":
                        LaunchChrome();
                        break;
                    case "background":
                        break;
                }
            }
        }
    }
}
