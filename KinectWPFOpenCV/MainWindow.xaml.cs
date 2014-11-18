using System.Diagnostics;
using System.Drawing;
using System.Windows.Media.Media3D;
using Emgu.CV.CvEnum;
using NativeHelpers;

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
	using SharpGL;
	using SharpGL.SceneGraph;
	using SharpGL.Enumerations;
	using SharpGL.WPF;

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


        readonly Window projectorWindow = new Window();
		OpenGLControl openGlControl = new OpenGLControl();

	    private PointF[] objectPoints; 

	    private readonly MCvPoint3D32f[][] imagePoints = new MCvPoint3D32f[1][];
       

	    public MainWindow()
	    {
	        InitializeComponent();

	        Loaded += MainWindow_Loaded;
	        Closing += MainWindow_Closing;
	        MouseDown += MainWindow_MouseDown;

	        openGlControl.OpenGLInitialized += OpenGLControl_OpenGLInitialized;
	        openGlControl.OpenGLDraw += OpenGLControl_OpenGLDraw;
	        openGlControl.DrawFPS = true;

	        // Set up window for projector
            projectorWindow.WindowStyle = WindowStyle.None;
	        projectorWindow.Content = openGlControl;
	        projectorWindow.Loaded += projectorWindow_Loaded;
	        WindowUtilities.ShowOnMonitor(1, projectorWindow);

            this.imagePoints[0] = new MCvPoint3D32f[49];

	    }

		void projectorWindow_Loaded(object sender, RoutedEventArgs e)
		{
		    var senderWindow = sender as Window;
		    if (senderWindow != null) 
                senderWindow.WindowState = WindowState.Maximized;
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
				if (colorFrame != null)
				{
					colorFrame.CopyPixelDataTo(this.colorPixels);

					

				    var openCvImgColour = new Image<Bgr, byte>(colorBitmap.ToBitmap());
                    var openCvImgGrayscale = new Image<Gray, byte>(this.colorBitmap.ToBitmap());

                    this.subSection = ExctractSubSection(openCvImgGrayscale);
				    this.subColSection = ExctractSubSection(openCvImgColour);

					// Get threshold value
					var thresholdMin = Convert.ToInt32(sliderThresholdMin.Value);
					var thresholdMax = Convert.ToInt32(sliderThresholdMax.Value);

					// Thresholding
                    var thresholdImage = subSection.ThresholdBinary(new Gray(thresholdMin), new Gray(thresholdMax));

                    FindContours(thresholdImage, subColSection);

					
					this.outImg.Source = ImageHelpers.ToBitmapSource(thresholdImage);

				    if (this.objectPoints != null)
				    {
				        foreach (var calibPoint in this.objectPoints)
				        {
                            this.subColSection.Draw(
                                new Rectangle(
                                    Convert.ToInt32(calibPoint.X),
                                    Convert.ToInt32(calibPoint.Y), 5, 5), 
                                    new Bgr(System.Drawing.Color.Yellow), 5); 
				        }
				        
				    }
                    this.projectorImage.Source = ImageHelpers.ToBitmapSource(subColSection);

					// Copy pixels to small color bitmap
					this.colorBitmap.WritePixels(
					  new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
					  this.colorPixels,
					  this.colorBitmap.PixelWidth * sizeof(int),
					  0);
				}
			}
		}

	    private static Image<TColor, TDepth> ExctractSubSection<TColor, TDepth>(Image<TColor, TDepth> sourceImage)
            where TColor : struct, IColor
            where TDepth : new()
	    {
	       
	        Image<TColor, TDepth> sect;
	        try
	        {
                sect = sourceImage.GetSubRect(new Rectangle(150, 0,
	                400,
	                250));
	        }
	        catch (Exception ex)
	        {
	            throw ex;
	        }
            return sect;
	    }

	    private static void FindContours(Image<Gray, byte> thresholdImage, Image<Bgr, byte> subColSection)
	    {
	        using (var storage = new MemStorage())
	        {
	            // Find contours
	            var contours = thresholdImage.FindContours(
	                Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
	                Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_TREE,
	                storage);

	            for (; contours != null; contours = contours.HNext)
	            {
	                // Draw contours we found
	                var currentContour = contours.ApproxPoly(contours.Perimeter*0.015, storage);
	                if (currentContour.BoundingRectangle.Width > 20)
	                {
	                    CvInvoke.cvDrawContours(subColSection, contours, new MCvScalar(0, 0, 255), new MCvScalar(0, 0, 255), -1,
	                        2, Emgu.CV.CvEnum.LINE_TYPE.EIGHT_CONNECTED, new System.Drawing.Point(0, 0));
	                }
	            }
	        }
	    }

	    #region OpenGl

		private float rotatePyramid = 0.0f;
	    private Image<Gray, byte> subSection;
	    private Image<Bgr, byte> subColSection;

	    private void OpenGLControl_OpenGLInitialized(object sender, OpenGLEventArgs args)
		{
			//  Enable the OpenGL depth testing functionality.
			args.OpenGL.Enable(OpenGL.GL_DEPTH_TEST);
            args.OpenGL.ClearColor(1.0f, 1.0f, 1.0f, 0.0f);
		}

		private void OpenGLControl_OpenGLDraw(object sender, OpenGLEventArgs args)
		{
			OpenGL gl = args.OpenGL;

			//  Clear the color and depth buffers.
			gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
			int GridSizeX = 8;
			int GridSizeY = 8;
			int SizeX = 16;
			int SizeY = 16;
 
			gl.MatrixMode(MatrixMode.Modelview);
			gl.LoadIdentity();
 
			gl.MatrixMode(MatrixMode.Projection);
			gl.LoadIdentity();
			gl.Ortho(0,                           // left
				SizeX * GridSizeX,              // right
				0,                                // bottom
				SizeY * GridSizeY,              // top
				-1.0, // near
				1.0);

            // Get scale value
		    var scaleX = ScaleX.Value;
            var scaleY = ScaleY.Value;
            gl.Scale(scaleX, scaleY, 0);

		    double leftTransate = 0.0, topTranslate = 0.0;

		    if (!Double.TryParse(shiftLeft.Text, out leftTransate))
		    {
		        leftTransate = 0.0;
		    }

            if (!Double.TryParse(shiftTop.Text, out topTranslate))
		    {
		        topTranslate = 0.0;
		    }

		    gl.Translate(leftTransate, topTranslate, 0.0);
 
			gl.Begin(BeginMode.Quads);

            // Draw border 

            //white
            //gl.Color(1.0f, 1.0f, 1.0f);
            //gl.Vertex(0, 0);
            //gl.Vertex(SizeX * GridSizeX + 10, SizeY * GridSizeY + 10);
            //gl.Vertex(0, SizeY * GridSizeY + 10);
            //gl.Vertex(SizeX * GridSizeX + 10, 0);

			for (int x = 0; x < GridSizeX; ++x)
			{
				for (int y = 0; y < GridSizeY; ++y)
				{
                    
					if (((x + y) & 0x1) == 0x1) //modulo 2
						gl.Color(1.0f, 1.0f, 1.0f); //white
					else
						gl.Color(0.0f, 0.0f, 0.0f); //black

					gl.Vertex(x * SizeX, y * SizeY);
					gl.Vertex((x + 1) * SizeX, y * SizeY);
					gl.Vertex((x + 1) * SizeX, (y + 1) * SizeY);
					gl.Vertex(x * SizeX, (y + 1) * SizeY);

				    if (x < GridSizeX - 1 && y < GridSizeY - 1)
				    {
				        this.imagePoints[0][x*y] = new MCvPoint3D32f((x + 1)*SizeX, (y + 1)*SizeY, 0);
				    }

				}
			}
			gl.End();
		}

		#endregion


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
			this.projectorWindow.Close();
		}

		private void CloseBtnClick(object sender, RoutedEventArgs e)
		{
			Close();
		}
		#endregion

        /// <summary>
        /// Calibrate the camera
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
	    private void OnButtonCalibrateClick(object sender, RoutedEventArgs e)
        {
            var greyscale = new Image<Gray, byte>(this.colorBitmap.ToBitmap());
            this.objectPoints = CameraCalibration.FindChessboardCorners(this.subSection,
                new System.Drawing.Size(7, 7),
                CALIB_CB_TYPE.ADAPTIVE_THRESH);

            if (this.objectPoints != null)
            {
                this.subColSection.Draw(new Rectangle(Convert.ToInt32(this.objectPoints[0].X),
                    Convert.ToInt32(this.objectPoints[0].Y), 10, 10), new Bgr(System.Drawing.Color.Yellow), 5);

                this.subColSection.Draw(new Rectangle(Convert.ToInt32(this.objectPoints[48].X),
                   Convert.ToInt32(this.objectPoints[48].Y), 10, 10), new Bgr(System.Drawing.Color.Yellow), 5);

                this.projectorImage.Source = ImageHelpers.ToBitmapSource(subColSection);

                // Re-shape result array
                PointF[][] objectPointsFrames = new PointF[1][];
                objectPointsFrames[0] = objectPoints;


                ExtrinsicCameraParameters[] extrinsicPoints;
                CameraCalibration.CalibrateCamera(this.imagePoints, objectPointsFrames, subSection.Size, new IntrinsicCameraParameters(),
                    CALIB_TYPE.DEFAULT, out extrinsicPoints);


            }
        }
	}
}
