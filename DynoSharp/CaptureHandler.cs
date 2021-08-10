#nullable enable
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using DynoSharp.Interop;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;

namespace DynoSharp
{

    internal static class CaptureHandler
    {

        private static Direct3D11CaptureFramePool CaptureFramePool = null!;
        private static GraphicsCaptureItem CaptureItem = null!;
        private static GraphicsCaptureSession CaptureSession = null!;
        
        private static readonly Device CaptureDevice = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
        
        public static bool FrameCaptured { get; private set; }
        public static bool IsCapturing { get; private set; }
        
        public static Device GraphicCaptureDevice()
        {
            return CaptureDevice;
        }

        public static void Start(string processName, string windowName)
        {
            StartWindowCapture(GetWindowHandle(processName, windowName));
        }
        
        public static void Stop()
        {
            CaptureSession.Dispose();
            CaptureFramePool.Dispose();
            CaptureSession = null!;
            CaptureFramePool = null!;
            CaptureItem = null!;
            IsCapturing = false;
        }

        private static IntPtr GetWindowHandle(string processName, string windowName)
        {
            Process[] processPool = Process.GetProcessesByName(processName);
            
            foreach (Process process in processPool) {
                if (process.MainWindowTitle.Equals(windowName)) return process.MainWindowHandle;
            }

            #region Error Processing
            string processNameList = "";
            int i = 0;
            foreach (Process process in processPool) {
                processNameList += "(" + process.ProcessName + ", " + process.MainWindowTitle + ")";
                if (i != processPool.Length - 1) {
                    processNameList += ", ";
                }

                i++;
            }
            string error = "Process with specified window name was not found. Total processes with process name: "
                           + processPool.Length + ". Did you mean one of these: [" + processNameList + "]";
            throw new InvalidProgramException(error);
            #endregion
        }

        private static IDirect3DDevice CreateCaptureDevice()
        {
            uint direct3D11DevicePointer = NativeMethodHandler.CreateDirect3D11DeviceFromDXGIDevice(
                CaptureDevice.NativePointer,
                out IntPtr graphicDevice
            );

            if (direct3D11DevicePointer != 0) {
                throw new InvalidProgramException("Native pointer pointed to wrong device.");
            }

            IDirect3DDevice windowsRuntimeDevice = (IDirect3DDevice)Marshal.GetObjectForIUnknown(graphicDevice) ??
                                                   throw new InvalidCastException();
            Marshal.Release(graphicDevice);

            return windowsRuntimeDevice;
        }

        private static void StartWindowCapture(IntPtr windowHandle)
        {
            #region Generate Capture Requirements
            
            CaptureItem = CreateItemForWindow(windowHandle);
            CaptureItem.Closed += CaptureItemOnClosed;
            
            IDirect3DDevice windowsRuntimeDevice = CreateCaptureDevice();
            
            #endregion

            #region Use Windows Graphics Capture API
            
            CaptureFramePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                windowsRuntimeDevice, 
                DirectXPixelFormat.B8G8R8A8UIntNormalized, 
                60, // total frames in frame pool
                CaptureItem.Size // size of each frame
                );
            
            CaptureSession = CaptureFramePool.CreateCaptureSession(CaptureItem);
            
            CaptureFramePool.FrameArrived += (sender, arguments) =>
            {
                AddFrame();
            };
            
            CaptureSession.StartCapture();
            IsCapturing = true;

            #endregion
        }

        private static void AddFrame()
        {
            /*
            if (Stopwatch.IsRunning && FrameCaptured) {
                Stopwatch.Stop();

                CaptureFps = (uint)(1000 / Stopwatch.ElapsedMilliseconds);
                // Console.WriteLine("FPS: " + CaptureFps);
            }
            */

            FramePool.FreeRuntimeResources();
            // LatestFrame?.Dispose();
            // LatestFrame = GetNextFrame();
            FramePool.AddFrame(GetNextFrame());
            FrameCaptured = true;
            // Stopwatch.Reset();
            // Stopwatch.Start();
        }

        private static Direct3D11CaptureFrame GetNextFrame()
        {
            if (!IsCapturing) throw new InvalidOperationException("Can't get frame without capture process.");
            return CaptureFramePool.TryGetNextFrame();
        }

        private static GraphicsCaptureItem CreateItemForWindow(IntPtr highlightedWindow)
        {
            IActivationFactory factory = WindowsRuntimeMarshal.GetActivationFactory(typeof(GraphicsCaptureItem));
            // ReSharper disable once SuspiciousTypeConversion.Global
            IGraphicsCaptureItemInterop interop = (IGraphicsCaptureItemInterop)factory;
            Type graphicsCaptureItemInterface = typeof(GraphicsCaptureItem).GetInterface("IGraphicsCaptureItem") ??
                                                throw new InvalidCastException();
            IntPtr pointer = interop.CreateForWindow(highlightedWindow,
                graphicsCaptureItemInterface.GUID);
            GraphicsCaptureItem capture = Marshal.GetObjectForIUnknown(pointer) as GraphicsCaptureItem ?? 
                                          throw new InvalidCastException();
            Marshal.Release(pointer);
            return capture;
        }
        
        private static void CaptureItemOnClosed(GraphicsCaptureItem sender, object eventArgs)
        {
            Stop();
        }

    }

}