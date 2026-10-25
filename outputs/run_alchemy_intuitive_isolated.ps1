# Diagnostic-only launcher: create a non-input desktop, never switch to it, close it after Godot exits.
# No mouse/keyboard injection, activation, screenshots of the user's desktop, or project setting changes.
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
public static class AlchemyIsolatedRender {
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    struct StartupInfo {
        public int cb; public string reserved, desktop, title;
        public int x,y,xSize,ySize,xChars,yChars,fill,flags;
        public short show, reservedSize; public IntPtr reservedBytes, input, output, error;
    }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr process, thread; public int pid, tid; }
    [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern IntPtr CreateDesktopW(string name, IntPtr device, IntPtr mode, int flags, uint access, IntPtr security);
    [DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool CreateProcessW(string app, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string cwd, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
    [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr handle, out uint code);
    [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr handle, uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    public static uint Run(string executable, string arguments, string cwd) {
        string name = "AlchemyRender_" + Guid.NewGuid().ToString("N");
        IntPtr desktop = CreateDesktopW(name, IntPtr.Zero, IntPtr.Zero, 0, 0x01ff, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        ProcessInfo process = default(ProcessInfo);
        try {
            var startup = new StartupInfo { cb = Marshal.SizeOf(typeof(StartupInfo)), desktop = name, flags = 1, show = 0 };
            if (!CreateProcessW(executable, new StringBuilder("\"" + executable + "\" " + arguments),
                IntPtr.Zero, IntPtr.Zero, false, 0x08000000, IntPtr.Zero, cwd, ref startup, out process))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (WaitForSingleObject(process.process, 60000) != 0) {
                TerminateProcess(process.process, 2);
                throw new TimeoutException("Isolated renderer exceeded 60 seconds.");
            }
            uint code; GetExitCodeProcess(process.process, out code); return code;
        } finally {
            if (process.thread != IntPtr.Zero) CloseHandle(process.thread);
            if (process.process != IntPtr.Zero) CloseHandle(process.process);
            CloseDesktop(desktop);
        }
    }
}
'@
$appraisalExit = [AlchemyIsolatedRender]::Run(
    'D:\Godot\gd.net\Godot_v4.7.2-stable_mono_win64.exe',
    '--path "D:/tian-guan-you-du" --rendering-method gl_compatibility --audio-driver Dummy --log-file "D:/tian-guan-you-du/outputs/alchemy_intuitive_visual_20261025.log" res://tscn/scene/AlchemyPreviewScene.tscn -- --alchemy-appraisal-check --alchemy-appraisal-capture',
    'D:\tian-guan-you-du')
Write-Output "ISOLATED_RENDER_EXIT $appraisalExit"
exit $appraisalExit
