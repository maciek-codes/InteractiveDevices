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
        private Matrix4 matProj;
        private Matrix4 matView;

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

                    //var subSection = ExctractSubSection(openCvImgGrayscale);
                    //var subColSection = ExctractSubSection(openCvImgColour);

                    // Get threshold value
                    var thresholdMin = 125;
                    var thresholdMax = 255;

                    // Thresholding
                    var thresholdImage = openCvImgGrayscale.ThresholdBinary(new Gray(thresholdMin), new Gray(thresholdMax));

                    thresholdImage.SmoothMedian(3);
                    var rectangle = FindContours(thresholdImage, colorImage);

                    if (rectangle.HasValue)
                    {
                        this.paperRect = rectangle.Value;
                    }
                    k.CoordinateMapper mapper = new k.CoordinateMapper(sensor);

                    k.DepthImagePoint[] depthImagePoints = new k.DepthImagePoint[colorFrame.Width*colorFrame.Height];
                    
                    mapper.MapColorFrameToDepthFrame(colorFrame.Format, depthFrame.Format, depthPixes,
                        depthImagePoints);

                    var index = paperRect.Y * colorFrame.Width + paperRect.X;

                    // Let's choose point (100, 100)
                    colorImage.Draw(new Cross2DF(new PointF(paperRect.X, paperRect.Y), 2.0f, 2.0f), new Bgr(Color.White), 1);

                    //Console.WriteLine("x={0} y={1}", depthImagePoints[index].X, depthImagePoints[index].Y);

                    depthImage.Draw(new Cross2DF(
                        new PointF(depthImagePoints[index].X, depthImagePoints[index].Y), 
                        2.0f, 2.0f), new Bgr(Color.White), 1);



                    this.skeletonPoint = mapper.MapDepthPointToSkeletonPoint(depthFrame.Format,
                        depthImagePoints[index]);

                    colorImage.Draw(paperRect, new Bgr(Color.Yellow), 2);

                    //Console.WriteLine("Skeleton Point x={0} y={1} z={2}", 
                     //   skeletonPoint.X, skeletonPoint.Y, skeletonPoint.Z);

                    CvInvoke.cvShowImage(this.kinectColorWindowName, colorImage);
                    CvInvoke.cvShowImage(this.kinectDepthWindowName, depthImage);
                    CvInvoke.cvShowImage(this.kinectThresholdWindowName, thresholdImage);
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
                sect = sourceImage.GetSubRect(new System.Drawing.Rectangle(150, 0,
                    400,
                    250));
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return sect;
        }

        private static System.Drawing.Rectangle? FindContours(Image<Gray, byte> thresholdImage, Image<Bgr, byte> subColSection)
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
            // set a dark ambient light
            mEngine.SceneMgr.AmbientLight = new ColourValue(0.1f, 0.1f, 0.1f);

            // place the camera to a better position
            mEngine.Camera.Position = new Vector3(0.0f, 0.0f, 0.0f);
            mEngine.Camera.Direction = new Vector3(0, 0, 1);
            //mEngine.Camera.LookAt(Vector3.ZERO);

            //mEngine.Camera.Roll(new Radian(new Degree(180)));

            // Propjection matrix
            var calibrationReader = new CalibrationSettingsReader("device_0.txt");
            calibrationReader.Read();

            this.matProj = calibrationReader.ProjectionMatrix;

            // View Matrix
            this.matView = calibrationReader.ViewMatrix;

            const float cameraHeighInMeters = 0.45f;
            var angleOfTheCameraInDeg = new Degree(78);

            var transformMat = new Matrix4();
            transformMat.MakeTransform(
                new Vector3(0, cameraHeighInMeters, 0), 
                new Vector3(1, 1, 1),
                new Quaternion(new Radian(angleOfTheCameraInDeg), new Vector3(1, 0, 0)));

            matView = matView * transformMat;

            
            mEngine.Camera.SetCustomProjectionMatrix(true, matProj);
            mEngine.Camera.SetCustomViewMatrix(true, matView);
            
            //mEngine.Camera.Roll(new Radian(new Degree(180)));

            // create one bright front light
            mLight1 = mEngine.SceneMgr.CreateLight("LIGHT1");
            mLight1.Type = Light.LightTypes.LT_POINT;
            mLight1.DiffuseColour = new ColourValue(1.0f, 0.975f, 0.85f);
            mLight1.Position = new Vector3(0f, 1f, 0f);
            mEngine.SceneMgr.RootSceneNode.AttachObject(mLight1);

            const float sizeOfSquareInM = 0.045f;

            var plane = new Plane(Vector3.UNIT_Y, 0);
            
            MeshManager.Singleton.CreatePlane("ground", ResourceGroupManager.DEFAULT_RESOURCE_GROUP_NAME, plane,
                sizeOfSquareInM, sizeOfSquareInM, 
                20, 20, true, 1, 5, 5, Vector3.UNIT_Z);

            Entity groundEnt = mEngine.SceneMgr.CreateEntity("GroundEntity", "ground");
            this.cSceneNode = mEngine.SceneMgr.RootSceneNode.CreateChildSceneNode();
      
            const float xPositionSquare = 0.05f;
            const float yPositionSquare = 0f;
            const float zPositionSquare = 0.53f; //0.62f;

            // Set the square 
            cSceneNode.SetPosition(xPositionSquare, yPositionSquare, zPositionSquare);

            groundEnt.SetMaterialName("my1_mycolor");
            groundEnt.CastShadows = false;
            cSceneNode.AttachObject(groundEnt);

 
        }

        /************************************************************************/
        /* update objects in the scene                                          */
        /************************************************************************/
        public void UpdateScene()
        {
            if (this.cSceneNode != null)
            {
                Console.WriteLine("Skeleton Point x={0} y={1} z={2}", skeletonPoint.X, skeletonPoint.Y, skeletonPoint.Z);
                Matrix4 viewProjectionInverse = matProj *
            matView;
                viewProjectionInverse = viewProjectionInverse.Inverse();

                // Do we need to inverse -x?
                Vector3 point3D  = new Vector3(-skeletonPoint.X, skeletonPoint.Y, skeletonPoint.Z);
                var point = viewProjectionInverse * point3D;
                this.cSceneNode.SetPosition(point.x, point.y, point.z);
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
