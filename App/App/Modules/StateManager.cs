using System;
using System.Reflection;

namespace Quickstart2010.Modules
{
  /************************************************************************/
  /* state manager for program states                                     */
  /************************************************************************/
  public class StateManager
  {
    //////////////////////////////////////////////////////////////////////////
    private OgreManager mEngine;

    //////////////////////////////////////////////////////////////////////////
    private State mCurrentState;
    private Type mNewState;

    // reference to the Ogre engine manager //////////////////////////////////
    public OgreManager Engine
    {
      get { return mEngine; }
    }

    /************************************************************************/
    /* constructor                                                          */
    /************************************************************************/
    public StateManager( OgreManager _engine )
    {
      mEngine = _engine;
      mCurrentState = null;
      mNewState = null;
    }

    /************************************************************************/
    /* start up and initialize the first state                              */
    /************************************************************************/
    public bool Startup( Type _firstState )
    {
      // can't start up the state manager again if it's already running
      if( mCurrentState != null || mNewState != null )
        return false;

      // initialize with first state
      if( !RequestStateChange( _firstState ) )
        return false;

      // OK
      return true;
    }

    /************************************************************************/
    /* shut down                                                            */
    /************************************************************************/
    public void Shutdown()
    {
      // if a state is active, shut down the state to clean up
      if( mCurrentState != null )
        SwitchToNewState( null );

      // make sure any pending state change request is reset
      mNewState = null;
    }

    /************************************************************************/
    /* update                                                               */
    /************************************************************************/
    public void Update( long _frameTime )
    {
      // check if a state change was requested
      if( mNewState != null )
      {
        State newState = null;

        // use reflection to get new state class default constructor
        ConstructorInfo constructor = mNewState.GetConstructor( Type.EmptyTypes );

        // try to create an object from the requested state class
        if( constructor != null )
          newState = (State) constructor.Invoke( null );

        // switch to the new state if an object of the requested state class could be created
        if( newState != null )
          SwitchToNewState( newState );

        // reset state change request until next state change is requested
        mNewState = null;
      }

      // if a state is active, update the active state
      if( mCurrentState != null )
        mCurrentState.Update( _frameTime );
    }

    /************************************************************************/
    /* set next state that should be switched to, returns false if invalid  */
    /************************************************************************/
    public bool RequestStateChange( Type _newState )
    {
      // new state class must be derived from base class "State"
      if( _newState == null || !_newState.IsSubclassOf( typeof( State ) ) )
        return false;

      // don't change the state if the requested state class matches the current state
      if( mCurrentState != null && mCurrentState.GetType() == _newState )
        return false;

      // store type of new state class to request a state change
      mNewState = _newState;

      // OK
      return true;
    }

    //////////////////////////////////////////////////////////////////////////
    // internal functions ////////////////////////////////////////////////////
    //////////////////////////////////////////////////////////////////////////

    /************************************************************************/
    /* change from one state to another state                               */
    /************************************************************************/
    private void SwitchToNewState( State _newState )
    {
      // if a state is active, shut it down
      if( mCurrentState != null )
        mCurrentState.Shutdown();

      // switch to the new state, might be null if no new state should be activated
      mCurrentState = _newState;

      // if a state is active, start it up
      if( mCurrentState != null )
        mCurrentState.Startup( this );
    }

  } // class

} // namespace
