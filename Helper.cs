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
using System.Diagnostics;
using System.Text;

namespace ScreenShareAssist
{
    /// <summary>
    /// Video and Audio Utilities
    /// </summary>
    public class Helper
    {
        public class VideoUtilities
        {
            /// <summary>
            /// Get them frames
            /// </summary>
            /// <remarks>
            /// Taken from OpenCVSharp Sample 06
            /// </remarks>
            /// <param name="capture"></param>
            /// <returns>FPS or -1 if an error ocurred</returns>
            public static double GetFps(VideoCapture capture)
            {
                var watch = Stopwatch.StartNew();
                double seconds = 0;

                using (var image = new Mat())
                {
                    while (true)
                    {
                        /* start camera */
                        capture.Read(image);
                        if (!image.Empty())
                        {
                            break;
                        }
                        else
                        {
                            seconds = watch.ElapsedMilliseconds / (double)1000;
                            if (seconds >= 10)
                            {
                                watch.Stop();
                                return -1;
                            }
                        }
                    }
                }

                using (var image = new Mat())
                {
                    double counter = 0;
                    seconds = 0;
                    watch = Stopwatch.StartNew();
                    while (true)
                    {
                        capture.Read(image);
                        if (image.Empty())
                        {
                            break;
                        }

                        counter++;
                        seconds = watch.ElapsedMilliseconds / (double)1000;
                        if (seconds >= 3)
                        {
                            watch.Stop();
                            break;
                        }
                    }
                    var fps = counter / seconds;
                    return fps;
                }
            }
        }

        /// <summary>
        /// Converts a WaveProvider to a WaveStream
        /// </summary>
        public class WaveProviderToWaveStream : WaveStream
        {
            private readonly IWaveProvider source;

            public WaveProviderToWaveStream(IWaveProvider source)
            {
                this.source = source;
            }

            public override WaveFormat WaveFormat
            {
                get { return source.WaveFormat; }
            }

            /// <summary>
            /// Source length is unknown so we return Int32.Max instead
            /// </summary>
            public override long Length
            {
                get { return Int32.MaxValue; }
            }

            public override long Position
            {
                get
                {
                    // Always return 0 as the position will be continuous
                    return 0;
                }
                set
                {
                    // Do not set anything :)
                }
            }

            /// <summary>
            /// Read from the available buffer
            /// </summary>
            /// <param name="buffer">Buffer to read to</param>
            /// <param name="offset">offset to read</param>
            /// <param name="count">Number of bytes to read</param>
            /// <returns>Number of bytes read</returns>
            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = source.Read(buffer, offset, count);
                return read;
            }
        }
    }
}
