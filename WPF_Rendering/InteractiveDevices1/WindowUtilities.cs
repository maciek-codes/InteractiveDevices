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

            window.WindowStyle = WindowStyle.None;
            window.WindowStartupLocation = WindowStartupLocation.Manual;

            window.Left = screens[monitor].Bounds.Left;
            window.Top = screens[monitor].Bounds.Top;

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
