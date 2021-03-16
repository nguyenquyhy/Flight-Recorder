using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
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

namespace FlightRecorder.Updater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();


             ToDown();
        }

        async void ToDown()
        {
            await Task.Run(() => Download());
        }

        private delegate void UpdateProgressBarDelegate(System.Windows.DependencyProperty dp, Object value);

        public void Download()
        {
            var client = new GitHubClient(new ProductHeaderValue("Flight-Recorder"));
            var releases = client.Repository.Release.GetAll("nguyenquyhy", "Flight-Recorder").Result;
            var latest = releases[0];
            WebClient client_web = new WebClient();
            Uri ur = new Uri(latest.Assets[0].BrowserDownloadUrl) ;
            client_web.DownloadProgressChanged += new DownloadProgressChangedEventHandler(WebClientDownloadProgressChanged);
            client_web.DownloadFileCompleted += new  System.ComponentModel.AsyncCompletedEventHandler (WebClientDownloadCompleted);
            string dir_app = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            client_web.DownloadFileAsync(ur, dir_app + @"\" + latest.Assets[0].Name);
           // lblText.Text = "Downloading ata"


        }


        private void WebClientDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            UpdateProgressBarDelegate updatePbDelegate = new UpdateProgressBarDelegate(lblText.SetValue);
            Dispatcher.Invoke(updatePbDelegate,
                 System.Windows.Threading.DispatcherPriority.Background,
                 new object[] { TextBlock.TextProperty, "Downloading data" });

            double percentage = (double.Parse(e.BytesReceived.ToString()) / double.Parse(e.TotalBytesToReceive.ToString())) * 100;
           updatePbDelegate = new UpdateProgressBarDelegate(pbProgress.SetValue);
            Dispatcher.Invoke(updatePbDelegate,
                System.Windows.Threading.DispatcherPriority.Background,
                new object[] { ProgressBar.ValueProperty, percentage });
        }

        private async void WebClientDownloadCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            await Task.Run(() => UnzipUpdate());
        }

        private void UnzipUpdate()
        {
            try
            {
                #region Labels Sets
                UpdateProgressBarDelegate updatePbDelegate = new UpdateProgressBarDelegate(pbProgress.SetValue);
                Dispatcher.Invoke(updatePbDelegate,
                    System.Windows.Threading.DispatcherPriority.Background,
                    new object[] { ProgressBar.ValueProperty, Double.Parse("0") });

                this.Dispatcher.Invoke((Action)(() => {
                    pbProgress.Visibility = Visibility.Hidden;
                }));

                updatePbDelegate = new UpdateProgressBarDelegate(lblText.SetValue);
                Dispatcher.Invoke(updatePbDelegate,
                     System.Windows.Threading.DispatcherPriority.Background,
                     new object[] { TextBlock.TextProperty, "Unzipping Files" });
                #endregion Labels Sets

                #region UnZip
                string fileFilter = null;
                string DirFilter = null;
                FastZipEvents events = new FastZipEvents();
                events.ProcessFile = ProcessFileMethod;

                FastZip fastZip = new FastZip(events);
                string dir_app = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName).ToString();
                fastZip.ExtractZip(dir_app + @"\Flight.Recorder.zip", dir_app, FastZip.Overwrite.Always, null, fileFilter, DirFilter,true,true);

                #endregion UnZip

                #region Labels Sets
                this.Dispatcher.Invoke((Action)(() => {
                    pbProgress.Visibility = Visibility.Visible;
                }));


                updatePbDelegate = new UpdateProgressBarDelegate(lblText.SetValue);
                Dispatcher.Invoke(updatePbDelegate,
                     System.Windows.Threading.DispatcherPriority.Background,
                     new object[] { TextBlock.TextProperty, "Updating Files" });

                updatePbDelegate = new UpdateProgressBarDelegate(lblProgress.SetValue);
                Dispatcher.Invoke(updatePbDelegate,
                     System.Windows.Threading.DispatcherPriority.Background,
                     new object[] { TextBlock.TextProperty, "Updating" });

                this.Dispatcher.Invoke((Action)(() => {
                    pbProgress.IsIndeterminate = true;
                }));

                #endregion Labels Sets

                #region Arq Actions
                var dir_to = dir_app.Replace(@"\Updater", "").Substring(0  , dir_app.LastIndexOf(@"\"));
                if(Directory.Exists(dir_app + @"\Flight Recorder") && Directory.Exists(dir_to))
                {
                    DirectoryCopy(dir_app + @"\Flight Recorder", dir_to, true);
                }
                if(File.Exists(dir_app.Replace(@"\Updater", "") + @"\FlightRecorder.Client.exe"))
                    Process.Start(dir_app.Replace(@"\Updater", "") + @"\FlightRecorder.Client.exe");
                if(Directory.Exists(dir_app + @"\Flight Recorder"))
                    Directory.Delete(dir_app + @"\Flight Recorder",true );
                if(File.Exists(dir_app + @"\Flight.Recorder.zip"))
                    File.Delete(dir_app + @"\Flight.Recorder.zip");
                #endregion Arq Actions

                Dispatcher.Invoke( System.Windows.Application.Current.Shutdown, System.Windows.Threading.DispatcherPriority.Normal);

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error : " + ex.Message);
            }
        }
        

        private void ProcessFileMethod(object sender, ScanEventArgs args)
        {
            if(args.ContinueRunning)
            {
                string arquivo = args.Name;
                UpdateProgressBarDelegate updatePbDelegate = new UpdateProgressBarDelegate(lblProgress.SetValue);
                Dispatcher.Invoke(updatePbDelegate,
                     System.Windows.Threading.DispatcherPriority.Background,
                     new object[] { TextBlock.TextProperty,  arquivo});
            }
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
 
            Directory.CreateDirectory(destDirName);


            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = System.IO.Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath,true);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = System.IO.Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, copySubDirs);
                }
            }
        }

    }
}
