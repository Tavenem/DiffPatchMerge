using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Tavenem.DiffPatchMerge.Test
{
    [TestClass]
    public class HelperUnitTests
    {
        [TestMethod]
        public void CompressionTest()
        {
            Console.WriteLine("Short text:");
            const string UncompressedShortText = "This is the original text.";

            var compressedText = UncompressedShortText.Compress();
            Console.WriteLine(compressedText);
            Console.Write("Uncompressed length: ");
            Console.WriteLine(UncompressedShortText.Length);
            Console.Write("Compressed length: ");
            Console.WriteLine(compressedText.Length);
            Console.Write("Compression factor: ");
            Console.WriteLine((compressedText.Length / (double)UncompressedShortText.Length).ToString("G2"));

            var result = compressedText.Decompress();

            Assert.AreEqual(UncompressedShortText, result, false);

            Console.WriteLine("Long text:");
            const string UncompressedLongText = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

            compressedText = UncompressedLongText.Compress();
            Console.WriteLine(compressedText);
            Console.Write("Uncompressed length: ");
            Console.WriteLine(UncompressedLongText.Length);
            Console.Write("Compressed length: ");
            Console.WriteLine(compressedText.Length);
            Console.Write("Compression factor: ");
            Console.WriteLine((compressedText.Length / (double)UncompressedLongText.Length).ToString("G2"));

            result = compressedText.Decompress();

            Assert.AreEqual(UncompressedLongText, result, false);
        }
    }
}
