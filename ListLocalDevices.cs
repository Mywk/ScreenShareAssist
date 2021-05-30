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
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace ScreenShareAssist
{
    /// <summary>
    /// Class to list Video/Audio devices using either ClassEnumerator or NAudio
    /// </summary>
    class ListLocalDevices
    {
        internal static readonly Guid SystemDeviceEnum = new Guid(0x62BE5D10, 0x60EB, 0x11D0, 0xBD, 0x3B, 0x00, 0xA0, 0xC9, 0x11, 0xCE, 0x86);
        internal static readonly Guid VideoInputDevice = new Guid(0x860BB310, 0x5D01, 0x11D0, 0xBD, 0x3B, 0x00, 0xA0, 0xC9, 0x11, 0xCE, 0x86); 
        internal static readonly Guid AudioCaptureSources = new Guid(0x33D9A762, 0x90C8, 0x11D0, 0xBD, 0x43, 0x00, 0xA0, 0xC9, 0x11, 0xCE, 0x86);
        internal static readonly Guid AudioRenderers = new Guid(0xE0F158E1, 0xCB04, 0x11D0, 0xBD, 0x4E, 0x00, 0xA0, 0xC9, 0x11, 0xCE, 0x86);

        [ComImport, Guid("55272A00-42CB-11CE-8135-00AA004BB851"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPropertyBag
        {
            [PreserveSig]
            int Read(
                [In, MarshalAs(UnmanagedType.LPWStr)] string propertyName,
                [In, Out, MarshalAs(UnmanagedType.Struct)] ref object pVar,
                [In] IntPtr pErrorLog);
            [PreserveSig]
            int Write(
                [In, MarshalAs(UnmanagedType.LPWStr)] string propertyName,
                [In, MarshalAs(UnmanagedType.Struct)] ref object pVar);
        }

        [ComImport, Guid("29840822-5B84-11D0-BD3B-00A0C911CE86"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface ICreateDevEnum
        {
            [PreserveSig]
            int CreateClassEnumerator([In] ref Guid type, [Out] out IEnumMoniker enumMoniker, [In] int flags);
        }

        enum DeviceType
        {
            Cameras,
            AudioInput,
            AudioRenderer
        }

        /// <summary>
        /// Get devices by the given type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private static List<string> GetDevices(DeviceType type)
        {
            List<string> devices = new List<string>();

            Object bagObj = null;
            object comObj = null;
            ICreateDevEnum enumDev = null;
            IEnumMoniker enumMon = null;
            IMoniker[] moniker = new IMoniker[100];
            IPropertyBag bag = null;
            try
            {
                // Get the system device enumerator
                Type srvType = Type.GetTypeFromCLSID(SystemDeviceEnum);

                // Create the device enumerator
                comObj = Activator.CreateInstance(srvType);
                enumDev = (ICreateDevEnum)comObj;

                // Create an enumerator to find devices of the category
                enumDev.CreateClassEnumerator((type == DeviceType.Cameras ? VideoInputDevice : AudioCaptureSources), out enumMon, 0);
                Guid bagId = typeof(IPropertyBag).GUID;
                while (enumMon.Next(1, moniker, IntPtr.Zero) == 0)
                {
                    moniker[0].BindToStorage(null, null, ref bagId, out bagObj);
                    bag = (IPropertyBag)bagObj;
                    object val = "";
                    bag.Read("FriendlyName", ref val, IntPtr.Zero);
                    
                    // Add to our list
                    devices.Add((string)val);
                }

            }
            catch (Exception)
            {
            }
            finally
            {
                bag = null;
                if (bagObj != null)
                {
                    Marshal.ReleaseComObject(bagObj);
                    bagObj = null;
                }
                enumDev = null;
                enumMon = null;
                moniker = null;
            }

            return devices;
        }

        /// <summary>
        /// Retrieves a list of available cameras
        /// </summary>
        /// <returns></returns>
        public static List<string> GetCameras()
        {
            return GetDevices(DeviceType.Cameras);
        }

        /// <summary>
        /// Retrieves a list of available input sources
        /// </summary>
        /// <param name="nAudioOnly">Use NAudio instead of the Class enumerator</param>
        /// <returns></returns>
        public static List<string> GetAudioInputSources(bool nAudioOnly = true)
        {
            if (!nAudioOnly)
                return GetDevices(DeviceType.AudioInput);

            List<string> audioInputSources = new List<string>();

            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                var caps = WaveIn.GetCapabilities(n);
                audioInputSources.Add(caps.ProductName);
            }

            return audioInputSources;
        }


        /// <summary>
        /// Retrieves a list of available output sources
        /// </summary>
        /// <param name="nAudioOnly">Use NAudio instead of the Class enumerator</param>
        /// <returns></returns>
        public static List<string> GetAudioOutputSources(bool nAudioOnly = true)
        {
            if (!nAudioOnly)
                return GetDevices(DeviceType.AudioRenderer);

            List<string> audioOutputSources = new List<string>();

            for (int n = 0; n < WaveOut.DeviceCount; n++)
            {
                var caps = WaveOut.GetCapabilities(n);
                audioOutputSources.Add(caps.ProductName);
            }

            return audioOutputSources;
        }

    }
}
