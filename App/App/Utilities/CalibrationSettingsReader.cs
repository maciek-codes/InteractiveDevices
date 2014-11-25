using System;
using System.IO;
using System.Text.RegularExpressions;
using Mogre;

namespace Origami.Utilities
{
    /// <summary>
    /// Reads ViewMatrix and ProjectionMatrix from a file
    /// </summary>
    internal class CalibrationSettingsReader
    {
        private readonly Regex numberRegex = new Regex(@"-?\d+(.\d+)?");

        private readonly string fileName;

        /// <summary>
        /// Projection matrix
        /// </summary>
        public Matrix4 ProjectionMatrix { get; private set; }

        /// <summary>
        /// View matrix
        /// </summary>
        public Matrix4 ViewMatrix { get; private set; }

        public CalibrationSettingsReader(string filename)
        {
            this.fileName = filename;
            ViewMatrix = new Matrix4();
            ProjectionMatrix = new Matrix4();
        }

        public void Read()
        {
            using (var file = new StreamReader(fileName))
            {
                var fileContent = file.ReadToEnd();

                this.ReadProjectionMatrix(fileContent);
                this.ReadViewMatrix(fileContent);
            }
        }

        internal void ReadViewMatrix(string fileContent)
        {
            const string sectionHeader = "OpenGL View Matrix";

            ParseSection(fileContent, sectionHeader, ViewMatrix);
        }

        internal void ReadProjectionMatrix(string fileContent)
        {
            const string sectionHeader = "OpenGL Projection Matrix";

            ParseSection(fileContent, sectionHeader, ProjectionMatrix);
        }

        private void ParseSection(string fileContent, string sectionHeader, Matrix4 matrix)
        {
            var startIndex = fileContent.IndexOf(sectionHeader, 
                StringComparison.InvariantCultureIgnoreCase) + sectionHeader.Length;
            var endIndex = fileContent.IndexOf(']', startIndex) + 1;

            var end = endIndex - startIndex + 1;
            if (startIndex + end >= fileContent.Length)
            {
                end = fileContent.Length - startIndex - 1;
            }
            var substring = fileContent.Substring(startIndex, end).Trim();

            ReadValuesIntoMatrix(substring, matrix);
        }

        private void ReadValuesIntoMatrix(string substring, Matrix4 matrix)
        {
            const int size = 4;

            var matches = this.numberRegex.Matches(substring);
            for (var i = 0; i < size; i++)
            {
                for (var j = 0; j < size; j++)
                {
                    var match = matches[i * size + j];
                    var value = Convert.ToSingle(match.Value);
                    matrix[i, j] = value;
                }
            }
        }
    }
}
