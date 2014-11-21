using System;
using System.Diagnostics;
using Origami.Modules;
using Origami.States;
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

                // Initialize buffer for pixels from kinect
                this.colorPixels = new byte[this.sensor.ColorStream.FramePixelDataLength];

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
        }

        void sensor_AllFramesReady(object sender, Microsoft.Kinect.AllFramesReadyEventArgs e)
        {
            using (var colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame == null)
                {
                    return;
                }

                colorFrame.CopyPixelDataTo(this.colorPixels);

                GCHandle handle = GCHandle.Alloc(this.colorPixels, GCHandleType.Pinned);
                Bitmap image = new Bitmap(colorFrame.Width,
                    colorFrame.Height,
                    colorFrame.Width << 2, System.Drawing.Imaging.PixelFormat.Format32bppRgb, handle.AddrOfPinnedObject());
                handle.Free();
                var openCvImgColour = new Image<Bgr, byte>(image);
                var openCvImgGrayscale = new Image<Gray, byte>(image);
                image.Dispose();

                var subSection = ExctractSubSection(openCvImgGrayscale);
                var subColSection = ExctractSubSection(openCvImgColour);

                // Get threshold value
                var thresholdMin = 60;
                var thresholdMax = 255;

                // Thresholding
                var thresholdImage = subSection.ThresholdBinary(new Gray(thresholdMin), new Gray(thresholdMax));

                var rectangle = FindContours(thresholdImage, subColSection);

                if (rectangle.HasValue)
                {
                    this.paperRect = rectangle.Value;
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
            var matProj = new Matrix4(
                4.058233684232872f, 0, 0.1712672322695396f, 0,
                0, 5.330161980980033f, 0.6174778127392031f, 0,
                0, 0, -1.02020202020202f, -0.202020202020202f,
                0, 0, -1, 0);

            // View Matrix
            var matView = new Matrix4(
                0.9998472841723921f, 0.0009793712472160466f, -0.01744847171106832f, -0.05399500685689796f,
                -0.002812918702471569f, 0.9944289344642237f, -0.1053716365476118f, 0.08005271442927822f,
                -0.01724806718055999f, -0.1054046257633356f, -0.9942798243181977f, 0.0005856650549028571f,
                0, 0, 0, 1);

            const float cameraHeighInMeters = 0.45f;
            var angleOfTheCameraInDeg = new Degree(56);

            var transformMat = new Matrix4();
            transformMat.MakeTransform(new Vector3(0, cameraHeighInMeters, 0), 
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
            
            var paperMesh = MeshManager.Singleton.CreatePlane("ground", ResourceGroupManager.DEFAULT_RESOURCE_GROUP_NAME, plane,
                sizeOfSquareInM, sizeOfSquareInM, 
                20, 20, true, 1, 5, 5, Vector3.UNIT_Z);

            Entity groundEnt = mEngine.SceneMgr.CreateEntity("GroundEntity", "ground");
            cSceneNode = mEngine.SceneMgr.RootSceneNode.CreateChildSceneNode();

            const float xPositionSquare = 0.07f;
            const float yPositionSquare = 0f;
            const float zPositionSquare = 0.53f; //0.62f;

            // Set the square 
            cSceneNode.SetPosition(xPositionSquare, yPositionSquare, zPositionSquare);
            cSceneNode.AttachObject(groundEnt);
            groundEnt.SetMaterialName("Examples/Rockwall");
            groundEnt.CastShadows = false;
            

            Console.WriteLine("Camera X:{0} Y:{1} Z:{2}", mEngine.Camera.Position.x, mEngine.Camera.Position.y, mEngine.Camera.Position.z);
        }

        /************************************************************************/
        /* update objects in the scene                                          */
        /************************************************************************/
        public void UpdateScene()
        {


            var paperRectangle = this.paperRect;
            var paperX = Convert.ToSingle(paperRectangle.X);
            var paperY = Convert.ToSingle(paperRectangle.Y);
            var paperWidth = Convert.ToSingle(paperRectangle.Width);
            var paperHeight = Convert.ToSingle(paperRectangle.Height);

            const float xPositionSquare = 0.07f;
            const float yPositionSquare = 0f;

            if (cSceneNode != null)
            {
                cSceneNode.SetPosition(xPositionSquare, yPositionSquare, zPositionSquare);
            }

            zPositionSquare -= 0.0000001f;

            // update the state manager, this will automatically update the active state
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
