using System;
using System.Collections.Generic;
using System.IO;

using Mogre;

namespace Quickstart2010.Modules
{
  /************************************************************************/
  /* resource manager uses ogre resource group manager                    */
  /************************************************************************/
  public class ResourceManager
  {
    //////////////////////////////////////////////////////////////////////////

    /************************************************************************/
    /* constructor                                                          */
    /************************************************************************/
    internal ResourceManager()
    {
    }

    /************************************************************************/
    /* start up resource manager                                            */
    /************************************************************************/
    internal bool Startup( string _configFile )
    {
      // init resource locations
      if( !InitResourcePacks( _configFile ) )
        return false;

      // initialize default resource group
      InitGroup( "Default" );

      // preload default resource group
      LoadGroup( "Default" );

      // OK
      return true;
    }

    /************************************************************************/
    /* shut down resource manager                                           */
    /************************************************************************/
    internal void Shutdown()
    {
      if( ResourceGroupManager.Singleton != null )
      {
        // unload bootstrap resource group
        ResourceGroupManager.Singleton.UnloadResourceGroup( "Default" );

        // iterate resource groups
        StringVector groups = ResourceGroupManager.Singleton.GetResourceGroups();
        foreach( string group in groups )
        {
          // unload group if it is still loaded
          if( ResourceGroupManager.Singleton.IsResourceGroupLoaded( group ) )
            ResourceGroupManager.Singleton.UnloadResourceGroup( group );

          // clear the resource group
          ResourceGroupManager.Singleton.ClearResourceGroup( group );
        }

        // shutdown resource manager completely
        ResourceGroupManager.Singleton.ShutdownAll();
      }
    }

    /************************************************************************/
    /* update resource manager                                              */
    /************************************************************************/
    internal void Update()
    {
    }

    //////////////////////////////////////////////////////////////////////////
    // internal functions ////////////////////////////////////////////////////
    //////////////////////////////////////////////////////////////////////////

    /************************************************************************/
    /* init resource groups and locations defined in engine config          */
    /************************************************************************/
    private bool InitResourcePacks( string _filename )
    {
      string resPath;

      // get list of resource packs
      List<ResourceConfig> resPackList = LoadResourceConfig( _filename );

      // iterate list to register packs with engine
      foreach( ResourceConfig res in resPackList )
      {
        // check directory first
        resPath = res.Directory;
        if( Directory.Exists( resPath ) )
        {
          // add directory as resource path
          ResourceGroupManager.Singleton.AddResourceLocation( resPath, "FileSystem", res.Group, true );
          continue;
        }

        // check zip pack file next
        resPath = res.PackFile;
        if( File.Exists( resPath ) )
        {
          // add zip file as resource pack
          ResourceGroupManager.Singleton.AddResourceLocation( resPath, "Zip", res.Group, true );
          continue;
        }

        // failed to register resource location
        //DO NOT FAIL ANYMORE, JUST IGNORE IT... return false;
      }

      // OK
      return true;
    }

    /************************************************************************/
    /* load resource config file                                            */
    /************************************************************************/
    private List<ResourceConfig> LoadResourceConfig( string _filename )
    {
      // create a new list
      List<ResourceConfig> resList = new List<ResourceConfig>();

      // read all lines of resource config file
      string[] lines = File.ReadAllLines( _filename );

      // parse all lines from config file
      foreach( string line in lines )
      {
        // if line starts with '#', ignore it
        if( line.Trim().StartsWith( "#" ) )
          continue;

        // split line into 3 fields separated by '|'
        string[] fields = line.Split( new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries );

        // if there are less than 3 fields, ignore the line
        if( fields.Length < 3 )
          continue;

        // create new resource entry
        ResourceConfig res = new ResourceConfig();

        // set resource group name and remove leading and trailing whitespace characters
        res.Group = fields[ 0 ].Trim();

        // set resource source directory and remove leading and trailing whitespace characters
        res.Directory = fields[ 1 ].Trim();

        // set resource source archive and remove leading and trailing whitespace characters
        res.PackFile = fields[ 2 ].Trim();

        // add resource configuration to list
        resList.Add( res );
      }

      // return the list of resource directories and archives to register
      return resList;
    }

