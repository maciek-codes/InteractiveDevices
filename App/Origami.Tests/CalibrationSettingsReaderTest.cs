using Microsoft.VisualStudio.TestTools.UnitTesting;
using Mogre;
using Origami.Utilities;

namespace Origami.Tests
{

    [TestClass]
    public class CalibrationSettingsReaderTest
    {
        [TestMethod]
        public void TestReadProjectionMatrix()
        {
            var calibrationReader = new CalibrationSettingsReader(string.Empty);
            calibrationReader.ReadProjectionMatrix(file);

            var projectionMatrix = calibrationReader.ProjectionMatrix;
            
            Assert.IsTrue(projectionMatrix.Equals(matProj));
        }

        [TestMethod]
        public void TestReadViewMatrix()
        {
            var calibrationReader = new CalibrationSettingsReader(string.Empty);
            calibrationReader.ReadViewMatrix(file);

            var viewMatrix = calibrationReader.ViewMatrix;

            Assert.IsTrue(viewMatrix.Equals(matView));
        }


        #region Sample file
        private string file = @"Camera Matrix
        [2093.381849700214, 0, 593.8663238427155;
          0, 2280.39275395402, 948.4138927400367;
          0, 0, 1]

        Projector translation
        [0.05505723727117357;
          0.07662712240224223;
          0.01638650334711456]

        Projector rotation
        [0.01312120656498382;
          -3.172488636893087;
          -0.06779782839148396]

        OpenGL Projection Matrix
        [4.08863642519573, 0, -0.1598951637553037, 0;
          0, 5.938522796755259, 1.472432012343846, 0;
          0, 0, -1.02020202020202, -0.202020202020202;
          0, 0, -1, 0]

        OpenGL View Matrix
        [0.9994650735605802, 0.008941933163257507, -0.0314580445013319, -0.05505723727117357;
          -0.007589831646579278, 0.999053073146942, 0.04284100245357377, 0.07662712240224223;
          -0.03181133741483571, 0.04257932440697439, -0.9985865210110364, 0.01638650334711456;
          0, 0, 0, 1]";


        // Propjection matrix
        private readonly Matrix4 matProj = new Matrix4(
            4.08863642519573f, 0, -0.1598951637553037f, 0,
          0, 5.938522796755259f, 1.472432012343846f, 0,
          0, 0, -1.02020202020202f, -0.202020202020202f,
          0, 0, -1, 0);

        // View Matrix
        private readonly Matrix4 matView = new Matrix4(
            0.9994650735605802f, 0.008941933163257507f, -0.0314580445013319f, -0.05505723727117357f,
          -0.007589831646579278f, 0.999053073146942f, 0.04284100245357377f, 0.07662712240224223f,
          -0.03181133741483571f, 0.04257932440697439f, -0.9985865210110364f, 0.01638650334711456f,
          0, 0, 0, 1);

        #endregion
    }
}
