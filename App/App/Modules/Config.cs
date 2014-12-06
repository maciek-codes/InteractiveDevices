using System.ComponentModel;
using Mogre;

namespace Origami.Modules
{
    using System.IO;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Global settings read form a 
    /// </summary>
    public class Config
    {
        private const string FullScreenSettingKey = "isFullScreen";
        private const string ScreenWidthKey = "screenWidth";
        private const string ScreenHeighKey = "screenHeight";
        private const string CameraAngleKey = "cameraAngle";
        private const string CameraHeightKey = "cameraHeight";
        private const string CameraDistanceKey = "cameraDistance";
        private const string PaddingKey = "roiRect";

        /// <summary>
        /// Configuration instance
        /// </summary>
        private static Config instance;

        private JObject configJson;

        /// <summary>
        /// Hide constructor
        /// </summary>
        private Config()
        {
        }

        /// <summary>
        /// Read from a file
        /// </summary>
        /// <param name="fileName"></param>
        public static void ReadFromFile(string fileName)
        {
            if(!File.Exists(fileName)) 
                return;

            using (var reader = new StreamReader(fileName))
            {
                var configFileString = reader.ReadToEnd();
                Instance.configJson = JObject.Parse(configFileString);
            }
        }

        public static Config Instance
        {
            get { return instance ?? (instance = new Config()); }
        }

        /// <summary>
        /// Gets Full screen setting
        /// </summary>
        public bool IsFullScreen
        {
            get { return TryGetOrDefault(FullScreenSettingKey, false); }
        }

        /// <summary>
        /// Gets screen width setting
        /// </summary>
        public int ScreenWidth
        {
            get { return TryGetOrDefault(ScreenWidthKey, 1024); }
        }

        /// <summary>
        /// Gets screen height setting
        /// </summary>
        public int ScreenHeight
        {
            get { return TryGetOrDefault(ScreenHeighKey, 768); }
        }

        public float CameraAngle { get { return TryGetOrDefault(CameraAngleKey, 70.0f); } }

        public float CameraHeight { get { return TryGetOrDefault(CameraHeightKey, .80f); } }

        public float CameraDistance { get { return TryGetOrDefault(CameraDistanceKey, -.10f); } }
        
        public Rect Padding { get { return TryGetOrDefault(PaddingKey, new Rect(0, 0, 100, 100)); }}

        private T TryGetOrDefault<T>(string keyName, T defaultValue)
        {
            JToken jValue;

            return this.configJson.TryGetValue(keyName, out jValue) ? 
                jValue.ToObject<T>() : 
                defaultValue;
        }

        public bool IsLoaded { get { return this.configJson != null; } }
    }
}
