using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace iNetOMeter;

public static class StartupManager
{
    private const string ShortcutName = "iNetOMeter.lnk";

    public static string GetStartupShortcutPath()
    {
        string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        return Path.Combine(startupFolder, ShortcutName);
    }

    public static bool IsStartWithWindowsEnabled()
    {
        try
        {
            string shortcutPath = GetStartupShortcutPath();
            return File.Exists(shortcutPath);
        }
        catch
        {
            return false;
        }
    }

    public static bool SetStartWithWindows(bool enable)
    {
        try
        {
            string shortcutPath = GetStartupShortcutPath();

            if (enable)
            {
                string targetExe = Application.ExecutablePath;
                string workingDir = AppContext.BaseDirectory;

                var link = (IShellLinkW)new ShellLink();
                link.SetPath(targetExe);
                link.SetWorkingDirectory(workingDir);
                link.SetDescription("iNetOMeter - Taskbar network monitor & speed test");

                var persistFile = (IPersistFile)link;
                persistFile.Save(shortcutPath, true);
                return true;
            }
            else
            {
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
                return true;
            }
        }
        catch
        {
            return false;
        }
    }

    // COM interop for creating standard Windows shell .lnk shortcuts without external dependencies
    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, out IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([Out, MarshalAs(UnmanagedType.LPWStr)] string ppszFileName);
    }
}
