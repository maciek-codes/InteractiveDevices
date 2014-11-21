using System;

using Mogre;
using Origami.Modules;

namespace Origami.States
{
  /************************************************************************/
  /* program state that just shows a turning ogre head                    */
  /************************************************************************/
  public class TurningHead : State
  {
    //////////////////////////////////////////////////////////////////////////
    private OgreManager mEngine;

    //////////////////////////////////////////////////////////////////////////
    private SceneNode mOgreHead;

    /************************************************************************/
    /* constructor                                                          */
    /************************************************************************/
    public TurningHead()
    {
      mEngine = null;
      mOgreHead = null;
    }

    /************************************************************************/
    /* start up                                                             */
    /************************************************************************/
    public override bool Startup( StateManager _mgr )
    {
      // store reference to engine, this state does not need to store the state manager reference
      mEngine = _mgr.Engine;

      // create the ogre head and add the object to the current scene
      //mOgreHead = mEngine.CreateSimpleObject( "Ogre", "ogrehead.mesh" );
      //MeshManager.Singleton._initialise();
      //var plane = MeshManager.Singleton.CreatePlane(
      //    "paper",
      //    "paperGroup",
      //    new Plane(new Vector3(0f, 1f, 0f), 0f),
      //    200f,
      //    200f);
      //var entity = mEngine.SceneMgr.CreateEntity("paper");
      //var sceneNode = mEngine.SceneMgr.RootSceneNode.CreateChildSceneNode();
      //sceneNode.AttachObject(entity);
      //mEngine.AddObjectToScene(sceneNode);

      // OK
      return true;
    }

    /************************************************************************/
    /* shut down                                                            */
    /************************************************************************/
    public override void Shutdown()
    {
      // check if ogre head exists
      if( mOgreHead != null )
      {
        // remove ogre head from scene and destroy it
        mEngine.RemoveObjectFromScene( mOgreHead );
        mEngine.DestroyObject( mOgreHead );
        mOgreHead = null;
      }
    }

    /************************************************************************/
    /* update                                                               */
    /************************************************************************/
    public override void Update( long _frameTime )
    {
      // check if ogre head exists
      if( mOgreHead != null )
      {
        // rotate the ogre head a little bit
        mOgreHead.Rotate( Vector3.UNIT_Y, new Radian( new Degree( 0.5f ) ) );
      }
    }

  } // class

} // namespace
