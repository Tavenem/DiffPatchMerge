using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tavenem.DiffPatchMerge.Test
{
    [TestClass]
    public class DiffUnitTests
    {
        [TestMethod]
        public void LongTest()
        {
            var longText1 = System.IO.File.ReadAllText("LongText1.txt");
            var longText2 = System.IO.File.ReadAllText("LongText2.txt");

            var diffs = Diff.GetDiff(longText1, longText2);

            var result = Diff.GetNewVersion(diffs);
            Assert.AreEqual(longText2, result, false);

            result = Diff.GetPreviousVersion(diffs);
            Assert.AreEqual(longText1, result, false);
        }
    }
}
