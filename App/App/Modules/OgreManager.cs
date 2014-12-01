using System;
using System.Collections.Generic;
using Mogre;

namespace Origami.Modules
{
    /************************************************************************/
    /* ogre manager                                                         */
    /************************************************************************/
    public class OgreManager
    {
        //////////////////////////////////////////////////////////////////////////
        private Root mRoot;
        private Viewport mViewport;
        private ResourceManager mResourceMgr;

        private const string RenderWindowTitle = "Origami";

        /// <summary>
        /// Flag is true if rendering is currently active
        /// </summary>
        public bool RenderingActive { get; private set; }

        // reference to Ogre render window
        public RenderWindow Window { get; private set; }

        // reference to scene manager
        public SceneManager SceneMgr { get; private set; }

        // reference to camera
        public Camera Camera { get; private set; }

        // events raised when direct 3D device is lost or restored
        public event EventHandler<OgreEventArgs> DeviceLost;
        public event EventHandler<OgreEventArgs> DeviceRestored;

        /// <summary>
        /// Constructor
        /// </summary>
        internal OgreManager()
        {
            mRoot = null;
            this.Window = null;
            this.SceneMgr = null;
            this.Camera = null;
            mViewport = null;
            this.RenderingActive = false;
            mResourceMgr = null;
        }

        /// <summary>
        /// start up ogre manager
        /// </summary>
        /// <returns></returns>
        internal bool Startup()
        {
        
            // check if already initialized
            if (mRoot != null)
            {
                return false;
            }

            // create ogre root
            mRoot = new Root("plugins.cfg", "settings.cfg", "mogre.log");

            // Read config file
            Config.ReadFromFile("config.json");

            // set render system
            mRoot.RenderSystem = mRoot.GetRenderSystemByName("Direct3D9 Rendering Subsystem");

            // register event to get notified when application lost or regained focus
            mRoot.RenderSystem.EventOccurred += OnRenderSystemEventOccurred;

            // initialize engine
            mRoot.Initialise(false);

            // optional parameters
            var parm = new NameValuePairList();
            parm["vsync"] = "true";

            // create windowl
            this.Window = mRoot.CreateRenderWindow(RenderWindowTitle, 1024, 768, Config.Instance.IsFullScreen, parm);
            
            // create scene manager
            this.SceneMgr = mRoot.CreateSceneManager(SceneType.ST_GENERIC, "DefaultSceneManager");

            // create default camera
            this.Camera = this.SceneMgr.CreateCamera("DefaultCamera");
            //this.Camera.AutoAspectRatio = true;
            //this.Camera.NearClipDistance = 1.0f;
            //this.Camera.FarClipDistance = 1000.0f;

            // create default viewport
            mViewport = this.Window.AddViewport(this.Camera);

            // create resource manager and initialize it
            mResourceMgr = new ResourceManager();
            if (!mResourceMgr.Startup("resources.cfg"))
                return false;

            // set rendering active flag
            this.RenderingActive = true;

            // OK
            return true;
        }

        /************************************************************************/
        /* shut down ogre manager                                               */
        /************************************************************************/
        internal void Shutdown()
        {
            // shutdown resource manager
            if (mResourceMgr != null)
            {
                mResourceMgr.Shutdown();
                mResourceMgr = null;
            }

            // shutdown ogre root
            if (mRoot != null)
            {
                // deregister event to get notified when application lost or regained focus
                mRoot.RenderSystem.EventOccurred -= OnRenderSystemEventOccurred;

                // shutdown ogre
                mRoot.Dispose();
            }
            mRoot = null;

            // forget other references to ogre systems
            this.Window = null;
            this.SceneMgr = null;
            this.Camera = null;
            mViewport = null;
            this.RenderingActive = false;
        }

        /************************************************************************/
        /* update ogre manager, also processes the systems event queue          */
        /************************************************************************/
        internal void Update()
        {
            // check if ogre manager is initialized
            if (mRoot == null)
                return;

            // process windows event queue (only if no external window is used)
            WindowEventUtilities.MessagePump();

            // render next frame
            if (this.RenderingActive)
                mRoot.RenderOneFrame();
        }

