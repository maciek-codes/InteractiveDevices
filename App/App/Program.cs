using System;
using System.Diagnostics;
using Origami.Modules;
using Origami.States;
using Origami.Utilities;
using k = Microsoft.Kinect;
using Mogre;
using System.IO;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.Drawing;
using System.Runtime.InteropServices;
using Rectangle = System.Drawing.Rectangle;

namespace Origami
{
    public class Program
    {
        //////////////////////////////////////////////////////////////////////////
        private static OgreManager mEngine;
        private static StateManager mStateMgr;

        //////////////////////////////////////////////////////////////////////////
        private Light mLight1;
        private Light mLight2;

        ////** KINECT STUFF **
        private Microsoft.Kinect.KinectSensor sensor;
        private byte[] colorPixels;

        /// <summary>
        /// Paper position, width and height detected by Kinect
        /// </summary>
        private Rectangle paperRect;
        private SceneNode cSceneNode;
        private float zPositionSquare = 0.53f;
        private k.DepthImagePixel[] depthPixes;
        private string kinectColorWindowName;
        private string kinectDepthWindowName;
        private string kinectThresholdWindowName;
        private k.SkeletonPoint skeletonPoint;
        private Matrix4 projectionMatrix;
        private Matrix4 viewMatrix;
        private Matrix4 transformMat;
        private Matrix4 inverseTransformMat;

        /************************************************************************/
        /* program starts here                                                  */
        /************************************************************************/
        [STAThread]
        static void Main()
        {
            // create Ogre manager
            mEngine = new OgreManager();

            // create state manager
            mStateMgr = new StateManager(mEngine);

            // create main program
            Program prg = new Program();

            // try to initialize Ogre and the state manager
            if (mEngine.Startup() && mStateMgr.Startup(typeof(TurningHead)))
            {
                // create objects in scene
                prg.CreateScene();

                // run engine main loop until the window is closed
                while (!mEngine.Window.IsClosed)
                {
                    // update the objects in the scene
                    prg.UpdateScene();

                    // update Ogre and render the current frame
                    mEngine.Update();
                }

                // remove objects from scene
                prg.RemoveScene();
            }

            // shut down state manager
            mStateMgr.Shutdown();

            // shutdown Ogre
            mEngine.Shutdown();
        }

        /************************************************************************/
        /* constructor                                                          */
        /************************************************************************/
        public Program()
        {
            mLight1 = null;
            mLight2 = null;
            foreach (var potentialSensor in k.KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == k.KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }


            if (null != this.sensor)
            {
                this.sensor.ColorStream.Enable(k.ColorImageFormat.RgbResolution640x480Fps30);
                this.sensor.DepthStream.Enable(k.DepthImageFormat.Resolution640x480Fps30);

                // Initialize buffer for pixels from kinect
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                this.depthPixes = new k.DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                this.sensor.AllFramesReady += sensor_AllFramesReady;
                this.kinectColorWindowName = "Kinect color";
                CvInvoke.cvNamedWindow(this.kinectColorWindowName);
                this.kinectDepthWindowName = "Kinect depth";
                CvInvoke.cvNamedWindow(this.kinectDepthWindowName);
                kinectThresholdWindowName = "Threshold window";
                CvInvoke.cvNamedWindow(this.kinectThresholdWindowName);

                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }
        }

