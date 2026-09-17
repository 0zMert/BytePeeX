using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Folderize.Services
{
    /// <summary>
    /// Enables Windows Backup and Restore privileges (SeBackupPrivilege, SeRestorePrivilege)
    /// on the process token, allowing an elevated administrator to bypass all NTFS DACLs / permissions
    /// (e.g. read other users' AppData, System Volume Information, and protected system files).
    /// </summary>
    public static class SecurityPrivilegeService
    {
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const string SE_BACKUP_NAME = "SeBackupPrivilege";
        private const string SE_RESTORE_NAME = "SeRestorePrivilege";
        private const string SE_SECURITY_NAME = "SeSecurityPrivilege";
        private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle,
            bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            uint BufferLength,
            IntPtr PreviousState,
            IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static bool _backupPrivilegeEnabled = false;

        public static bool EnsureBackupPrivileges()
        {
            if (_backupPrivilegeEnabled) return true;

            try
            {
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr hToken))
                {
                    return false;
                }

                try
                {
                    bool b1 = EnablePrivilege(hToken, SE_BACKUP_NAME);
                    bool b2 = EnablePrivilege(hToken, SE_RESTORE_NAME);
                    bool b3 = EnablePrivilege(hToken, SE_SECURITY_NAME);
                    _backupPrivilegeEnabled = b1 || b2;
                    return _backupPrivilegeEnabled;
                }
                finally
                {
                    CloseHandle(hToken);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool EnablePrivilege(IntPtr hToken, string privilegeName)
        {
            if (!LookupPrivilegeValue(null, privilegeName, out LUID luid))
            {
                return false;
            }

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges = new LUID_AND_ATTRIBUTES
                {
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                }
            };

            return AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
