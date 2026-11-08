using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace VercelDeploymentMonitor.Services;

public sealed class TaskbarProgressService : IDisposable
{
    private IntPtr _hwnd;
    private ITaskbarList3? _taskbar;
    private bool _isBusy;

    public void Initialize(Window window)
    {
        _hwnd = WindowNative.GetWindowHandle(window);
        _taskbar = (ITaskbarList3)new CTaskbarList();
        _taskbar.HrInit();
    }

    public void SetBusy(bool isBusy)
    {
        if (_taskbar is null || _hwnd == IntPtr.Zero || _isBusy == isBusy)
        {
            return;
        }

        _isBusy = isBusy;
        _taskbar.SetProgressState(_hwnd, isBusy ? TBPFLAG.TBPF_INDETERMINATE : TBPFLAG.TBPF_NOPROGRESS);
    }

    public void Dispose()
    {
        try
        {
            if (_taskbar is not null && _hwnd != IntPtr.Zero)
            {
                _taskbar.SetProgressState(_hwnd, TBPFLAG.TBPF_NOPROGRESS);
            }
        }
        catch
        {
            // Ignore dispose-time COM failures.
        }

        if (_taskbar is not null)
        {
            Marshal.ReleaseComObject(_taskbar);
            _taskbar = null;
        }
    }

    [ComImport]
    [Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    private class CTaskbarList
    {
    }

    [ComImport]
    [Guid("EA1AFB91-9E28-4B86-90E9-9E9F8A5EEFAF")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
        void SetProgressState(IntPtr hwnd, TBPFLAG state);
    }

    private enum TBPFLAG
    {
        TBPF_NOPROGRESS = 0,
        TBPF_INDETERMINATE = 0x1,
        TBPF_NORMAL = 0x2,
        TBPF_ERROR = 0x4,
        TBPF_PAUSED = 0x8
    }
}
