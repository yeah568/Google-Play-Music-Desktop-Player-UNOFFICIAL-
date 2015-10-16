﻿using CefSharp;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Utilities;
using Microsoft.WindowsAPICodePack.Taskbar;
using System.Net;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace Google_Play_Music
{
    public partial class CoreMusicApp : Form
    {

        private const string CURRENT_VERSION = "1.4.1";

        public CoreMusicApp()
        {
            InitializeForm();

            this.Size = new Size(1080, 720);
            this.Icon = Properties.Resources.MainIcon;
            this.Text = "Google Music Player";
            StartPosition = FormStartPosition.Manual;
            Point loc = Screen.PrimaryScreen.WorkingArea.Location;
            int X = (Screen.PrimaryScreen.WorkingArea.Width / 2) - 540 + loc.X;
            int Y = (Screen.PrimaryScreen.WorkingArea.Height / 2) - 360 + loc.Y;
            Location = new Point((X > 0 ? X : 0), (Y > 0 ? Y : 0));
            

            // Check for updates on the Github Release API
            HttpWebRequest wrGETURL = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/MarshallOfSound/Google-Play-Music-Desktop-Player-UNOFFICIAL-/releases");
            wrGETURL.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2;)";
            StreamReader strRead;
            try {
                strRead = new StreamReader(wrGETURL.GetResponse().GetResponseStream());
            } catch (WebException)
            {
                return;
            }

            try {
                dynamic JSON = JsonConvert.DeserializeObject(strRead.ReadToEnd());
                string version = JSON[0].tag_name;
                string downloadURL = JSON[0].assets[0].browser_download_url;
                // If the newest version is not the current version there must be an update available
                if (version != CURRENT_VERSION)
                {
                    var result = MessageBox.Show("There is an update available for this player, do you want to download it now?", "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (result == DialogResult.Yes)
                    {
                        // Download the Resource URL from the GitHub API
                        Process.Start(downloadURL);
                        // Let the form finish initialising before closing it through an asyncronous method invoker
                        // Prevents strange garbage collection
                        new Thread(() =>
                        {
                            Load += (send, ev) =>
                            {
                                Close();
                            };
                        }).Start();
                        return;
                    }
                }
            } catch (Exception)
            {
                // Something went wrong while fetching from the GitHub API
            }
        }

        // Media Functions
        private void playPause()
        {
            webBrowser1.EvaluateScriptAsync("(function() {document.querySelectorAll('[data-id=play-pause]')[0].click()})()");
        }

        private void prevTrack()
        {
            webBrowser1.EvaluateScriptAsync("(function() {document.querySelectorAll('[data-id=rewind]')[0].click()})()");
        }

        private void nextTrack()
        {
            webBrowser1.EvaluateScriptAsync("(function() {document.querySelectorAll('[data-id=forward]')[0].click()})()");
        }

        // Task Bar Media Controls
        private ThumbnailToolBarButton prevTrackButton;
        private ThumbnailToolBarButton nextTrackButton;
        private ThumbnailToolBarButton playPauseButton;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            prevTrackButton = new ThumbnailToolBarButton(Properties.Resources.PrevTrack, "Previous Track");
            prevTrackButton.Click += (send, ev) =>
            {
                this.prevTrack();
            };

            nextTrackButton = new ThumbnailToolBarButton(Properties.Resources.NextTrack, "Next Track");
            nextTrackButton.Click += (send, ev) =>
            {
                this.nextTrack();
            };


            playPauseButton = new ThumbnailToolBarButton(Properties.Resources.Play, "Play / Pause");
            playPauseButton.Click += (send, ev) =>
            {
                this.playPause();
            };

            TaskbarManager.Instance.ThumbnailToolBars.AddButtons(this.Handle, prevTrackButton, playPauseButton, nextTrackButton);
        }

        // CefSharp configuration
        public CefSharp.WinForms.ChromiumWebBrowser webBrowser1;
        private static globalKeyboardHook gkh;

        private void InitializeForm()
        {
            CefSettings settings = new CefSharp.CefSettings();
            settings.CachePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/GPMDP";
            settings.WindowlessRenderingEnabled = true;
            settings.CefCommandLineArgs.Add("enable-smooth-scrolling", "1");
            settings.CefCommandLineArgs.Add("enable-overlay-scrollbar", "1");
            settings.CefCommandLineArgs.Add("enable-npapi", "1");
            Cef.Initialize(settings);

            webBrowser1 = new CefSharp.WinForms.ChromiumWebBrowser("http://play.google.com/music/listen")
            {
                // Use this to inject our theming and modding javascript code
                ResourceHandlerFactory = new GPMResouceHandlerFactory(),
                // Stop that silly right click menu appearing
                MenuHandler = new GPMMenuHandler()
            };
            webBrowser1.RegisterAsyncJsObject("csharpinterface", new JSBound(this));

            webBrowser1.Dock = DockStyle.Fill;

            Controls.Add(webBrowser1);

            gkh = new globalKeyboardHook();

            // Don't let the Garbage Man interfere
            GC.KeepAlive(gkh);

            // Global Hotkey Listener
            gkh.HookedKeys.Add(Keys.MediaPlayPause);
            gkh.HookedKeys.Add(Keys.MediaNextTrack);
            gkh.HookedKeys.Add(Keys.MediaPreviousTrack);
            gkh.HookedKeys.Add(Keys.MediaStop);
            gkh.KeyDown += new KeyEventHandler(gkh_KeyDown);
            gkh.KeyUp += new KeyEventHandler(gkh_KeyUp);
        }

        private void Form1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try {
                // Make sure we unhook once the form closes
                if (gkh != null)
                {
                    gkh.unhook();
                }
            } catch (Exception) {
                // Do nothing
            }
        }

        void gkh_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode.ToString())
            {
                case "MediaPlayPause":
                    this.playPause();
                    break;
                case "MediaStop":
                    this.playPause();
                    break;
                case "MediaNextTrack":
                    this.nextTrack();
                    break;
                case "MediaPreviousTrack":
                    this.prevTrack();
                    break;
            }
            e.Handled = false;
        }

        void gkh_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = false;
        }

        public void fadeInOut(Func<int> call)
        {
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 2;
            int currentStep = 0;
            int fadeSteps = 20;
            int totalSteps = fadeSteps * 2 + 12;
            Boolean runTick = true;
            timer.Tick += (arg1, arg2) =>
            {
                if (runTick)
                {
                    currentStep++;
                    if (currentStep <= fadeSteps)
                    {
                        Opacity = ((double)(fadeSteps - currentStep) / fadeSteps);
                    }
                    else if (currentStep == fadeSteps + 1)
                    {
                        runTick = false;
                        call();
                        runTick = true;
                    }
                    else if (currentStep <= totalSteps)
                    {
                        Opacity = ((double)(fadeSteps - totalSteps + currentStep)) / fadeSteps;
                    }
                    else
                    {
                        timer.Stop();
                        timer.Dispose();
                    }
                }
            };
            timer.Start();
        }

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        public void dragStart()
        {
            // This function fakes a window drag start
            // It is triggered from the boundJS object
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        }
    }
}