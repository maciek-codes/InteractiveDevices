using System;

namespace Quickstart2010.Modules
{
  /************************************************************************/
  /* event arguments for ogre events (device lost or restored)            */
  /************************************************************************/
  public class OgreEventArgs : EventArgs
  {
    //////////////////////////////////////////////////////////////////////////
    private int mWidth;
    private int mHeight;

    // get width of screen after device restored, 0 if device lost ///////////
    public int Width
    {
      get { return mWidth; }
    }

    // get height of screen after device restored, 0 if device lost //////////
    public int Height
    {
      get { return mHeight; }
    }

    /************************************************************************/
    /* constructor                                                          */
    /************************************************************************/
    public OgreEventArgs()
      : this( 0, 0 )
    {
    }

    /************************************************************************/
    /* constructor                                                          */
    /************************************************************************/
    public OgreEventArgs( int _width, int _height )
    {
      // store width and height
      mWidth = _width;
      mHeight = _height;
    }

  } // class

} // namespace
