namespace Origami
{
    using System;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using KinectWPFOpenCV;
    using Microsoft.Kinect;
    using Emgu.CV;
    using Emgu.CV.Structure;
    using System.IO;
    using Utiities;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        KinectSensor sensor;
        //WriteableBitmap depthBitmap;
        WriteableBitmap colorBitmap;
        //DepthImagePixel[] depthPixels;
        byte[] colorPixels;

        public MainWindow()
        {
            InitializeComponent();
           
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            MouseDown += MainWindow_MouseDown;

            WindowUtilities.MaximizeWindow(this);

        }

      
        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {

            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }


            if (null != this.sensor)
            {

                //this.sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                this.sensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                //this.depthPixels = new DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                this.colorBitmap = new WriteableBitmap(this.sensor.ColorStream.FrameWidth, this.sensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);
                //this.depthBitmap = new WriteableBitmap(this.sensor.DepthStream.FrameWidth, this.sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);                
                this.colorImg.Source = this.colorBitmap;

                this.sensor.AllFramesReady += sensor_AllFramesReady;

                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null != this.sensor) 
                return;

            this.outputViewbox.Visibility = Visibility.Collapsed;
            this.txtError.Visibility = Visibility.Visible;
            this.txtInfo.Text = "No Kinect Found";
        }

        private void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using (var colorFrame = e.OpenColorImageFrame())
            {

                /*
                using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
                {
                    if (depthFrame != null)
                    {

                        blobCount = 0;
                        depthBmp = depthFrame.SliceDepthImage((int)sliderMin.Value, (int)sliderMax.Value);
                        
                        Image<Bgr, Byte> openCVImg = new Image<Bgr, byte>(depthBmp.ToBitmap());
                        Image<Gray, byte> gray_image = openCVImg.Convert<Gray, byte>();

                        using (MemStorage stor = new MemStorage())
                        {
                            //Find contours with no holes try CV_RETR_EXTERNAL to find holes
                            Contour<System.Drawing.Point> contours = gray_image.FindContours(
                             Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                             Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_EXTERNAL,
                             stor);

                            for (int i = 0; contours != null; contours = contours.HNext)
                            {
                                i++;

                                if ((contours.Area > Math.Pow(sliderMinSize.Value, 2)) && (contours.Area < Math.Pow(sliderMaxSize.Value, 2)))
                                {
                                    MCvBox2D box = contours.GetMinAreaRect();                                    
                                    openCVImg.Draw(box, new Bgr(System.Drawing.Color.Red), 2);                                    
                                    blobCount++;
                                }
                            }
                        }

                        this.outImg.Source = ImageHelpers.ToBitmapSource(openCVImg);                        
                        //txtBlobCount.Text = blobCount.ToString();
                    }
                }*/


                if (colorFrame != null)
                {
                    colorFrame.CopyPixelDataTo(this.colorPixels);

                    var openCvImgGrayscale = new Image<Gray, byte>(colorBitmap.ToBitmap());
                    var openCvImgColour = new Image<Bgr, byte>(colorBitmap.ToBitmap());
                    
 

                    // Get threshold value
                    var thresholdMin = Convert.ToInt32(sliderThresholdMin.Value);
                    var thresholdMax = Convert.ToInt32(sliderThresholdMax.Value);

                    // Thresholding
                    var thresholdImage = openCvImgGrayscale.ThresholdBinary(new Gray(thresholdMin), new Gray(thresholdMax));

                    #region Extracting the Contours
                    using (var storage = new MemStorage())
                    {
                        // Find contours
                        var contours = thresholdImage.FindContours(
                            Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, 
                            Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_TREE, 
                            storage);

                        for (;contours != null; contours = contours.HNext)
                        {

                            // Draw contours we found
                            var currentContour = contours.ApproxPoly(contours.Perimeter * 0.015, storage);
                            if (currentContour.BoundingRectangle.Width > 20)
                            {
                                CvInvoke.cvDrawContours(openCvImgColour, contours, new MCvScalar(0, 0, 255), new MCvScalar(0, 0, 255), -1, 2, Emgu.CV.CvEnum.LINE_TYPE.EIGHT_CONNECTED, new System.Drawing.Point(0, 0));
                            }
                        }
                    }

                    #endregion

                    this.outImg.Source = ImageHelpers.ToBitmapSource(thresholdImage);
                    this.projectorImage.Source = ImageHelpers.ToBitmapSource(openCvImgColour);

                    // Copy pixels to small color bitmap
                    this.colorBitmap.WritePixels(
                      new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                      this.colorPixels,
                      this.colorBitmap.PixelWidth * sizeof(int),
                      0);
                }
            }
        }


        #region Window Stuff
        void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
           // this.DragMove();
        }


        void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        private void CloseBtnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
        #endregion
    }
}
