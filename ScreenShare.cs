/* Copyright (C) 2021 - Mywk.Net
 * Licensed under the EUPL, Version 1.2
 * You may obtain a copy of the Licence at: https://joinup.ec.europa.eu/community/eupl/og_page/eupl
 * Unless required by applicable law or agreed to in writing, software distributed under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * 
 * Code related to NAudio (MIT License) and OpenCvSharp (Apache-2.0 License) is licensed under their respective licenses.
 */
using NAudio.Wave;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenShareAssist
{
    /// <summary>
    /// Manages video and audio
    /// </summary>
    public class ScreenShare
    {
        /// <summary>
        /// Camera worker
        /// </summary>
        private BackgroundWorker _worker;

        /// <summary>
        /// This allows us to force abort our camera worker
        /// </summary>
        private bool abortRequested = false;

        /// <summary>
        /// Whenever the worker has started
        /// </summary>
        private bool workerStarted = false;

        /// <summary>
        /// Whenever the worker finished
        /// </summary>
        private bool workerFinished = false;


        /// <summary>
        /// WaveInMixer list
        /// </summary>
        private List<WaveInMixer> waveInMixerList = new List<WaveInMixer>();

        /// <summary>
        /// Our audio output
        /// </summary>
        WaveOut waveOut = null;

        public enum ScreenShareType
        {
            Camera,
            Video,
            Image
        }

        private ScreenShareType Type;

        private int videoInput;
        public int VideoInput
        {
            get
            {
                return videoInput;
            }
            set
            {
                videoInput = value;
            }
        }

        /// <summary>
        /// Selected video source
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

        /// <summary>
        /// Selected camera
        /// </summary>
        private int selectedCameraIndex = -1;
        public int SelectedCameraIndex
        {
            get
            {
                return selectedCameraIndex;
            }
            set
            {
                selectedCameraIndex = value;
            }
        }

        /// <summary>
        /// Selected audio out
        /// </summary>
        private int audioOutDeviceIndex = -1;
        public int AudioOutDeviceIndex
        {
            get
            {
                return audioOutDeviceIndex;
            }
            set
            {
                audioOutDeviceIndex = value;
            }
        }

        /// <summary>
        /// Audio inputs
        /// </summary>
        Dictionary<int, string> audioInputs = null;
        public Dictionary<int, string> AudioInputs
        {
            get
            {
                return audioInputs;
            }
            set
            {
                audioInputs = value;
            }
        }

        /// <summary>
        /// ScreenShare constructor
        /// </summary>
        /// <param name="type">Type of ScreenShareType (camera, looping video or static image)</param>
        /// <param name="workerProgressChanged">Worker event handler that gives a Mat object type to write to a bitmap</param>
        public ScreenShare(ScreenShareType type, ProgressChangedEventHandler workerProgressChanged)
        {
            this.Type = type;

            // If we want work with video we start its worker
            if (type != ScreenShareType.Image)
            {
                _worker = new BackgroundWorker
                {
                    WorkerReportsProgress = true,
                    WorkerSupportsCancellation = true
                };

                _worker.DoWork += workerDoReadStream;

                _worker.ProgressChanged += workerProgressChanged;
                _worker.RunWorkerCompleted += workerRunWorkerCompleted;
            }
        }

        /// <summary>
        /// Used for video capture
        /// </summary>
        VideoCapture capture = null;

        /// <summary>
        /// VideoCapture interval for our worker
        /// </summary>
        int videoCaptureInterval = 0;


        /// <summary>
        /// Starts the worker 
        /// </summary>
        /// <remarks>
        /// Throws an error if it wasn't successful
        /// </remarks>
        public async Task Start()
        {
            if (Type != ScreenShareType.Image)
            {
                // Use DSHOW as the Video Capture API
                VideoCaptureAPIs selectedAPI = VideoCaptureAPIs.DSHOW;

                // Find best VideoCaptureAPI
                // Removed as DSHOW seems to suit most use cases
                //foreach (VideoCaptureAPIs api in (VideoCaptureAPIs[])Enum.GetValues(typeof(VideoCaptureAPIs)))
                //{
                //    if (api == VideoCaptureAPIs.ANY)
                //        continue;

                //    using (var capture = new VideoCapture(selectedCamera - 1, api))
                //    {
                //        if (capture.IsOpened())
                //        {
                //            selectedAPI = api;
                //            break;
                //        }
                //    }
                //}

                // If the selected camera index is zero we are dealing with a file, otherwise it's a video input
                if (Type == ScreenShareType.Video)
                    capture = new VideoCapture(SelectedVideoSourcePath);
                else
                    capture = new VideoCapture(SelectedCameraIndex, selectedAPI);


                // Set the video capture to FHD
                capture.Set(VideoCaptureProperties.FrameWidth, 1920);
                capture.Set(VideoCaptureProperties.FrameHeight, 1080);

                double fps = 0;

                if (Type == ScreenShareType.Camera)
                {
                    await Task.Run(() => fps = Helper.VideoUtilities.GetFps(capture));
                    capture.Fps = fps;
                }
                else
                    fps = capture.Fps;

                if (fps <= 0)
                {
                    throw new Exception("Unable to start the selected video input.");
                }
                else
                {
                    videoCaptureInterval = (int)(1000 / fps);
                }
            }

            // Start audio processing if necessary
            // Note: This needs to be re-written and improved
            if (AudioOutDeviceIndex >= 0 && AudioInputs != null)
            {
                var mixer = new WaveMixerStream32 { AutoStop = false };

                foreach (var item in AudioInputs)
                {
                    try
                    {
                        WaveInMixer waveInMixer = new WaveInMixer(item.Key);
                        waveInMixerList.Add(waveInMixer);

                        waveInMixer.StartRecording();
                        mixer.AddInputStream(waveInMixer.GetInputStream());
                    }
                    catch (Exception)
                    {
                        mixer.Dispose();

                        throw new Exception("Unable to start audio-in: " + item.Value);
                    }
                }

                waveOut = new WaveOut();
                waveOut.DeviceNumber = audioOutDeviceIndex;
                waveOut.Init(mixer);

                waveOut.Play();
            }

            if (Type != ScreenShareType.Image)
                _worker.RunWorkerAsync();
        }

        private void workerDoReadStream(object sender, DoWorkEventArgs e)
        {
            workerStarted = true;

            // Force clean GC once in a while
            int gcCleanCounter = 0;

            using (var image = new Mat())
            {
                while (_worker != null && !_worker.CancellationPending && !abortRequested)
                {
                    capture.Read(image);
                    if (image.Empty())
                    {
                        if (Type == ScreenShareType.Camera)
                            break;
                        else
                        {
                            // Loop video file
                            capture.Set(VideoCaptureProperties.PosFrames, 0);
                            continue;
                        }
                    }

                    _worker.ReportProgress(0, image);
                    Thread.Sleep(videoCaptureInterval);

                    // The GC clean counter force a immediate garbage collection once in a while to prevent too much RAM usage
                    gcCleanCounter++;
                    if (gcCleanCounter > 30)
                    {
                        GC.Collect();
                        gcCleanCounter = 0;
                    }
                }
            }

            capture.Dispose();

            workerFinished = true;
        }

        private void workerRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _worker.Dispose();
            _worker = null;
            workerFinished = true;
        }

        BufferedWaveProvider bufferedWaveProvider = null;

        private void waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
            byte[] WaveIn_Data = e.Buffer;
        }

        /// <summary>
        /// Dispose of the worker if necessary
        /// </summary>
        public async System.Threading.Tasks.Task Dispose()
        {
            if (waveOut != null)
            {
                waveOut.Stop();

                foreach (var mixer in waveInMixerList)
                    mixer.Dispose();

                waveInMixerList.Clear();
                waveOut = null;
            }

            abortRequested = true;

            // Wait for worker to finish, 10 seconds timeout, just in case
            int emergencyTimeout = 1000;
            while (!workerFinished && workerStarted)
            {
                await System.Threading.Tasks.Task.Delay(10);
                emergencyTimeout--;

                if (emergencyTimeout <= 0)
                {
                    if (_worker != null)
                    {
                        _worker.CancelAsync();
                        _worker.Dispose();
                    }

                    break;
                }
            }

            GC.Collect();
        }


    }
}
