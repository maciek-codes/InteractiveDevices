using System;
using System.Windows.Interop;
using NativeHelpers;

namespace Origami.Utiities
{
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using System.Windows.Forms;

    static public class WindowUtilities
    {

        public static Screen GetSecondScreen()
        {
            var secondaryScreen = Screen.PrimaryScreen;

            foreach (var screen in Screen.AllScreens)
            {
                if (screen.Equals(Screen.PrimaryScreen))
                    continue;
                secondaryScreen = screen;
                break;
            }
            return secondaryScreen;
        }

        public static void ShowOnMonitor(int monitor, Window window)
        {
            Screen[] screens = Screen.AllScreens;

            IntPtr windowHandle = new WindowInteropHelper(window).Handle;

            const double baseDpi = 96.0;

            double currentDpi = PerMonitorDPIHelper.GetDpiForWindow(windowHandle);

            window.WindowStyle = WindowStyle.None;
            window.WindowStartupLocation = WindowStartupLocation.Manual;

            window.Left = screens[monitor].Bounds.Left * baseDpi / currentDpi;
            window.Top = screens[monitor].Bounds.Top * baseDpi / currentDpi;

            window.SourceInitialized += (snd, arg) =>
                window.WindowState = WindowState.Maximized;

            window.Show();
        }

        public static void SetCanvasAsWindowRoot(Window window, Canvas canvas)
        {
            // Set Grid Panel as content of the Window
            window.Content = canvas;
        }
    }
}
