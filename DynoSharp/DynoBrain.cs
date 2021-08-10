using System;
using System.Threading;
using OpenCvSharp;

namespace DynoSharp
{

    internal static class DynoBrain
    {

        public static void Loop()
        {
            while (true) {
                const int iterations = 15;
                
                Thread.Sleep(5000);

                if (!CaptureHandler.FrameCaptured) break; // Make sure pre-requisites are met.
                Benchmark(iterations);
                break;
            }
        }

        private static void Benchmark(int iterations)
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
            bool isNull = false;
            (Mat frame, int width, int height) = FramePool.GetLatestFrameAsMat();
            if (frame == null) isNull = true;
            frame?.Dispose();
            (long t2D, long bA, long mT) = FramePool.GetTimerValues();
            
            return (t2D + bA + mT, width, height, isNull);
        }

    }

}