        void sensor_AllFramesReady(object sender, Microsoft.Kinect.AllFramesReadyEventArgs e)
        {
            using (var colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame == null)
                {
                    return;
                }

                using (var depthFrame = e.OpenDepthImageFrame())
                {
                    if (depthFrame == null)
                    {
                        return;
                    }             
                    colorFrame.CopyPixelDataTo(this.colorPixels);
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixes);

                    GCHandle handle = GCHandle.Alloc(this.colorPixels, GCHandleType.Pinned);
                    Bitmap image = new Bitmap(colorFrame.Width,
                        colorFrame.Height,
                        colorFrame.Width << 2, System.Drawing.Imaging.PixelFormat.Format32bppRgb,
                        handle.AddrOfPinnedObject());
                    handle.Free();

                    GCHandle handle2 = GCHandle.Alloc(this.depthPixes, GCHandleType.Pinned);
                    Bitmap image2 = new Bitmap(depthFrame.Width,
                        depthFrame.Height,
                        depthFrame.Width << 2, System.Drawing.Imaging.PixelFormat.Format32bppRgb,
                        handle2.AddrOfPinnedObject());
                    handle2.Free();

                    var colorImage = new Image<Bgr, byte>(image);
                    var openCvImgGrayscale = new Image<Gray, byte>(image);
                    var depthImage = new Image<Bgr, byte>(image2);
                    image.Dispose();

                    //  Get points form depth sensor
                    var depthImagePoints = new k.DepthImagePoint[colorFrame.Width * colorFrame.Height];

                    // Map color and depth frame from Kinect
                    var mapper = new k.CoordinateMapper(sensor);
                    mapper.MapColorFrameToDepthFrame(colorFrame.Format, depthFrame.Format, depthPixes,
                        depthImagePoints);


                    // Get threshold value
                    var thresholdMin = 125;
                    var thresholdMax = 255;

                    // Thresholding
                    var trimmedColorImage = ExtractSubSection(colorImage);

                    var thresholdImage = openCvImgGrayscale.ThresholdBinary(new Gray(thresholdMin), new Gray(thresholdMax));

                    var trimmedthresholdImage = ExtractSubSection(thresholdImage);


                    thresholdImage.SmoothMedian(3);
                    var rectangle = FindContours(trimmedthresholdImage, trimmedColorImage);

                    if (rectangle.HasValue)
                    {
                        this.paperRect = rectangle.Value;

                        int x = paperRect.X + paperRect.Width;
                        int y = paperRect.Y + paperRect.Height;

                        // Find where the X,Y point is in the 1-D array of color frame
                        var index = y * colorFrame.Width + x;

                        // Let's choose point e.g. (x, y)
                        trimmedColorImage.Draw(new Cross2DF(new PointF(x, y), 2.0f, 2.0f), new Bgr(Color.White), 1);

                        // Draw it on depth image
                        depthImage.Draw(new Cross2DF(
                            new PointF(depthImagePoints[index].X, depthImagePoints[index].Y),
                            2.0f, 2.0f), new Bgr(Color.White), 1);

                        // Draw on color image the B-box found
                        trimmedthresholdImage.Draw(paperRect, new Gray(0), 2);

                        // Get the point in skeleton space
                        this.skeletonPoint = mapper.MapDepthPointToSkeletonPoint(depthFrame.Format,
                            depthImagePoints[index]);
                    }
                    
                    CvInvoke.cvShowImage(this.kinectColorWindowName, trimmedColorImage);
                    CvInvoke.cvShowImage(this.kinectDepthWindowName, depthImage);
                    CvInvoke.cvShowImage(this.kinectThresholdWindowName, trimmedthresholdImage);
                }
            }
        }

        public Vector3 convertKinectToProjector(Vector3 kinectPoint)
        {
            // Transform by transformation matrix
            var pointTranlated = transformMat * kinectPoint;

            // Print it please
            Console.WriteLine("Transformed point X={0} Y={1} Z={2}",
                pointTranlated.x, pointTranlated.y, pointTranlated.z);

            return pointTranlated;
        }

        private static Image<TColor, TDepth> ExtractSubSection<TColor, TDepth>(Image<TColor, TDepth> sourceImage)
            where TColor : struct, IColor
            where TDepth : new()
        {
            int paddingTop = 100, paddingBottom = 150;
            int paddingLeft = 125, paddingRight = 125;

            Image<TColor, TDepth> maskImage = new Image<TColor, TDepth>(sourceImage.Size);


            maskImage.SetZero();

            Image<TColor, TDepth> sect;
            try
            {
                for (int row = paddingTop; row < sourceImage.Height - paddingBottom; row++)
                {
                    for (int col = paddingLeft; col < sourceImage.Width - paddingRight; col++)
                    {
                        maskImage[row, col] = sourceImage[row, col];
                    }
                }

                //sect = sourceImage.GetSubRect(new Rectangle(paddingLeft, paddingTop,
                //    sourceImage.Width - paddingRight,
                //    sourceImage.Height - paddingBottom));
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return maskImage;
        }

        private static Rectangle? FindContours(Image<Gray, byte> thresholdImage, Image<Bgr, byte> coloImage)
        {
            using (var storage = new MemStorage())
            {
                // Find contours
                var contours = thresholdImage.FindContours(
                    CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                    RETR_TYPE.CV_RETR_TREE,
                    storage);

                if (contours != null)
                {
                    var polygonPoints = contours.ApproxPoly(contours.Perimeter*0.05);
                    coloImage.Draw(polygonPoints, new Bgr(Color.Yellow), 2);

                    return contours.BoundingRectangle;
                }
            }

            return null;
        }


        /************************************************************************/
        /* create a scene to render                                             */
        /************************************************************************/
        public void CreateScene()
        {
            float distanceCameraProjection = -0.18f;
            float cameraAngelDeg = 70.0f;
            float heightCamera = 84.0f;

            // set a dark ambient light
            mEngine.SceneMgr.AmbientLight = new ColourValue(0.1f, 0.1f, 0.1f);

            // place the camera to a better position
            mEngine.Camera.Position = new Vector3(0.0f, 0.0f, -2.0f);
            mEngine.Camera.Direction = Vector3.UNIT_Z;
           
           // mEngine.Camera.LookAt(Vector3.UNIT_Z);

            InitializeViewAndProjectionMatrices();

            // Create transform matrix
            CreateTransformMatrix(heightCamera, distanceCameraProjection, cameraAngelDeg);

            this.inverseTransformMat = transformMat.Inverse();

            this.viewMatrix = this.viewMatrix * inverseTransformMat;
           
            // Apply our matrices to the camera
            mEngine.Camera.SetCustomProjectionMatrix(true, this.projectionMatrix);
            mEngine.Camera.SetCustomViewMatrix(true, this.viewMatrix);

            Console.WriteLine("Transform X={0} Y={1} Z={2}", this.viewMatrix[0,3],this.viewMatrix[1,3],this.viewMatrix[2,3]);
            
            // create one bright front light
            mLight1 = mEngine.SceneMgr.CreateLight("LIGHT1");
            mLight1.Type = Light.LightTypes.LT_POINT;
            mLight1.DiffuseColour = new ColourValue(1.0f, 0.975f, 0.85f);
            mLight1.Position = new Vector3(0f, 1f, 0f);
            mEngine.SceneMgr.RootSceneNode.AttachObject(mLight1);

            const float paperWidth = 0.1f;
            const float paperHeight = 0.1f;

            var plane = new Plane(Vector3.UNIT_Y, 0);
            
            //MeshManager.Singleton.Create(,
            //    ResourceGroupManager.DEFAULT_RESOURCE_GROUP_NAME, 
            //    plane,
            //    paperWidth, paperHeight, 
            //    20, 20, true, 1, 5, 5, Vector3.UNIT_Z);

            //Entity groundEnt = mEngine.SceneMgr.CreateEntity("GroundEntity", "ground");
            cSceneNode = mEngine.SceneMgr.RootSceneNode.CreateChildSceneNode();
            cSceneNode.SetPosition(0.0f, 0.0f, 0.0f);
            cSceneNode.Scale(new Vector3(0.1f, 0.1f, 0.1f));
            ManualObject manualObject = CreateMesh("Cube", "my1_mycolor");
            manualObject.CastShadows = false;
            cSceneNode.AttachObject(manualObject);

            //groundEnt.SetMaterialName("my1_mycolor");
            //groundEnt.CastShadows = false;
            //cSceneNode.AttachObject(groundEnt);

 


        }

        private void InitializeViewAndProjectionMatrices()
        {
            // Read from settings
            var calibrationReader = new CalibrationSettingsReader("device_0.txt");
            calibrationReader.Read();

            // Set Projection matrix
            this.projectionMatrix = calibrationReader.ProjectionMatrix;

            // View Matrix
            this.viewMatrix = calibrationReader.ViewMatrix;

        }

        ManualObject CreateMesh(String name, String matName)
        {

            ManualObject cube = new ManualObject(name);
            cube.Begin(matName);

            cube.Position(0.5f, -0.5f, 1.0f); cube.Normal(0.408248f, -0.816497f, 0.408248f); cube.TextureCoord(1, 0);
            cube.Position(-0.5f, -0.5f, 0.0f); cube.Normal(-0.408248f, -0.816497f, -0.408248f); cube.TextureCoord(0, 1);
            cube.Position(0.5f, -0.5f, 0.0f); cube.Normal(0.666667f, -0.333333f, -0.666667f); cube.TextureCoord(1, 1);
            cube.Position(-0.5f, -0.5f, 1.0f); cube.Normal(-0.666667f, -0.333333f, 0.666667f); cube.TextureCoord(0, 0);
            cube.Position(0.5f, 0.5f, 1.0f); cube.Normal(0.666667f, 0.333333f, 0.666667f); cube.TextureCoord(1, 0);
            cube.Position(-0.5f, -0.5f, 1.0f); cube.Normal(-0.666667f, -0.333333f, 0.666667f); cube.TextureCoord(0, 1);
            cube.Position(0.5f, -0.5f, 1.0f); cube.Normal(0.408248f, -0.816497f, 0.408248f); cube.TextureCoord(1, 1);
            cube.Position(-0.5f, 0.5f, 1.0f); cube.Normal(-0.408248f, 0.816497f, 0.408248f); cube.TextureCoord(0, 0);
            cube.Position(-0.5f, 0.5f, 0.0f); cube.Normal(-0.666667f, 0.333333f, -0.666667f); cube.TextureCoord(0, 1);
            cube.Position(-0.5f, -0.5f, 0.0f); cube.Normal(-0.408248f, -0.816497f, -0.408248f); cube.TextureCoord(1, 1);
            cube.Position(-0.5f, -0.5f, 1.0f); cube.Normal(-0.666667f, -0.333333f, 0.666667f); cube.TextureCoord(1, 0);
            cube.Position(0.5f, -0.5f, 0.0f); cube.Normal(0.666667f, -0.333333f, -0.666667f); cube.TextureCoord(0, 1);
            cube.Position(0.5f, 0.5f, 0.0f); cube.Normal(0.408248f, 0.816497f, -0.408248f); cube.TextureCoord(1, 1);
            cube.Position(0.5f, -0.5f, 1.0f); cube.Normal(0.408248f, -0.816497f, 0.408248f); cube.TextureCoord(0, 0);
            cube.Position(0.5f, -0.5f, 0.0f); cube.Normal(0.666667f, -0.333333f, -0.666667f); cube.TextureCoord(1, 0);
            cube.Position(-0.5f, -0.5f, 0.0f); cube.Normal(-0.408248f, -0.816497f, -0.408248f); cube.TextureCoord(0, 0);
            cube.Position(-0.5f, 0.5f, 1.0f); cube.Normal(-0.408248f, 0.816497f, 0.408248f); cube.TextureCoord(1, 0);
            cube.Position(0.5f, 0.5f, 0.0f); cube.Normal(0.408248f, 0.816497f, -0.408248f); cube.TextureCoord(0, 1);
            cube.Position(-0.5f, 0.5f, 0.0f); cube.Normal(-0.666667f, 0.333333f, -0.666667f); cube.TextureCoord(1, 1);
            cube.Position(0.5f, 0.5f, 1.0f); cube.Normal(0.666667f, 0.333333f, 0.666667f); cube.TextureCoord(0, 0);


            cube.Triangle(0, 1, 2); cube.Triangle(3, 1, 0);
            cube.Triangle(4, 5, 6); cube.Triangle(4, 7, 5);
            cube.Triangle(8, 9, 10); cube.Triangle(10, 7, 8);
            cube.Triangle(4, 11, 12); cube.Triangle(4, 13, 11);
            cube.Triangle(14, 8, 12); cube.Triangle(14, 15, 8);
            cube.Triangle(16, 17, 18); cube.Triangle(16, 19, 17);
            cube.End();

            return cube;

        }

        /// <summary>
        /// Create transformation matrix
        /// </summary>
        /// <param name="cameraHeighInMeters">Camera height in meters</param>
        /// <param name="zTransl">Distance between origin on projection and camera</param>
        /// <param name="angleOfTheCameraInDeg">Camera angle</param>
        private void CreateTransformMatrix(
            float cameraHeighInMeters, 
            float zTransl,
            float angleOfTheCameraInDeg)
        {
            this.transformMat = new Matrix4();

                this.transformMat.MakeTransform(
                new Vector3(0, cameraHeighInMeters, zTransl),
                new Vector3(1, 1, 1),
                new Quaternion(new Radian(new Degree(angleOfTheCameraInDeg)), new Vector3(1, 0, 0)));
        }

        /************************************************************************/
        /* update objects in the scene                                          */
        /************************************************************************/
        public void UpdateScene()
        {
            if (this.cSceneNode != null)
            {

                var newPoint = convertKinectToProjector(new Vector3(-skeletonPoint.X, skeletonPoint.Y, skeletonPoint.Z));

                this.cSceneNode.SetPosition(newPoint.x, newPoint.y, newPoint.z);
            }
            mStateMgr.Update(0);
        }

        /************************************************************************/
        /*                                                                      */
        /************************************************************************/
        public void RemoveScene()
        {
            // Shut down the kinect 
            if (sensor != null)
            {
                sensor.Stop();
            }

            // check if light 2 exists
            if (mLight2 != null)
            {
                // remove light 2 from scene and destroy it
                mEngine.SceneMgr.RootSceneNode.DetachObject(mLight2);
                mEngine.SceneMgr.DestroyLight(mLight2);
                mLight2 = null;
            }

            // check if light 1 exists
            if (mLight1 != null)
            {
                // remove light 1 from scene and destroy it
                mEngine.SceneMgr.RootSceneNode.DetachObject(mLight1);
                mEngine.SceneMgr.DestroyLight(mLight1);
                mLight1 = null;
            }
        }
    } // class

} // namespace
