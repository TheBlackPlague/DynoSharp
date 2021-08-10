using System;
using System.Runtime.InteropServices;

namespace DynoSharp.Interop
{

   [ComImport]
   [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   public interface IGraphicsCaptureItemInterop
   {

      IntPtr CreateForWindow([In] IntPtr windowHandle, ref Guid guid);

   }

   [ComImport]
   [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
   [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
   public interface IDirect3DDxgiInterfaceAccess : IDisposable
   {

      IntPtr GetInterface([In] ref Guid guid);

   }

}