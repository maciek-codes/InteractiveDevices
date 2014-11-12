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

        public static void MaximizeWindow(Window window)
        {
            Debug.Write(Screen.AllScreens.Length);
            var secondaryScreen = Screen.AllScreens[Screen.AllScreens.Length - 1];
            //Screen secondaryScreen = Screen.AllScreens.Where(s => !s.Primary).FirstOrDefault();
            //Screen.AllScreens[1];
            
            if (secondaryScreen == null) 
                return;

            if (!window.IsLoaded)
            {
                window.WindowStartupLocation = WindowStartupLocation.Manual;
            }
            
            var workingArea = secondaryScreen.WorkingArea;
            window.Left = workingArea.Left;
            window.Top = workingArea.Top;
            window.Width = workingArea.Width;
            window.Height = workingArea.Height;
            
            if (window.IsLoaded)
            {
                window.WindowState = WindowState.Maximized;
            }
        }

        public static void SetCanvasAsWindowRoot(Window window, Canvas canvas)
        {
            // Set Grid Panel as content of the Window
            window.Content = canvas;
        }
    }
}
