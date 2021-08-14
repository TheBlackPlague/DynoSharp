#nullable enable
using System;
using System.Diagnostics;
using System.Threading;
using OpenCvSharp;
using OpenCV = OpenCvSharp.Cv2;

namespace DynoSharp
{

    internal static class DynoBrain
    {
        
        /*
        public const int 
            CV_8UC1 = 0,
            CV_8SC1 = 1,
            CV_16UC1 = 2,
            CV_16SC1 = 3,
            CV_32SC1 = 4,
            CV_32FC1 = 5,
            CV_64FC1 = 6,
            CV_8UC2 = 8,
            CV_8SC2 = 9,
            CV_16UC2 = 10,
            CV_16SC2 = 11,
            CV_32SC2 = 12,
            CV_32FC2 = 13,
            CV_64FC2 = 14,
            CV_8UC3 = 16,
            CV_8SC3 = 17,
            CV_16UC3 = 18,
            CV_16SC3 = 19,
            CV_32SC3 = 20,
            CV_32FC3 = 21,
            CV_64FC3 = 22,
            CV_8UC4 = 24,
            CV_8SC4 = 25,
            CV_16UC4 = 26,
            CV_16SC4 = 27,
            CV_32SC4 = 28,
            CV_32FC4 = 29,
            CV_64FC4 = 30,
            CV_8UC5 = 32,
            CV_8SC5 = 33,
            CV_16UC5 = 34,
            CV_16SC5 = 35,
            CV_32SC5 = 36,
            CV_32FC5 = 37,
            CV_64FC5 = 38,
            CV_8UC6 = 40,
            CV_8SC6 = 41,
            CV_16UC6 = 42,
            CV_16SC6 = 43,
            CV_32SC6 = 44,
            CV_32FC6 = 45,
            CV_64FC6 = 46;
        */

        private static Mat ReferenceImage = null!;
        private static Mat ScreenFrame = null!;

        public static void Loop()
        {
            Console.WriteLine("[Dyno#] Starting up.");
            Startup();
            Thread.Sleep(5000);
            while (true) {
                // const int iterations = 15;

                if (!CaptureHandler.FrameCaptured) break; // Make sure pre-requisites are met.
                
                (Mat? screenFrame, _, _) = FramePool.GetLatestFrameAsMat();
                
                if (screenFrame == null) {
                    continue;
                }

                ScreenFrame = screenFrame;
                // OpenCvDetection();

                /*
                Window window = new Window("Display", Mat);
                window.Resize(Mat.Width, Mat.Height);
                Cv2.WaitKey();
                window.Dispose();
                */

                ScreenFrame.Dispose();
                
                // Benchmark(iterations);
                break;
            }
            Console.WriteLine("[Dyno#] Shutting down.");
            Shutdown();
        }

        private static void Startup()
        {
            ReferenceImage = new Mat(@"///", ImreadModes.Unchanged); // CV_8UC4
            // Console.WriteLine(ReferenceImage.Type());
        }

        private static void Shutdown()
        {
            ReferenceImage.Dispose();
        }

        private static void OpenCvDetection()
        {
            int row = ScreenFrame.Rows - ReferenceImage.Rows + 1;
            int col = ScreenFrame.Cols - ReferenceImage.Cols + 1;
            Mat result = new Mat(row, col, MatType.CV_32FC1);
            
            Mat grayScreenFrame = ScreenFrame.CvtColor(ColorConversionCodes.BGRA2GRAY);
            // Can be done before OpenCvDetection() is called.
            Mat grayReferenceImage = ReferenceImage.CvtColor(ColorConversionCodes.BGRA2GRAY);
            
            OpenCV.MatchTemplate(
                grayScreenFrame, 
                grayReferenceImage, 
                result, 
                TemplateMatchModes.CCoeffNormed
                );
            OpenCV.Threshold(result, result, 0.8, 1.0, ThresholdTypes.Tozero);

            while (true) {
                const double threshold = 0.9;
                
                OpenCV.MinMaxLoc(
                    result, 
                    out _, 
                    out double maxValue, 
                    out _, 
                    out Point maxLocation
                    );
                
                if (maxValue < threshold) break;

                if (maxLocation.X < ScreenFrame.Width * 0.25) {
                    KeyboardHandler.PressSpace();
                    break;
                }
                
                // Fill the result so we don't find the same value again
                // when running OpenCV.MinMaxLoc().
                OpenCV.FloodFill(
                    result, 
                    maxLocation, 
                    new Scalar(0), 
                    out _, 
                    new Scalar(0.1), 
                    new Scalar(1.0)
                );
            }
            
            // Free resources.
            result.Dispose();
            grayScreenFrame.Dispose();
            grayReferenceImage.Dispose();
        }

        public static void Benchmark(int iterations)
        {
            float totalTime = 0, longestTime = 0;
            int width = 0, height = 0;
            Console.WriteLine("[BENCHMARK] Starting benchmark. Iteration #0 is regarded as test iteration.");
            for (int i = 0; i < iterations + 1; i++) {
                Console.WriteLine("[BENCHMARK] Running iteration #" + i + "...");
                    
                (float time, int w, int h, bool isNull) = MeasurePerformance();
                    
                Console.WriteLine("[BENCHMARK] Iteration #" + i + " completed with success = " + (!isNull));

                if (isNull) {
                    i--;
                    continue;
                }

                if (i == 0) {
                    width = w;
                    height = h;
                    continue;
                }

                if (time > longestTime) longestTime = time;
                totalTime += time;
            }

            float avgTime = totalTime / (iterations - 1);
            float fps = 1000 / avgTime;
            float lowestFps = 1000 / longestTime;
            Console.WriteLine("[BENCHMARK] Iteration Count: " + iterations);
            Console.WriteLine("[BENCHMARK] Resolution: " + width + "x" + height);
            Console.WriteLine("[BENCHMARK] FPS: " + fps + ", Lowest FPS: " + lowestFps);
        }

        private static (float time, int width, int height, bool isNull) MeasurePerformance()
        {
            Thread.Sleep(200);
            Stopwatch watch = new Stopwatch();
            watch.Start();
            (Mat? frame, int width, int height) = FramePool.GetLatestFrameAsMat();
            if (frame == null) return (0f, 0, 0, true);
            ScreenFrame = frame;
            
            OpenCvDetection();

            ScreenFrame.Dispose();
            watch.Stop();
            // (long t2D, long bA, long mT) = FramePool.GetTimerValues();
            
            return (watch.ElapsedMilliseconds, width, height, false);
        }

    }

}