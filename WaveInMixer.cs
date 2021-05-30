/* Copyright (C) 2021 - Mywk.Net
 * Licensed under the EUPL, Version 1.2
 * You may obtain a copy of the Licence at: https://joinup.ec.europa.eu/community/eupl/og_page/eupl
 * Unless required by applicable law or agreed to in writing, software distributed under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * 
 * Code related to NAudio (MIT License) and OpenCvSharp (Apache-2.0 License) is licensed under their respective licenses.
 */
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScreenShareAssist
{
    /// <summary>
    /// Used to capture WaveIn for usage in a Mixer
    /// </summary>
    public class WaveInMixer
    {
        /// <summary>
        /// Audio related
        /// </summary>
        WaveIn sourceStream = null;
        WaveChannel32 channel = null;
        BufferedWaveProvider bufferedWaveProvider = null;
        Helper.WaveProviderToWaveStream waveProvider = null;
        Wave16ToFloatProvider wave16ToFloatProvider = null;

        // TODO: Add more audio formats capabilities
        int globalWaveFormat = 44100;

        private WaveInCapabilities waveInCapabilities;
        public WaveInCapabilities WaveInCapabilities { get { return waveInCapabilities; } private set { waveInCapabilities = value; } }

        public WaveInMixer(int audioInDeviceIndex)
        {
            waveInCapabilities = WaveIn.GetCapabilities(audioInDeviceIndex);

            sourceStream = new WaveIn();
            sourceStream.DeviceNumber = audioInDeviceIndex;
            sourceStream.WaveFormat = new WaveFormat(globalWaveFormat, waveInCapabilities.Channels);

            sourceStream.DataAvailable += SourceStream_DataAvailable;

            bufferedWaveProvider = new BufferedWaveProvider(sourceStream.WaveFormat) { DiscardOnBufferOverflow = true };
            wave16ToFloatProvider = new Wave16ToFloatProvider(bufferedWaveProvider);
            waveProvider = new Helper.WaveProviderToWaveStream(wave16ToFloatProvider);

            channel = new WaveChannel32(waveProvider);
        }

        private void SourceStream_DataAvailable(object sender, WaveInEventArgs e)
        {
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

            // TODO: Clean buffer once in a while
        }

        public void Dispose()
        {
            if (sourceStreamRecording)
                StopRecording();

            wave16ToFloatProvider = null;
            waveProvider = null;
            bufferedWaveProvider = null;
            channel = null;

            sourceStream.Dispose();
            sourceStream = null;

            // Force GC clean
            GC.Collect();
        }

        bool sourceStreamRecording = false;

        /// <summary>
        /// Start souce stream recording
        /// </summary>
        public void StartRecording()
        {
            sourceStreamRecording = true;
            sourceStream.StartRecording();
        }

        /// <summary>
        /// Stop source stream recording
        /// </summary>
        public void StopRecording()
        {
            sourceStreamRecording = false;
            sourceStream.StopRecording();
        }

        /// <summary>
        /// Gets the input stream ready for usage in a WaveMixerStream32
        /// </summary>
        /// <returns>WaveChannel32</returns>
        public WaveChannel32 GetInputStream()
        {
            return channel;
        }
    }
}
