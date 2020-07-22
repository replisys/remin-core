using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace borderline
{
    class Program
    {
        static void Main(string[] args)
        {
            SxSExtract.SystemRoot = args[1];
            SxSExtract.TempFolder = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "WCPEx");
            SxSExtract.CopyList = new List<KeyValuePair<string, string>>();
            SxSExtract.SxSExpandAvailable = true;
            SxSExtract.MakingCAB = true;
            LibSxS.Delta.DeltaAPI.wcpBasePath = System.IO.Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "manifest.bin");

            SxSExtract.RecurseManifestHierarchy($@"{args[0]}", null, null, null, null, null);
            SxSExtract.CopyPackage($@"{args[2]}");
        }


    }
}
