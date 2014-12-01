using System;
using System.Collections.Generic;
using System.Linq;
using Origami.Modules;
using Origami.States;
using Origami.Utilities;
using Kinect = Microsoft.Kinect;
using Mogre;
using System.IO;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Origami
{
    public class Program
    {

        private const int CV_WND_PROP_FULLSCREEN = 0;
        public const string OPENCV_HIGHGUI_LIBRARY = "opencv_highgui240";
        public const UnmanagedType StringMarshalType = UnmanagedType.LPStr;
        private const int CV_WINDOW_FULLSCREEN = 1;
        

        [DllImport(OPENCV_HIGHGUI_LIBRARY, CallingConvention = CvInvoke.CvCallingConvention, EntryPoint = "cvSetWindowProperty")]
        private static extern void _cvSetWindowProperty([MarshalAs(StringMarshalType)] String name, int prop, double propvalue);

        [DllImport(OPENCV_HIGHGUI_LIBRARY, CallingConvention = CvInvoke.CvCallingConvention, EntryPoint = "cvNamedWindow")]
        private static extern void _cvNamedWindow([MarshalAs(StringMarshalType)] String name, int prop);

        /// <summary>
        /// Creates a window which can be used as a placeholder for images and trackbars. Created windows are reffered by their names. 
        /// If the window with such a name already exists, the function does nothing.
        /// </summary>
        /// <param name="name">Name of the window which is used as window identifier and appears in the window caption</param>
        public static void cvSetWindowProperty(String name)
        {
            //return _cvSetWindowProperty(name, CV_WND_PROP_FULLSCREEN, CV_WINDOW_FULLSCREEN);
            _cvSetWindowProperty(name, CV_WND_PROP_FULLSCREEN, CV_WINDOW_FULLSCREEN);
        }

        //////////////////////////////////////////////////////////////////////////
        private static OgreManager mEngine;
        private static StateManager mStateMgr;

        //////////////////////////////////////////////////////////////////////////
        private Light mLight1;
        private Light mLight2;

        ////** KINECT STUFF **
        private readonly Kinect.KinectSensor sensor;
        private readonly byte[] colorPixels;

        /// <summary>
        /// Paper position, width and height detected by Kinect
        /// </summary>
        private SceneNode cSceneNode;

        private readonly Kinect.DepthImagePixel[] depthPixes;

        private const string KinectColorWindowName = "Kinect color";
        private const string KinectDepthWindowName = "Kinect depth";
        private const string KinectThresholdWindowName = "Threshold window";

        private Kinect.SkeletonPoint skeletonPoint;
        private Matrix4 projectionMatrix;
        private Matrix4 viewMatrix;
        private Matrix4 transformMat;
        private Matrix4 inverseTransformMat;
        private readonly IList<Kinect.SkeletonPoint> skeletonPoints = new List<Kinect.SkeletonPoint>();
        private ManualObject origamiMesh;

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
            var prg = new Program();

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

            foreach (var potentialSensor in Kinect.KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == Kinect.KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }


            if (null != this.sensor)
            {
                this.sensor.ColorStream.Enable(Kinect.ColorImageFormat.RgbResolution640x480Fps30);
                this.sensor.DepthStream.Enable(Kinect.DepthImageFormat.Resolution640x480Fps30);

                // Initialize buffer for pixels from kinect
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];
                this.depthPixes = new Kinect.DepthImagePixel[this.sensor.DepthStream.FramePixelDataLength];
                this.sensor.AllFramesReady += sensor_AllFramesReady;

                CvInvoke.cvNamedWindow(KinectColorWindowName);
                CvInvoke.cvNamedWindow(KinectDepthWindowName);
                _cvNamedWindow(KinectThresholdWindowName, 0x00000001);
                //cvSetWindowProperty(KinectThresholdWindowName);
                Console.WriteLine("RET: {0}");

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

        void sensor_AllFramesReady(object sender, Kinect.AllFramesReadyEventArgs e)
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

                    var handle = GCHandle.Alloc(this.colorPixels, GCHandleType.Pinned);
                    var image = new Bitmap(colorFrame.Width,
                        colorFrame.Height,
                        colorFrame.Width << 2, System.Drawing.Imaging.PixelFormat.Format32bppRgb,
                        handle.AddrOfPinnedObject());
                    handle.Free();

                    var handle2 = GCHandle.Alloc(this.depthPixes, GCHandleType.Pinned);
                    var image2 = new Bitmap(depthFrame.Width,
                        depthFrame.Height,
                        depthFrame.Width << 2, System.Drawing.Imaging.PixelFormat.Format32bppRgb,
                        handle2.AddrOfPinnedObject());
                    handle2.Free();

                    var colorImage = new Image<Bgr, byte>(image);
                    var openCvImgGrayscale = new Image<Gray, byte>(image);
                    var depthImage = new Image<Bgr, byte>(image2);
                    image.Dispose();

                    //  Get points form depth sensor
                    var depthImagePoints = new Kinect.DepthImagePoint[colorFrame.Width * colorFrame.Height];

                    // Map color and depth frame from Kinect
                    var mapper = new Kinect.CoordinateMapper(sensor);
                    mapper.MapColorFrameToDepthFrame(colorFrame.Format, depthFrame.Format, depthPixes,
                        depthImagePoints);


                    // Get threshold value
                    const int thresholdMin = 125;
                    const int thresholdMax = 255;

                    // Thresholding
                    var trimmedColorImage = ExtractSubSection(colorImage);

                    var thresholdImage = openCvImgGrayscale.ThresholdBinary(new Gray(thresholdMin), new Gray(thresholdMax));

                    var trimmedthresholdImage = ExtractSubSection(thresholdImage);


                    thresholdImage.SmoothMedian(3);


                    var testWindowContent = new Image<Bgr, byte>(trimmedColorImage.Size);

                    var points = FindContours(trimmedthresholdImage, testWindowContent);

                    lock (this.skeletonPoints)
                    {
                        this.skeletonPoints.Clear();
                    }

                    //testWindowContent;

               

                    foreach (var point in points)
                    {
                        // Find where the X,Y point is in the 1-D array of color frame
                        var index = point.Y * colorFrame.Width + point.X;

                        // Let's choose point e.g. (x, y)
                        trimmedColorImage.Draw(new Cross2DF(
                            new PointF(point.X, point.Y), 2.0f, 2.0f), new Bgr(Color.Red), 5);

                        // Draw it on depth image
                        depthImage.Draw(new Cross2DF(
                            new PointF(depthImagePoints[index].X, depthImagePoints[index].Y),
                            2.0f, 2.0f), new Bgr(Color.White), 1);
                        
                        

                        // Get the point in skeleton space
                        var sp = mapper.MapDepthPointToSkeletonPoint(
                            depthFrame.Format,
                            depthImagePoints[index]);

                        Console.WriteLine("Depth value is: {0}", depthImagePoints[index].Depth << Kinect.DepthImageFrame.PlayerIndexBitmaskWidth);

                        lock (this.skeletonPoints)
                        {

                            this.skeletonPoints.Add(sp);
                        }
                    }

                    this.skeletonPoint = this.skeletonPoints.FirstOrDefault();

                    CvInvoke.cvShowImage(KinectColorWindowName, trimmedColorImage);
                    CvInvoke.cvShowImage(KinectDepthWindowName, depthImage);
                    CvInvoke.cvShowImage(KinectThresholdWindowName, testWindowContent);
                }
            }
        }

        /// <summary>
        /// Transform kinect point from skeleton to scene space (use transform matrix)
        /// </summary>
        /// <param name="kinectPoint"></param>
        /// <returns></returns>
        public Vector3 ConvertKinectToProjector(Vector3 kinectPoint)
        {
            // Transform by transformation matrix
            var pointTranlated = transformMat*kinectPoint;
            return pointTranlated;
        }

        private static Image<TColor, TDepth> ExtractSubSection<TColor, TDepth>(Image<TColor, TDepth> sourceImage)
            where TColor : struct, IColor
            where TDepth : new()
        {
            // TODO: Read from config file
            int paddingTop = 100, paddingBottom = 150;
            int paddingLeft = 125, paddingRight = 125;

            var maskImage = new Image<TColor, TDepth>(sourceImage.Size);

            // Set to black
            maskImage.SetZero();

            // Copy values to the black mask
            for (var row = paddingTop; row < sourceImage.Height - paddingBottom; row++)
            {
                for (var col = paddingLeft; col < sourceImage.Width - paddingRight; col++)
                {
                    maskImage[row, col] = sourceImage[row, col];
                }
            }

            return maskImage;
        }

        private static IEnumerable<Point> FindContours(Image<Gray, byte> thresholdImage, 
            Image<Bgr, byte> testWindowContent)
        {
            var points = new List<Point>();

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

                    testWindowContent.Draw(polygonPoints, new Bgr(Color.Yellow), 2);

                    points.AddRange(polygonPoints.Select(polygonPoint => new Point(polygonPoint.X, polygonPoint.Y)));
                }
            }

            return points;
        }


        /************************************************************************/
        /* create a scene to render                                             */
        /************************************************************************/
        public void CreateScene()
        {
            float distanceCameraProjection = Config.Instance.CameraDistance;
            float cameraAngelDeg = Config.Instance.CameraAngle;
            float heightCamera = Config.Instance.CameraHeight;

            // set a dark ambient light
            mEngine.SceneMgr.AmbientLight = new ColourValue(0.1f, 0.1f, 0.1f);

            // place the camera to a better position
            mEngine.Camera.Position = new Vector3(0.0f, 0.0f, -10.0f);
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

            cSceneNode = mEngine.SceneMgr.RootSceneNode.CreateChildSceneNode();
            cSceneNode.SetPosition(0.0f, 0.0f, 0.0f);
            cSceneNode.Scale(new Vector3(1f, 1f, 1f));
            //cSceneNode.Rotate(new Vector3(1.0f, 0.0f, 0.0f), new Radian(new Degree(40)));

            origamiMesh = CreateMesh("Cube", "my1_mycolor");
            origamiMesh.CastShadows = false;
            cSceneNode.AttachObject(origamiMesh);

            //groundEnt.SetMaterialName("my1_myC");
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

        void UpdateMeshPoints(ManualObject mesh, IEnumerable<Vector3> points)
        {
            // Begin updating the mesh
            mesh.BeginUpdate(0);

            // Assign points
            var sortedPoints = points.OrderBy(a => a.x).ThenBy(b => b.y).ToList();


            //Console.WriteLine("I got {0} points", sortedPoints.Count);

            foreach (var point in sortedPoints)
            {
                float shiftX = -0.05f;
                float shiftY = -0.15f;
                float shiftZ = 0;//+0.04f;
                var pt = new Vector3(point.x + shiftX, point.y + shiftY, point.z + shiftZ);
               

                mesh.Position(pt);
                //Console.WriteLine("\t x={0} y={1} z={2}", point.x, point.y, point.z);
            }

            //Console.WriteLine();


            mesh.Index(0);
            mesh.Index(1);
            mesh.Index(3);
            mesh.Index(2);
            mesh.Index(0);

            mesh.End();
        }

        private static int PointSorter(Vector3 a, Vector3 b)
        {
            //  Reference Point is Vector2.ZERO
       
            // Ignore Z-coord for now

            // Calculate Atan
            var aTanA = System.Math.Atan2(a.y - Vector2.ZERO.y, a.x - Vector2.ZERO.x);
            var aTanB = System.Math.Atan2(b.y - Vector2.ZERO.y, b.x - Vector2.ZERO.x);

            //  Determine next point in Clockwise rotation
            if (aTanA < aTanB) return 1;
            else if (aTanA > aTanB) return -1;

            return 0;
        }

        ManualObject CreateMesh(string name, string matName)
        {

            var mesh = new ManualObject(name) {Dynamic = true};

            //var initialPoints = new List<dynamic>
            //{

            //    new {pos = new Vector3(-0.07937469f, -0.01292092f, 0.02449267f), col = ColourValue.Green},
            //    new {pos = new Vector3(-0.0352901f,  0.002292752f, -0.0735068f), col = ColourValue.Red},
            //    new {pos = new Vector3(0.05551887f, -0.01208323f,  0.08107224f), col = ColourValue.Blue},
            //    new {pos = new Vector3(0.09854966f, -4.094839E-05f,  -0.01187806f), col = ColourValue.Green},
            //};

            var initialPoints = new List<dynamic>
            {

                new {pos = new Vector3(-0.05152771f, -0.01892626f, 0.1056104f), col = ColourValue.Green},
                new {pos = new Vector3(0.02864167f, 0.0145198f, 0.1717866f), col = ColourValue.Red},
                new {pos = new Vector3(0.0447953f, 0.0007368922f,  -0.007466584f), col = ColourValue.Blue},
                new {pos = new Vector3(0.122395f, -0.008235455f,  0.06258318f), col = ColourValue.Green},
            };

             // x=-0.05152771 y=-0.01892626 z=0.1056104
             // x=0.02864167 y=-0.0245198 z=0.1717866
             // x=0.0447953 y=0.0007368922 z=-0.007466584
             // x=0.122395 y=-0.008235455 z=0.06258318


            // OT_TRIANGLE_STRIP - 3 vertices for the first triangle and 1 per triangle after that
            mesh.Begin(matName, RenderOperation.OperationTypes.OT_TRIANGLE_STRIP);

            foreach (var point in initialPoints)
            {
                mesh.Position(point.pos);
                mesh.Colour(point.col);
            }
            mesh.Index(0);
            mesh.Index(1);
            mesh.Index(2);
            mesh.Index(3);
            mesh.Index(0);
            
            
            ///,mesh.Triangle(0, 1, 2); 
            //cube.Triangle(3, 1, 0);
            
            mesh.End();

            return mesh;

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
            var initialPoints = new List<Vector3>
            {
                new Vector3(-.5f, .5f, 0.324f),
                new Vector3(-.5f, -.5f, 0.323f),
                new Vector3(.5f, -.5f, 0.324f),
                new Vector3(.5f, .5f, 0.322f)
            };

            if (this.cSceneNode != null && this.skeletonPoints != null)
            {
                var points = this.skeletonPoints.ToArray();

                if (points.Any())
                {
                    var scenePoints = points.ToArray().
                        Select(kinectPoint => ConvertKinectToProjector(
                            new Vector3(-kinectPoint.X, kinectPoint.Y, kinectPoint.Z))).ToList();


                    UpdateMeshPoints(origamiMesh, scenePoints);

                    //var newPoint = ConvertKinectToProjector(new Vector3(-skeletonPoint.X, skeletonPoint.Y, skeletonPoint.Z));
                    //this.cSceneNode.SetPosition(newPoint.x, newPoint.y, newPoint.z);
                }
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
