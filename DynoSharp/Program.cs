using System;
using System.Threading;

namespace DynoSharp
{

    internal static class Program
    {
        
        private const string PROCESS_NAME = "Taskmgr";
        private const string WINDOW_NAME = "Task Manager";

        private static void Main(string[] args)
        {
            Console.WriteLine("Starting capture.");
            CaptureHandler.Start(PROCESS_NAME, WINDOW_NAME);
            Console.WriteLine("Capture Status: " + CaptureHandler.IsCapturing);
            DynoBrain.Loop();
            Console.WriteLine("Stopping capture.");
            CaptureHandler.Stop();
            Thread.Sleep(5000);
        }

    }

}