        /************************************************************************/
        /* handle device lost and device restored events                        */
        /************************************************************************/
        private void OnRenderSystemEventOccurred(string eventName, Const_NameValuePairList parameters)
        {
            EventHandler<OgreEventArgs> evt;
            OgreEventArgs args;

            // check which event occured
            switch (eventName)
            {
                // direct 3D device lost
                case "DeviceLost":
                    // don't set mRenderingActive to false here, because ogre will try to restore the
                    // device in the RenderOneFrame function and mRenderingActive needs to be set to true
                    // for this function to be called

                    // event to raise is device lost event
                    evt = DeviceLost;

                    // on device lost, create empty ogre event args
                    args = new OgreEventArgs();
                    break;

                // direct 3D device restored
                case "DeviceRestored":
                    uint width;
                    uint height;
                    uint depth;

                    // event to raise is device restored event
                    evt = DeviceRestored;

                    // get metrics for the render window size
                    this.Window.GetMetrics(out width, out height, out depth);

                    // on device restored, create ogre event args with new render window size
                    args = new OgreEventArgs((int)width, (int)height);
                    break;

                default:
                    return;
            }

            // raise event with provided event args
            if (evt != null)
                evt(this, args);
        }

        /************************************************************************/
        /* create a simple object just consisting of a scenenode with a mesh    */
        /************************************************************************/
        internal SceneNode CreateSimpleObject(string name, string mesh)
        {
            // if scene manager already has an object with the requested name, fail to create it again
            if (this.SceneMgr.HasEntity(name) || this.SceneMgr.HasSceneNode(name))
                return null;

            // create entity and scenenode for the object
            Entity entity;
            try
            {
                // try to create entity from mesh
                entity = this.SceneMgr.CreateEntity(name, mesh);
            }
            catch
            {
                // failed to create entity
                return null;
            }

            // add entity to scenenode
            var node = this.SceneMgr.CreateSceneNode(name);

            // connect entity to the scenenode
            node.AttachObject(entity);

            // return the created object
            return node;
        }

        /************************************************************************/
        /* destroy an object                                                    */
        /************************************************************************/
        internal void DestroyObject(SceneNode node)
        {
            // check if object has a parent node...
            if (node.Parent != null)
            {
                // ...if so, remove it from its parent node first
                node.Parent.RemoveChild(node);
            }

            // first remove all child nodes (they are not destroyed here !)
            node.RemoveAllChildren();

            // create a list of references to attached objects
            var objList = new List<MovableObject>();

            // get number of attached objects
            ushort count = node.NumAttachedObjects();

            // get all attached objects references
            for (ushort i = 0; i < count; ++i)
                objList.Add(node.GetAttachedObject(i));

            // detach all objects from node
            node.DetachAllObjects();

            // destroy all previously attached objects
            foreach (var obj in objList)
                this.SceneMgr.DestroyMovableObject(obj);

            // destroy scene node
            this.SceneMgr.DestroySceneNode(node);
        }

        /************************************************************************/
        /* add an object to the scene                                           */
        /************************************************************************/
        internal void AddObjectToScene(SceneNode node)
        {
            // check if object is already has a parent
            if (node.Parent != null)
            {
                // check if object is in scene already, then we are done
                if (node.Parent == this.SceneMgr.RootSceneNode)
                    return;

                // otherwise remove the object from its current parent
                node.Parent.RemoveChild(node);
            }

            // add object to scene
            this.SceneMgr.RootSceneNode.AddChild(node);
        }

        /************************************************************************/
        /* add an object to another object as child                             */
        /************************************************************************/
        internal void AddObjectToObject(SceneNode node, SceneNode newParent)
        {
            // check if object is already has a parent
            if (node.Parent != null)
            {
                // check if object is in scene already, then we are done
                if (node.Parent == newParent)
                    return;

                // otherwise remove the object from its current parent
                node.Parent.RemoveChild(node);
            }

            // add object to scene
            newParent.AddChild(node);
        }

        /************************************************************************/
        /* remove object from scene                                             */
        /************************************************************************/
        internal void RemoveObjectFromScene(SceneNode node)
        {
            // if object is attached to a node
            if (node.Parent != null)
            {
                // remove object from its parent
                node.Parent.RemoveChild(node);
            }
        }

    } // class

} // namespace
