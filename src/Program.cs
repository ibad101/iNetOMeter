using System;
using System.Threading;
using System.Windows.Forms;

namespace iNetOMeter;

internal static class Program
{
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    static void Main()
    {
        const string appGuid = "iNetOMeter_SingleInstance_Mutex_9B8C7E6A";
        _singleInstanceMutex = new Mutex(true, appGuid, out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("iNetOMeter is already running in the taskbar.", "iNetOMeter", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TaskbarBandForm());

        GC.KeepAlive(_singleInstanceMutex);
    }
}
