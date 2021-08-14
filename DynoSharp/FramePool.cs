#nullable enable
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using DynoSharp.Interop;
using OpenCvSharp;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace DynoSharp
{

    internal static class FramePool
    {
        
        private static Direct3D11CaptureFrame? LatestFrame;
        private static readonly Guid ResourceGuid = new Guid("DC8E63F3-D12B-4952-B47B-5E45026A862D");
        private static long Texture2DTime;
        private static long ByteArrayTime;
        private static long MatTime;
        
        private static Direct3D11CaptureFrame? GetLatestFrame()
        {
            /*
            The code below is non-atomic. Race-conditions can occur.
             
            Direct3D11CaptureFrame? frame = LatestFrame;
            LatestFrame = null;
            return frame;
            */
            
            // The code below is atomic.
            return Interlocked.Exchange(ref LatestFrame, null);
        }

        private static Texture2D? GetLatestFrameAsTexture2D()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            
            Direct3D11CaptureFrame? frame = GetLatestFrame();
            Device device = CaptureHandler.GraphicCaptureDevice();
            if (frame == null) return null;

            // Somehow at certain times, an ObjectDisposedException comes here.
            // It makes zero sense exactly why at the moment, so using the
            // trusty "try-catch" to workaround it for now. Really odd
            // considering all the checks done previous to running this.
            IDirect3DDxgiInterfaceAccess direct3DSurfaceDxgiInterfaceAccess;
            try {
                // ReSharper disable once SuspiciousTypeConversion.Global
                direct3DSurfaceDxgiInterfaceAccess = (IDirect3DDxgiInterfaceAccess)frame.Surface;
            } catch (ObjectDisposedException exception) {
                Console.WriteLine("[EXCEPTION OCCURED]: " + exception.ObjectName + " - " + exception.Message);
                return null;
            }

            IntPtr resourcePointer = direct3DSurfaceDxgiInterfaceAccess.GetInterface(ResourceGuid);
            using Texture2D surfaceTexture = new Texture2D(resourcePointer);
            frame.Dispose();
            Texture2DDescription description = new Texture2DDescription
            {
                ArraySize = 1,
                BindFlags = BindFlags.None,
                CpuAccessFlags = CpuAccessFlags.Read,
                Format = Format.B8G8R8A8_UNorm,
                Height = surfaceTexture.Description.Height,
                Width = surfaceTexture.Description.Width,
                MipLevels = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging // GPU -> CPU
            };
            Texture2D texture2DFrame = new Texture2D(device, description);
            device.ImmediateContext.CopyResource(surfaceTexture, texture2DFrame);
            
            watch.Stop();
            Texture2DTime = watch.ElapsedMilliseconds;

            return texture2DFrame;
        }

        private static void CopyMemory(
            bool parallel,
            int from, 
            int to, 
            IntPtr sourcePointer, 
            IntPtr destinationPointer, 
            int sourceStride, 
            int destinationStride)
        {
            if (!parallel) {
                for (int i = from; i < to; i++) {
                    IntPtr sourceIteratedPointer = IntPtr.Add(sourcePointer, sourceStride * i);
                    IntPtr destinationIteratedPointer = IntPtr.Add(destinationPointer, destinationStride * i);
                    
                    // Memcpy is apparently faster than Buffer.MemoryCopy. 
                    Utilities.CopyMemory(destinationIteratedPointer, sourceIteratedPointer, destinationStride);
                }
                return;
            }

            Parallel.For(from, to, i =>
            {
                IntPtr sourceIteratedPointer = IntPtr.Add(sourcePointer, sourceStride * i);
                IntPtr destinationIteratedPointer = IntPtr.Add(destinationPointer, destinationStride * i);
                
                // Memcpy is apparently faster than Buffer.MemoryCopy. 
                Utilities.CopyMemory(destinationIteratedPointer, sourceIteratedPointer, destinationStride);
            });
        }

        [SuppressMessage(
            "ReSharper.DPA", 
            "DPA0003: Excessive memory allocations in LOH", 
            MessageId = "type: System.Byte[]"
            )]
        private static (byte[]? frameBytes, int width, int height, int stride) GetLatestFrameAsByteBgra()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            
            Texture2D? frame = GetLatestFrameAsTexture2D();
            if (frame == null) return (null, 0, 0, 0);
            
            Device device = CaptureHandler.GraphicCaptureDevice();

            DataBox mappedMemory = 
                device.ImmediateContext.MapSubresource(frame, 0, MapMode.Read, MapFlags.None);

            int width = frame.Description.Width;
            int height = frame.Description.Height;
            
            IntPtr sourcePointer = mappedMemory.DataPointer;
            int sourceStride = mappedMemory.RowPitch;
            int destinationStride = width * 4;

            byte[] frameBytes = new byte[width * height * 4]; // 4 bytes / pixel (High Mem. Allocation)

            unsafe {
                fixed (byte* frameBytesPointer = frameBytes) {
                    IntPtr destinationPointer = (IntPtr)frameBytesPointer;
                    
                    /*
                    for (int i = 0; i < height; i++) {
                        Utilities.CopyMemory(destinationPointer, sourcePointer, destinationStride);

                        sourcePointer = IntPtr.Add(sourcePointer, sourceStride);
                        destinationPointer = IntPtr.Add(destinationPointer, destinationStride);
                    }
                    */
                    
                    CopyMemory(
                        true, // Should run in parallel or not.
                        0, 
                        height, 
                        sourcePointer, 
                        destinationPointer, 
                        sourceStride, 
                        destinationStride
                        );
                }
            }
            
            device.ImmediateContext.UnmapSubresource(frame, 0);
            frame.Dispose();
            
            watch.Stop();
            ByteArrayTime = watch.ElapsedMilliseconds;
            
            return (frameBytes, width, height, destinationStride);
        }
        
        public static (Mat?, int width, int height) GetLatestFrameAsMat()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            
            (byte[]? frameBytes, int width, int height, int stride) = GetLatestFrameAsByteBgra();
            if (frameBytes == null) return (null, 0, 0);
            
            /*
            Mat frameMat = new Mat(width, height, MatType.CV_8UC4); // 8UC4: 8 unsigned bits * 4 colors (BGRA)
            Mat.Indexer<Vec4b> indexer = frameMat.GetGenericIndexer<Vec4b>();

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    int bufferPos = y * width + x * 4;
                    // BGRA format
                    byte blue = frameBytes[bufferPos];
                    byte green = frameBytes[bufferPos + 1];
                    byte red = frameBytes[bufferPos + 2];
                    byte alpha = frameBytes[bufferPos + 3];
                    Vec4b matByteValue = new Vec4b(blue, green, red, alpha);
                    indexer[y, x] = matByteValue;
                }
            }
            */

            // 8UC4: 8 unsigned bits * 4 colors (BGRA), Padding: width * (4 bytes / pixel)
            Mat frameMat = new Mat(height, width, MatType.CV_8UC4, frameBytes, stride);

            watch.Stop();
            MatTime = watch.ElapsedMilliseconds;

            return (frameMat, width, height);
        }

        public static (long textureTime, long byteTime, long matTime) GetTimerValues()
        {
            return (Texture2DTime, ByteArrayTime, MatTime);
        }
        
        public static void AddFrame(Direct3D11CaptureFrame frame)
        {
            LatestFrame = frame;
        }

        public static void FreeRuntimeResources()
        {
            LatestFrame?.Dispose();
        }

    }

}