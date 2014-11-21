using System;

namespace Quickstart2010.Modules
{
  /************************************************************************/
  /* resource group configuration                                         */
  /************************************************************************/
  [Serializable]
  public class ResourceConfig
  {
    //////////////////////////////////////////////////////////////////////////
    public string Group;
    public string Directory;
    public string PackFile;

    /************************************************************************/
    /* constructor                                                          */
    /************************************************************************/
    internal ResourceConfig()
    {
      Group = string.Empty;
      Directory = string.Empty;
      PackFile = string.Empty;
    }

  } // class

} // namespace
