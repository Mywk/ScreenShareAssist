/* Copyright (C) 2021 - Mywk.Net
 * Licensed under the EUPL, Version 1.2
 * You may obtain a copy of the Licence at: https://joinup.ec.europa.eu/community/eupl/og_page/eupl
 * Unless required by applicable law or agreed to in writing, software distributed under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * 
 * Code related to NAudio (MIT License) and OpenCvSharp (Apache-2.0 License) is licensed under their respective licenses.
 */
using NAudio.Wave;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace ScreenShareAssist
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public MainWindow()
        {
            InitializeComponent();

            RefreshVideoList();
            RefreshAudioInputs();
            RefreshAudioOutputs();

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            bottomLabel.Content = "ScreenShareAssist  v" + version.Major + "." + version.Minor + " © " + DateTime.Now.Year + " - Mywk.Net";

            if (CheckForUpdates())
                updateLabel.Visibility = Visibility.Visible;
        }


        /// <summary>
        /// Refresh our video list
        /// </summary>
        private void RefreshVideoList()
        {
            videoInputComboBox.Items.Clear();

            videoInputComboBox.Items.Add(noneString);
            videoInputComboBox.Items.Add("▌ Static image");
            videoInputComboBox.Items.Add("▌ Video loop");

            // TODO - Video loop
            //cameraComboBox.Items.Add("Video loop");

            foreach (var camera in ListLocalDevices.GetCameras())
            {
                videoInputComboBox.Items.Add(camera);
            }

            if (videoInputComboBox.Items.Count > 0)
                videoInputComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Check if a newer version of the software is available
        /// </summary>
        private bool CheckForUpdates()
        {
            try
            {
                var web = new System.Net.WebClient();
                var url = "https://Mywk.Net/software.php?assembly=" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
                var responseString = web.DownloadString(url);

                foreach (var str in responseString.Split('\n'))
                {
                    if (str.Contains("Version"))
                    {
                        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                        if (version.Major + "." + version.Minor != str.Split('=')[1])
                            return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// This sets the window as out of sight, location is not saved and window can still be streamed while not being visible for the user
        /// </summary>
        bool windowOutOfSight = false;

        bool settingsLoaded = false;
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load default settings
            LoadSettings();

            settingsLoaded = true;

            // Fade-in animation
            var anim = new DoubleAnimation(1, (Duration)TimeSpan.FromMilliseconds(400));
            anim.Completed += (s, _) => this.ToggleUI();
            loadingIcon.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        /// <summary>
        /// Closing fade-out animation
        /// </summary>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Closing -= Window_Closing;
            e.Cancel = true;
            var anim = new DoubleAnimation(0, (Duration)TimeSpan.FromMilliseconds(300));
            anim.Completed += (s, _) => this.Close();
            this.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        /// <summary>
        /// Quick and cheap way to delete the last element, I don't like the way the UI looks with an extra remove icon on each combobox so until a re-design is required I'm leaving it as-is
        /// </summary>
        const string removeAudioInString = "▌ Remove";

        const string noneString = "▌ None";

        /// <summary>
        /// Refresh our audio input list
        /// </summary>
        private void RefreshAudioInputs()
        {
            for (int i = 0; i < audioInComboBoxPanel.Children.Count; i++)
            {
                var item = audioInComboBoxPanel.Children[i];

                var audioInComboBox = (ComboBox)item;

                audioInComboBox.Items.Clear();

                audioInComboBox.Items.Add(noneString);

                foreach (var camera in ListLocalDevices.GetAudioInputSources())
                {
                    audioInComboBox.Items.Add(camera);
                }

                if (audioInComboBox.Items.Count > 0)
                    audioInComboBox.SelectedIndex = 0;

                if (i != 0)
                    audioInComboBox.Items.Add(removeAudioInString);
            }
        }

        /// <summary>
        /// Refresh our audio out list
        /// </summary>
        private void RefreshAudioOutputs()
        {
            audioOutComboBox.Items.Clear();
            audioOutComboBox.Items.Add(noneString);

            foreach (var camera in ListLocalDevices.GetAudioOutputSources())
            {
                audioOutComboBox.Items.Add(camera);
            }

            if (audioOutComboBox.Items.Count > 0)
                audioOutComboBox.SelectedIndex = 0;
        }

        //                 videoInputComboBox.Dispatcher.Invoke((new Action(() => selectedIndex = videoInputComboBox.SelectedIndex)));

        public enum LoadingIconType
        {
            None,
            Stopped,
            Loading
        }

        private LoadingIconType LoadingIconVisibility
        {
            set
            {
                loadingIcon.Dispatcher.Invoke((new Action(() =>
                {
                    loadingIcon.Visibility = (value == LoadingIconType.None ? Visibility.Hidden : Visibility.Visible);

                    if (value == LoadingIconType.Loading)
                    {
                        loadingIcon.Kind = MahApps.Metro.IconPacks.PackIconBoxIconsKind.RegularAnalyse;
                        loadingIcon.Spin = true;
                        isLoadingIcon = true;
                    }
                    else if (value == LoadingIconType.Stopped)
                    {
                        loadingIcon.Kind = MahApps.Metro.IconPacks.PackIconBoxIconsKind.RegularStopCircle;
                        loadingIcon.Spin = false;
                    }
                })));
            }
        }

        /// <summary>
        /// Receives images updates from the video worker and updates the cameraViewerImage
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void workerProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (isLoadingIcon)
                LoadingIconVisibility = LoadingIconType.None;

            var image = e.UserState as Mat;
            if (image == null) return;

            if (!image.IsDisposed)
                cameraViewerImage.Source = image.ToWriteableBitmap(PixelFormats.Bgr24);
        }

        /// <summary>
        /// Enable/disable UI
        /// </summary>
        /// <param name="enable"></param>
        private void ScreenEnableUI(bool enable)
        {
            controlsGrid.IsEnabled = startButton.IsEnabled = enable;
            stopButton.IsEnabled = !enable;
        }

        /// <summary>
        /// Shows an error for three seconds on the bottom left corner
        /// </summary>
        /// <param name="str">Error message</param>
        private void ShowError(string str)
        {
            warningLabel.Dispatcher.Invoke((new Action(() =>
            {

                warningLabel.Content = str;
                warningLabel.Visibility = Visibility.Visible;

            })));

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                warningLabel.Dispatcher.Invoke((new Action(() => warningLabel.Visibility = Visibility.Collapsed)));
            });
        }

        ScreenShare screenShare = null;

        bool isLoadingIcon = false;

        private async void startButton_Click(object sender, RoutedEventArgs e)
        {
            // Don't start if no cameras exists in our list
            if (videoInputComboBox.Items.Count == 0)
                return;

            // Don't start if the worker is already running
            if (screenShare != null)
                return;

            ScreenEnableUI(false);

            ScreenShare.ScreenShareType type = ScreenShare.ScreenShareType.Camera;

            // If a static image is selected we set it directly here
            if (videoInputComboBox.SelectedIndex == 1)
                type = ScreenShare.ScreenShareType.Image;
            // If it's a looping video we have to manually set the ScreenShare SelectedVideoSourcePath
            else if (videoInputComboBox.SelectedIndex == 2)
                type = ScreenShare.ScreenShareType.Video;

            // If we want to stream a camera we set a Loading Icon Type
            if (videoInputComboBox.SelectedIndex > 1)
                LoadingIconVisibility = LoadingIconType.Loading;
            else
                LoadingIconVisibility = LoadingIconType.None;


            // Create a new instance of ScreenShare
            screenShare = new ScreenShare(type, workerProgressChanged);

            switch (type)
            {
                case ScreenShare.ScreenShareType.Camera:
                    {
                        screenShare.SelectedCameraIndex = SelectedCameraIndex;
                    }
                    break;
                case ScreenShare.ScreenShareType.Video:
                    {
                        screenShare.SelectedVideoSourcePath = SelectedVideoSourcePath;
                    }
                    break;
                case ScreenShare.ScreenShareType.Image:
                    {
                        if (BackgroundImage != null)
                            cameraViewerImage.Source = BackgroundImage;
                    }
                    break;
            }

            // Set the audio output
            if (audioOutComboBox.SelectedIndex > 0)
            {
                screenShare.AudioOutDeviceIndex = audioOutComboBox.SelectedIndex - 1;

                // Set the audio inputs
                Dictionary<int, string> audioInputs = new Dictionary<int, string>();
                foreach (var item in audioInComboBoxPanel.Children)
                {
                    var audioInComboBox = (ComboBox)item;
                    int audioInDeviceIndex = audioInComboBox.SelectedIndex - 1;
                    audioInputs.Add(audioInDeviceIndex, audioInComboBox.Text);
                }

                screenShare.AudioInputs = audioInputs;
            }

            try
            {
                await screenShare.Start();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);

                await screenShare.Dispose().ContinueWith(_ =>
                {

                    screenShare = null;
                    LoadingIconVisibility = LoadingIconType.None;
                    ScreenEnableUI(true);

                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        /// <summary>
        /// Start processing audio and video
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void stopButton_Click(object sender, RoutedEventArgs e)
        {
            if (screenShare != null)
                await screenShare.Dispose().ContinueWith(_ =>
                {

                    // UI stuff
                    startButton.IsEnabled = true;
                    cameraViewerImage.Source = null;
                    LoadingIconVisibility = LoadingIconType.Stopped;
                    warningLabel.Visibility = Visibility.Collapsed;
                    screenShare = null;
                    ScreenEnableUI(true);

                }, TaskScheduler.FromCurrentSynchronizationContext()); ;
        }

        /// <summary>
        /// Stop all pending operations and close this program
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void closeButton_Click(object sender, RoutedEventArgs e)
        {
            if (screenShare != null)
                await screenShare.Dispose();

            this.Close();
        }

        /// <summary>
        /// Drag the window from anywhere
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        /// <summary>
        /// Double click to maximize/return to normal size
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (this.WindowState == System.Windows.WindowState.Normal)
                    this.WindowState = System.Windows.WindowState.Maximized;
                else
                    this.WindowState = System.Windows.WindowState.Normal;
            }
        }

        private bool uiVisibility = false;

        /// <summary>
        /// Show/Hide the UI
        /// </summary>
        private void ToggleUI()
        {
            uiVisibility = !uiVisibility;
            mainGrid.IsEnabled = uiVisibility;
            dockPanelBorder.BorderThickness = uiVisibility ? new Thickness(2) : new Thickness(0);
        }

        /// <summary>
        /// Calculate width based on height and x:y ratio
        /// </summary>
        /// <param name="x">x:y</param>
        /// <param name="y">x:y</param>
        /// <param name="height"></param>
        /// <returns></returns>
        private int CalculateRatio(int x, int y, int height)
        {
            return (x * height) / y;
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ToggleUI();
        }

        /// <summary>
        /// Resize the window to the selected aspect ratio
        /// </summary>
        /// <remarks>
        /// QND
        /// </remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResizeWindowButtonClick(object sender, RoutedEventArgs e)
        {
            int x = 16, y = 9;
            if (ratioComboBox.SelectedIndex == 2)
            {
                y = 10;
            }
            else if (ratioComboBox.SelectedIndex == 2)
            {
                x = 4; y = 3;
            }

            this.Width = CalculateRatio(x, y, (int)this.Height);
        }

        private void webSiteLabel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var targetURL = "https://mywk.net/software/screen-share-assist";
            var psi = new ProcessStartInfo
            {
                FileName = targetURL,
                UseShellExecute = true
            };
            Process.Start(psi);
        }

        /// <summary>
        /// Surpress the MouseDown event on controls
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mainGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void mainGrid_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount > 1)
                e.Handled = true;
        }

        /// <summary>
        /// Prevent double clicking here
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void bottomLabel_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void minimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void refreshAudioInPackIconControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RefreshAudioInputs();
        }
        private void refreshAudioOutPackIconControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RefreshAudioOutputs();
        }
        private void refreshVideoPackIconControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            RefreshVideoList();
        }

        /// <summary>
        /// Used for the static image if selected
        /// </summary>
        private BitmapImage BackgroundImage = null;

        /// <summary>
        /// Currently selected camera
        /// </summary>
        public int SelectedCameraIndex
        {
            get
            {
                return videoInputComboBox.SelectedIndex - 3;
            }
        }

        /// <summary>
        /// Currently selected video source
        /// </summary>
        private string selectedVideoSourcePath = "";
        public string SelectedVideoSourcePath
        {
            get
            {
                return selectedVideoSourcePath;
            }
            set
            {
                selectedVideoSourcePath = value;
            }
        }

        private void videoInputComboBox_DropDownClosed(object sender, EventArgs e)
        {
            BackgroundImage = null;

            // Image selection
            if (videoInputComboBox.SelectedIndex == 1)
            {
                videoInputComboBox.IsDropDownOpen = false;

                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;";
                dialog.Title = "Please select an image file.";


                Task.Run(() =>
                {
                    warningLabel.Dispatcher.Invoke((new Action(() =>
                    {

                        if (dialog.ShowDialog() == true)
                        {
                            if (System.IO.File.Exists(dialog.FileName))
                            {
                                BackgroundImage = new BitmapImage(new Uri(dialog.FileName));

                                Settings.Default.BackgroundImagePath = dialog.FileName;
                                Settings.Default.Save();
                            }
                        }

                    })));
                });
            }
            // Video selection
            else if (videoInputComboBox.SelectedIndex == 2)
            {
                videoInputComboBox.IsDropDownOpen = false;

                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Filter = "Video Files|*.mp4;";
                dialog.Title = "Please select a video file.";


                Task.Run(() =>
                {
                    warningLabel.Dispatcher.Invoke((new Action(() =>
                    {

                        if (dialog.ShowDialog() == true)
                        {
                            if (System.IO.File.Exists(dialog.FileName))
                                SelectedVideoSourcePath = dialog.FileName;

                            Settings.Default.BackgroundVideoPath = dialog.FileName;
                            Settings.Default.Save();
                        }

                    })));
                });
            }
        }

        private void AddNewAudioIn()
        {
            var audioInComboBox = new ComboBox() { Margin = new Thickness(0, 5, 0, 0) };
            audioInComboBox.SelectionChanged += AudioInComboBox_SelectionChanged;
            audioInComboBoxPanel.Children.Add(audioInComboBox);
        }

        private void addAudioInIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            AddNewAudioIn();
            RefreshAudioInputs();
        }

        /// <summary>
        /// Handle AudioIn ComboBox SelectionChanged events
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AudioInComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded || e.AddedItems.Count == 0 || e.AddedItems[0] == null)
                return;

            // QND way of removing audio in extra devices
            if (e.AddedItems.Count > 0)
            {
                if (e.AddedItems[0].ToString() == removeAudioInString)
                    audioInComboBoxPanel.Children.Remove((UIElement)sender);
                else if (e.AddedItems[0].ToString() != noneString)
                {
                    // Check for duplicated items
                    // Note: This could be improved, a bit QND too
                    foreach (var item in audioInComboBoxPanel.Children)
                    {
                        var str = ((item as ComboBox).SelectedItem as string);

                        if (str != noneString)
                        {
                            // Prevent selection if already used
                            if (Settings.Default.AudioInDeviceList != null && Settings.Default.AudioInDeviceList.Contains(e.AddedItems[0].ToString()))
                            {
                                (sender as ComboBox).SelectedIndex = 0;
                                e.AddedItems.Clear();
                                e.Handled = true;
                                break;
                            }
                        }
                    }
                }
            }

            audioInComboBoxPanel.IsEnabled = false;

            // Trigger saving the currently selected audio input devices
            saveAudioInDevicesTrigger++;
            SaveAudioInDevices();
        }

        private int saveAudioInDevicesTrigger = 0;
        private void SaveAudioInDevices()
        {
            Task.Run(() =>
            {

                int originalTrigger = saveAudioInDevicesTrigger;

                // Save aproximately 200 ms after the last change
                for (int i = 0; i < 20; i++)
                {
                    if (originalTrigger != saveAudioInDevicesTrigger)
                        return;

                    Thread.Sleep(10);
                }

                this.Dispatcher.Invoke((new Action(() =>
                {
                    StringCollection audioInputs = new StringCollection();
                    foreach (var item in audioInComboBoxPanel.Children)
                    {
                        var audioInComboBox = (ComboBox)item;
                        audioInputs.Add(audioInComboBox.Text);
                    }
                    Settings.Default.AudioInDeviceList = audioInputs;
                    Settings.Default.Save();

                    audioInComboBoxPanel.IsEnabled = true;
                })));

                saveAudioInDevicesTrigger = 0;

            });

        }

        private void topToggleButton_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;
            Settings.Default.TopMost = this.Topmost;
            Settings.Default.Save();
        }

        /// <summary>
        /// Tray notify icon
        /// </summary>
        System.Windows.Forms.NotifyIcon notifyIcon = null;
        private void hideToTaskbarButton_Click(object sender, RoutedEventArgs e)
        {
            // Create our notify icon if it's still null
            if(notifyIcon == null)
            {
                notifyIcon = new System.Windows.Forms.NotifyIcon();
                notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
                notifyIcon.Text = "ScreenShareAssist - Restore window";
                notifyIcon.Click += NotifyIcon_Click;

                notifyIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info;
                notifyIcon.BalloonTipTitle = "ScreenShareAssist";
                notifyIcon.BalloonTipText = "Window is now hidden, click the tray bar icon to restore it.";
            }

            if (!windowOutOfSight)
            {
                // Hide the UI if currently visible
                if (uiVisibility)
                    ToggleUI();

                windowOutOfSight = true;
                this.Left = -9999;
                this.Top = -9999;
                this.ShowInTaskbar = false;

                notifyIcon.Visible = true;
                notifyIcon.ShowBalloonTip(2000);
            }
            else
            {
                this.Left = Settings.Default.WindowLeft;
                this.Top = Settings.Default.WindowTop;
                windowOutOfSight = false;

                this.ShowInTaskbar = true;
                notifyIcon.Visible = false;
            }
        }

        private void NotifyIcon_Click(object sender, EventArgs e)
        {
            hideToTaskbarButton_Click(sender, null);
        }

        #region Settings

        /// <summary>
        /// Load last saved settings if any
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                // Window size and position
                {
                    if (Settings.Default.WindowSize.Width != 0)
                    {
                        this.Width = Settings.Default.WindowSize.Width;
                        this.Height = Settings.Default.WindowSize.Height;
                    }

                    if (Settings.Default.WindowLeft != 0)
                        this.Left = Settings.Default.WindowLeft;

                    if (Settings.Default.WindowTop != 0)
                        this.Top = Settings.Default.WindowTop;
                }

                // Last selected audio out and video
                {
                    if (Settings.Default.SelectedAudioOut != "")
                    {
                        foreach (var item in audioOutComboBox.Items)
                        {
                            if ((item as string) == Settings.Default.SelectedAudioOut)
                            {
                                audioOutComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }

                    if (Settings.Default.SelectedVideo != "")
                    {
                        foreach (var item in videoInputComboBox.Items)
                        {
                            if ((item as string) == Settings.Default.SelectedVideo)
                            {
                                videoInputComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                }

                // Load static image and video loop settings
                {
                    if (Settings.Default.BackgroundVideoPath != "")
                    {
                        if (File.Exists(Settings.Default.BackgroundVideoPath))
                            SelectedVideoSourcePath = Settings.Default.BackgroundVideoPath;
                    }

                    if (Settings.Default.BackgroundImagePath != "")
                    {
                        if (File.Exists(Settings.Default.BackgroundImagePath))
                        {
                            BackgroundImage = new BitmapImage(new Uri(Settings.Default.BackgroundImagePath));
                        }
                    }
                }

                // Load audio in devices
                if (Settings.Default.AudioInDeviceList != null && Settings.Default.AudioInDeviceList.Count > 0)
                {
                    for (int i = 1; i < Settings.Default.AudioInDeviceList.Count; i++)
                        AddNewAudioIn();

                    RefreshAudioInputs();

                    for (int i = 0; i < audioInComboBoxPanel.Children.Count; i++)
                    {
                        string name = Settings.Default.AudioInDeviceList[i];
                        var item = audioInComboBoxPanel.Children[i];

                        var audioInComboBox = (ComboBox)item;

                        foreach (var comboBoxItem in audioInComboBox.Items)
                        {
                            if ((comboBoxItem as string) == name)
                            {
                                audioInComboBox.SelectedItem = name;
                                break;
                            }
                        }
                    }
                }

                // Topmost
                this.Topmost = Settings.Default.TopMost;
            }
            catch (Exception)
            {
                ShowError("An error occurred while attempting to load the last saved settings.");
            }
        }

        /// <summary>
        /// Save last known window size
        /// </summary>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!settingsLoaded) return;

            Settings.Default.WindowSize = e.NewSize;
            Settings.Default.Save();
        }

        /// <summary>
        /// Save last known window position
        /// </summary>
        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (!settingsLoaded || windowOutOfSight) return;

            Settings.Default.WindowLeft = Application.Current.MainWindow.Left;
            Settings.Default.WindowTop = Application.Current.MainWindow.Top;
            Settings.Default.Save();
        }

        private void audioOutComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;

            if (e.AddedItems.Count > 0)
            {
                Settings.Default.SelectedAudioOut = e.AddedItems[0].ToString();
                Settings.Default.Save();
            }
        }

        private void videoInputComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!settingsLoaded) return;

            if (e.AddedItems.Count > 0)
            {
                Settings.Default.SelectedVideo = e.AddedItems[0].ToString();
                Settings.Default.Save();
            }
        }

        #endregion

    }
}
