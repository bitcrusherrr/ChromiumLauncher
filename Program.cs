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
            if (!string.IsNullOrEmpty(LauncherConfig.Default.LastVersion) && Directory.Exists(Path.Combine(_chromePath, LauncherConfig.Default.LastVersion)) &&
                 LauncherConfig.Default.LastVersion.CompareTo(_lastBuild) != 0)
                Directory.Delete(Path.Combine(_chromePath, LauncherConfig.Default.LastVersion), true);

            LauncherConfig.Default.LastVersion = _lastBuild;
            LauncherConfig.Default.Save();
        }

        private static void ExtractChrome()
        {
            Console.WriteLine("Unpacking Chromium");

            //Check if folder already exists
            if(Directory.Exists(Path.Combine(_chromePath, _lastBuild)))
            {
                Directory.Delete(Path.Combine(_chromePath, _lastBuild), true);
            }

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

            //Also check if Chromium folder is still there, just in case it was deleted by user and there is nothing to launch
            if (!result && !Directory.Exists(Path.Combine(_chromePath, LauncherConfig.Default.LastVersion, "chrome-win32")))
            {
                Console.WriteLine("Missing Chromium folder");
                result = true;
            }

            return result;
        }

        private static void LaunchChrome()
        {
            Console.WriteLine("Starting Chromium");
            ProcessStartInfo chromeProc = new ProcessStartInfo();
            chromeProc.FileName = Path.Combine(_chromePath, LauncherConfig.Default.LastVersion, @"chrome-win32\chrome.exe");

            if (File.Exists(chromeProc.FileName))
                Process.Start(chromeProc);
            else
                Console.WriteLine("Chromium executable was not found");
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
                    if (!CheckForRunningChrome())
                    {
                        ExtractChrome();
                        Cleanup();
                    }
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
                            if (!CheckForRunningChrome())
                            {
                                ExtractChrome();
                                Cleanup();
                            }
                        }

                        LaunchChrome();
                        break;
                    case "forceupdate":
                        if (!CheckForRunningChrome())
                        {
                            NewBuildAvaliable();//Still have to call it to refresh build number
                            DownloadNewChrome();
                            ExtractChrome();
                            Cleanup();
                            LaunchChrome();
                        }
                        break;
                    case "nocheck":
                        LaunchChrome();
                        break;
                    case "background":
                        break;
                }
            }
        }

        private static bool CheckForRunningChrome()
        {
            bool result = false;

            Process[] chromeProcs = Process.GetProcessesByName("chrome");

            if (chromeProcs.Count() > 0)
            {
                MessageBox.Show("Chromium is currently running. \nPlease close it if you wish to carry on with the update before closing this message.\nOtherwise update will be aborted");

                chromeProcs = Process.GetProcessesByName("chrome");
                if (chromeProcs.Count() > 0)
                {
                    Console.WriteLine("Chromium was not closed. Aborting");
                    result = true;
                }
            }

            return result;
        }
    }
}
