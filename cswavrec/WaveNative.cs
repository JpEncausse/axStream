//  THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
//  KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
//  IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR
//  PURPOSE.
//
//  This material may not be duplicated in whole or in part, except for 
//  personal use, without the express written consent of the author. 
//
//  Email:  ianier@hotmail.com
//
//  Copyright (C) 1999-2003 Ianier Munoz. All Rights Reserved.

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace WaveLib
{
	public enum WaveFormats
	{
		Pcm = 1,
		Float = 3
	}

	[StructLayout(LayoutKind.Sequential)] 
	public class WaveFormat
	{
		public short wFormatTag;
		public short nChannels;
		public int nSamplesPerSec;
		public int nAvgBytesPerSec;
		public short nBlockAlign;
		public short wBitsPerSample;
		public short cbSize;

		public WaveFormat(int rate, int bits, int channels)
		{
			wFormatTag = (short)WaveFormats.Pcm;
			nChannels = (short)channels;
			nSamplesPerSec = rate;
			wBitsPerSample = (short)bits;
			cbSize = 0;
               
			nBlockAlign = (short)(channels * (bits / 8));
			nAvgBytesPerSec = nSamplesPerSec * nBlockAlign;
		}
	}

	internal class WaveNative
	{
		// consts
		public const int MMSYSERR_NOERROR = 0; // no error

		public const int MM_WOM_OPEN = 0x3BB;
		public const int MM_WOM_CLOSE = 0x3BC;
		public const int MM_WOM_DONE = 0x3BD;

		public const int MM_WIM_OPEN = 0x3BE;
		public const int MM_WIM_CLOSE = 0x3BF;
		public const int MM_WIM_DATA = 0x3C0;

		public const int CALLBACK_FUNCTION = 0x00030000;    // dwCallback is a FARPROC 

		public const int TIME_MS = 0x0001;  // time in milliseconds 
		public const int TIME_SAMPLES = 0x0002;  // number of wave samples 
		public const int TIME_BYTES = 0x0004;  // current byte offset 

		// callbacks
		public delegate void WaveDelegate(IntPtr hdrvr, int uMsg, int dwUser, ref WaveHdr wavhdr, int dwParam2);

		// structs 

		[StructLayout(LayoutKind.Sequential)] public struct WaveHdr
		{
			public IntPtr lpData; // pointer to locked data buffer
			public int dwBufferLength; // length of data buffer
			public int dwBytesRecorded; // used for input only
			public IntPtr dwUser; // for client's use
			public int dwFlags; // assorted flags (see defines)
			public int dwLoops; // loop control counter
			public IntPtr lpNext; // PWaveHdr, reserved for driver
			public int reserved; // reserved for driver
		}

		private const string mmdll = "winmm.dll";

		// WaveOut calls
		[DllImport(mmdll)]
		public static extern int waveOutGetNumDevs();
		[DllImport(mmdll)]
		public static extern int waveOutPrepareHeader(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);
		[DllImport(mmdll)]
		public static extern int waveOutUnprepareHeader(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);
		[DllImport(mmdll)]
		public static extern int waveOutWrite(IntPtr hWaveOut, ref WaveHdr lpWaveOutHdr, int uSize);
		[DllImport(mmdll)]
		public static extern int waveOutOpen(out IntPtr hWaveOut, int uDeviceID, WaveFormat lpFormat, WaveDelegate dwCallback, int dwInstance, int dwFlags);
		[DllImport(mmdll)]
		public static extern int waveOutReset(IntPtr hWaveOut);
		[DllImport(mmdll)]
		public static extern int waveOutClose(IntPtr hWaveOut);
		[DllImport(mmdll)]
		public static extern int waveOutPause(IntPtr hWaveOut);
		[DllImport(mmdll)]
		public static extern int waveOutRestart(IntPtr hWaveOut);
		[DllImport(mmdll)]
		public static extern int waveOutGetPosition(IntPtr hWaveOut, out int lpInfo, int uSize);
		[DllImport(mmdll)]
		public static extern int waveOutSetVolume(IntPtr hWaveOut, int dwVolume);
		[DllImport(mmdll)]
		public static extern int waveOutGetVolume(IntPtr hWaveOut, out int dwVolume);

		// WaveIn calls
		[DllImport(mmdll)]
		public static extern int waveInGetNumDevs();
		[DllImport(mmdll)]
		public static extern int waveInAddBuffer(IntPtr hwi, ref WaveHdr pwh, int cbwh);
		[DllImport(mmdll)]
		public static extern int waveInClose(IntPtr hwi);
		[DllImport(mmdll)]
		public static extern int waveInOpen(out IntPtr phwi, int uDeviceID, WaveFormat lpFormat, WaveDelegate dwCallback, int dwInstance, int dwFlags);
		[DllImport(mmdll)]
		public static extern int waveInPrepareHeader(IntPtr hWaveIn, ref WaveHdr lpWaveInHdr, int uSize);
		[DllImport(mmdll)]
		public static extern int waveInUnprepareHeader(IntPtr hWaveIn, ref WaveHdr lpWaveInHdr, int uSize);
		[DllImport(mmdll)]
		public static extern int waveInReset(IntPtr hwi);
		[DllImport(mmdll)]
		public static extern int waveInStart(IntPtr hwi);
		[DllImport(mmdll)]
		public static extern int waveInStop(IntPtr hwi);

       [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct WaveInCaps
        {
            public short wMid;
            public short wPid;
            public int vDriverVersion;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public char[] szPname;
            public uint dwFormats;
            public short wChannels;
            public short wReserved1;
        }
      
        [DllImport("winmm.dll", EntryPoint = "waveInGetDevCaps")]
        public static extern int waveInGetDevCaps(int uDeviceID, ref WaveInCaps lpCaps, int uSize);

        //http://blogs.msdn.com/b/matthew_van_eerde/archive/2012/03/13/sample-how-to-enumerate-wavein-and-waveout-devices-on-your-system.aspx
        //http://www.codeproject.com/Articles/18685/Enumerating-Sound-Recording-Devices-Using-winmm-dl?fid=415295&df=90&mpp=10&noise=1&prof=True&sort=Position&view=Quick&spc=Relaxed&fr=11
        public static List<string> EnumInputDevices()
        {
            List<string> InputDeviceNames = new List<string>();
            int waveInDevicesCount = waveInGetNumDevs(); //get total
            if (waveInDevicesCount > 0)
            {
                for (int uDeviceID = 0; uDeviceID < waveInDevicesCount; uDeviceID++)
                {
                    WaveInCaps waveInCaps = new WaveInCaps();
                    waveInGetDevCaps(uDeviceID, ref waveInCaps, Marshal.SizeOf(typeof(WaveInCaps)));
                    string devnameandid = "Device ID " + uDeviceID + ": " + new string(waveInCaps.szPname);
                    InputDeviceNames.Add(devnameandid.Remove(devnameandid.IndexOf('\0')).Trim());
                }
            }
            return InputDeviceNames;
        }
    }
}
