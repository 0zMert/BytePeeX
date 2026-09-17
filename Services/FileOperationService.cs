using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Folderize.Services
{
    /// <summary>
    /// Provides shell file operations such as moving files and folders to the Windows Recycle Bin.
    /// </summary>
    public static class FileOperationService
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string? lpszProgressTitle;
        }

        private const uint FO_DELETE = 0x0003;
        private const ushort FOF_ALLOWUNDO = 0x0040;        // Recycle Bin!
        private const ushort FOF_NOCONFIRMATION = 0x0010;   // Don't show native Windows confirmation (we handle rich confirmation in WPF)
        private const ushort FOF_WANTNUKEWARNING = 0x4000;  // Warn user if item is too large for recycle bin

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

        /// <summary>
        /// Moves a file or directory to the Windows Recycle Bin.
        /// </summary>
        public static bool SendToRecycleBin(string path, IntPtr hwnd = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            // Strip extended-length prefix if present since Shell32 expects standard Win32 path
            string cleanPath = path.StartsWith(@"\\?\") ? path.Substring(4) : path;
            cleanPath = cleanPath.TrimEnd('\\');

            if (!File.Exists(cleanPath) && !Directory.Exists(cleanPath))
            {
                return false;
            }

            try
            {
                var shf = new SHFILEOPSTRUCT
                {
                    hwnd = hwnd,
                    wFunc = FO_DELETE,
                    pFrom = cleanPath + "\0\0",
                    pTo = null,
                    fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_WANTNUKEWARNING,
                    fAnyOperationsAborted = false,
                    hNameMappings = IntPtr.Zero,
                    lpszProgressTitle = null
                };

                int result = SHFileOperation(ref shf);
                return result == 0 && !shf.fAnyOperationsAborted;
            }
            catch
            {
                return false;
            }
        }
    }
}