    //////////////////////////////////////////////////////////////////////////
    // public functions //////////////////////////////////////////////////////
    //////////////////////////////////////////////////////////////////////////

    /************************************************************************/
    /* init resource group and parse all ogre scripts inside                */
    /************************************************************************/
    public void InitGroup( string _groupName )
    {
      // init resource group if it is not yet initialized
      if( !ResourceGroupManager.Singleton.IsResourceGroupInitialised( _groupName ) )
        ResourceGroupManager.Singleton.InitialiseResourceGroup( _groupName );
    }

    /************************************************************************/
    /* load all resources for a resource group (preloading)                 */
    /************************************************************************/
    public void LoadGroup( string _groupName )
    {
      // load resource group if it is not yet loaded
      if( !ResourceGroupManager.Singleton.IsResourceGroupLoaded( _groupName ) )
        ResourceGroupManager.Singleton.LoadResourceGroup( _groupName );
    }

    /************************************************************************/
    /* unload all resource for a resource group                             */
    /************************************************************************/
    public void UnloadGroup( string _groupName )
    {
      // do not allow to unload the default resource group in this function
      if( _groupName == "Default" )
        return;

      // unload resource group if it is loaded
      if( ResourceGroupManager.Singleton.IsResourceGroupLoaded( _groupName ) )
        ResourceGroupManager.Singleton.UnloadResourceGroup( _groupName );
    }

    /************************************************************************/
    /* check if a resource exists in a resource group                       */
    /************************************************************************/
    public bool CheckResourceExists( string _groupName, string _fileName )
    {
      // check if file exists in requested resource group
      return ResourceGroupManager.Singleton.ResourceExists( _groupName, _fileName );
    }

    /************************************************************************/
    /* read a resource completely as a byte array                           */
    /************************************************************************/
    public byte[] ReadResource( string _groupName, string _fileName )
    {
      // if the file does not exist there is no way to load it
      if( !CheckResourceExists( _groupName, _fileName ) )
        return null;

      // open resource for reading
      DataStreamPtr streamPtr = ResourceGroupManager.Singleton.OpenResource( _fileName, _groupName );
      uint length = streamPtr.Size();
      uint readLength = 0;

      // create buffer to load resource into
      byte[] buffer = new byte[ (int) length ];
      if( length != 0 )
      {
        unsafe
        {
          fixed( byte* bufferPtr = &buffer[ 0 ] )
          {
            // read resource into buffer
            readLength = streamPtr.Read( bufferPtr, length );
          }
        }
      }

      // if reading the resource failed, just get rid of the buffer
      if( readLength != length )
        buffer = null;

      // close the resource stream
      streamPtr.Close();

      // return buffer of loaded resource or null if failed
      return buffer;
    }

    /************************************************************************/
    /* get a list of all known resource groups                              */
    /************************************************************************/
    public string[] GetResourceGroups()
    {
      // get list of resource groups from resource group manager
      StringVector groups = ResourceGroupManager.Singleton.GetResourceGroups();

      // create array to hold result list
      string[] res = new string[ groups.Count ];

      // copy resource group names into array
      for( int i = 0; i < groups.Count; ++i )
        res[ i ] = groups[ i ];

      // return list of resource groups
      return res;
    }

    /************************************************************************/
    /* get a list of all resources in a group                               */
    /************************************************************************/
    public string[] GetResourcesInGroup( string _group )
    {
      // get list of all resource in the requested resource group
      FileInfoList resources = ResourceGroupManager.Singleton.ListResourceFileInfo( _group );

      // create array to hold the result list
      string[] res = new string[ resources.Count ];

      // iterate list of resource infos
      for( int i = 0; i < resources.Count; ++i )
      {
        // get resource info
        FileInfo_NativePtr resource = resources[ i ];

        // store base file name in result list
        res[ i ] = resource.filename;
      }

      // return list of resources in the requested group
      return res;
    }

  } // class

} // namespace
