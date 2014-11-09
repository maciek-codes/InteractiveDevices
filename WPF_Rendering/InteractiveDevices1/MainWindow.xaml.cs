﻿namespace Origami.Utilities
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using Utiities;


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            WindowUtilities.MaximizeWindow(this);

            InitializeComponent();
            //PointCollection myPC = new PointCollection();
            //myPC.Add(new Point(100, 100));
            //myPC.Add(new Point(100, 500));
            //myPC.Add(new Point(500, 500));
            ////myPC.Add(new Point(500, 100));
            
            //Polygon myP = new Polygon();
            //myP.Points = myPC;
            //myP.Fill = Brushes.Red;
            //myP.x
            //myP.Width = 1000;
            //myP.Height = 500;
            //myP.Stretch = Stretch.Fill;
            //myP.Stroke = Brushes.Red;
            //myP.StrokeThickness = 2;
            //this.mainCanvas.Children.Add(myP);


            // Create a Canvas Panel control
            Canvas canvasPanel = new Canvas();
            // Set Canvas Panel properties
            canvasPanel.Background = new SolidColorBrush(Colors.Black);
 
            // Add Child Elements to Canvas
            Rectangle redRectangle = new Rectangle();
            redRectangle.Width = 200;
            redRectangle.Height = 200;
            redRectangle.Stroke = new SolidColorBrush(Colors.Black);
            redRectangle.StrokeThickness = 10;
            redRectangle.Fill = new SolidColorBrush(Colors.Red);
            // Set Canvas position
            Canvas.SetLeft(redRectangle, 10);
            Canvas.SetTop(redRectangle, 10);
            // Add Rectangle to Canvas
            canvasPanel.Children.Add(redRectangle);
 
            // Add Child Elements to Canvas
            Rectangle blueRectangle = new Rectangle();
            blueRectangle.Width = 200;
            blueRectangle.Height = 200;
            blueRectangle.Stroke = new SolidColorBrush(Colors.Black);
            blueRectangle.StrokeThickness = 10;
            blueRectangle.Fill = new SolidColorBrush(Colors.Blue);
            // Set Canvas position
            Canvas.SetLeft(blueRectangle, 60);
            Canvas.SetTop(blueRectangle, 60);
            // Add Rectangle to Canvas
            canvasPanel.Children.Add(blueRectangle);
 
            // Add Child Elements to Canvas
            Rectangle greenRectangle = new Rectangle();
            greenRectangle.Width = 200;
            greenRectangle.Height = 200;
            greenRectangle.Stroke = new SolidColorBrush(Colors.Black);
            greenRectangle.StrokeThickness = 10;
            greenRectangle.Fill = new SolidColorBrush(Colors.Green);
            // Set Canvas position
            Canvas.SetLeft(greenRectangle, 500);
            Canvas.SetTop(greenRectangle, 110);
            // Add Rectangle to Canvas
            canvasPanel.Children.Add(greenRectangle);
  
 
            // Set Grid Panel as content of the Window
            RootWindow.Content = canvasPanel;
        }
    }
}
