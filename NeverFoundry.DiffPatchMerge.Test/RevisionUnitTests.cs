using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;

namespace NeverFoundry.DiffPatchMerge.Test
{
    [TestClass]
    public class RevisionUnitTests
    {
        [TestMethod]
        public void RoundTripTest()
        {
            const string Text1 = "This is the original text.";
            const string Text2 = "This is a revised text with multiple differences.";

            var revision = Revision.GetRevison(Text1, Text2);
            Console.WriteLine(revision);

            var result = revision.Apply(Text1);

            Assert.AreEqual(Text2, result, false);
        }

        [TestMethod]
        public void SeriesTest()
        {
            var longText1 = System.IO.File.ReadAllText("LongText1.txt");
            var longText2 = System.IO.File.ReadAllText("LongText2.txt");
            var longText3 = System.IO.File.ReadAllText("LongText3.txt");

            var revision1 = Revision.GetRevison(longText1, longText2);
            var revision2 = Revision.GetRevison(longText2, longText3);

            var result = new[] { revision1, revision2 }.Apply(longText1);

            Assert.AreEqual(longText3, result, false);
        }

        [TestMethod]
        public void ToStringTest()
        {
            Console.WriteLine("Short text:");
            const string ShortText1 = "This is the original text.";
            const string ShortText2 = "This is a revised text with multiple differences.";

            var revision = Revision.GetRevison(ShortText1, ShortText2);
            var revisionString = revision.ToString();
            Console.WriteLine(revisionString);
            Console.Write("Final text length: ");
            Console.WriteLine(ShortText2.Length);
            Console.Write("Revision length: ");
            Console.WriteLine(revisionString.Length);
            Console.Write("Compression factor: ");
            Console.WriteLine((revisionString.Length / (double)ShortText2.Length).ToString("G2"));

            Console.WriteLine("Long text:");
            var longText1 = System.IO.File.ReadAllText("LongText1.txt");
            var longText2 = System.IO.File.ReadAllText("LongText2.txt");

            var sw = Stopwatch.StartNew();
            revision = Revision.GetRevison(longText1, longText2);
            sw.Stop();
            Console.Write("Time to calculate revision: ");
            Console.Write(sw.ElapsedMilliseconds);
            Console.WriteLine(" ms");

            sw.Restart();
            revisionString = revision.ToString();
            sw.Stop();
            Console.WriteLine(revisionString);
            Console.Write("Time to create revision string: ");
            Console.Write(sw.ElapsedMilliseconds);
            Console.WriteLine(" ms");

            Console.Write("Final text length: ");
            Console.WriteLine(longText2.Length);
            Console.Write("Revision length: ");
            Console.WriteLine(revisionString.Length);
            Console.Write("Compression factor: ");
            Console.WriteLine((revisionString.Length / (double)longText2.Length).ToString("G2"));

            sw.Restart();
            var result = revision.Apply(longText1);
            sw.Stop();
            Console.Write("Time to apply revision: ");
            Console.Write(sw.ElapsedMilliseconds);
            Console.WriteLine(" ms");

            Assert.AreEqual(longText2, result, false);
        }
    }
}
