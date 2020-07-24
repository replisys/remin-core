using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace borderline
{
    public static class SxSExtract
    {
        internal const string Banner1 = "Aunty Mel's Cheap And Nasty SxS Package Extractor, 2013/11/09";
        internal const string Banner2 = "Copyright (C) 2012-2013 Melinda Bellemore. All rights reserved.";

        internal const string Line79 = "-------------------------------------------------------------------------------";
        internal const string Space79 = "                                                                               ";

        internal const string IdentityPath = "/assemblyIdentity";
        internal const string DependencyPath = "/dependency/dependentAssembly/assemblyIdentity";
        internal const string PackagePath = "/package/update/package/assemblyIdentity";
        internal const string ComponentPath = "/package/update/component/assemblyIdentity";
        internal const string DriverPath = "/package/update/driver/assemblyIdentity";

        internal const int ForReading = 1;
        internal const int ForWriting = 2;
        internal const int ForAppending = 8;

        internal const int FileFlagNone = 0;
        internal const int FileFlagError = 1;
        internal const int FileFlagCompressed = 2;

        internal const int MAX_PATH = 260;

        internal static object Shell;
        internal static object FSO;

        internal static string TempFolder;
        internal static bool DebugMode;
        internal static string SystemRoot;
        internal static bool IncludeRes;
        internal static bool ViciousHacks;

        internal static bool SxSExpandAvailable;
        internal static bool CABArcAvailable;

        internal static bool MakingCAB;
        internal static string InputPath;
        internal static string OutputPath;

        internal static bool SwitchLoop;
        internal static int ParamIndex;

        internal static HashSet<string> FilesTried = new HashSet<string>();

        internal static List<KeyValuePair<string, string>> CopyList;

        // Simple logging functions.
        internal static void LogBare(string AText)
        {
            Console.WriteLine(AText);
        }

        internal static void LogInfo(string AText)
        {
            Console.WriteLine("[Information] " + AText);
        }

        internal static void LogError(string AText)
        {
            Console.WriteLine("[   Error   ] " + AText);
        }

        internal static void LogFatal(string AText)
        {
            Console.WriteLine("[Fatal error] " + AText);
        }

        internal static void LogDebug(string AText)
        {
            if (DebugMode)
                Console.WriteLine("[   Debug   ] " + AText);
        }

        // Adds a file or folder to the copy list, with error-checking to work around
        // the fact that VBScript doesn't like having the same "key" added multiple
        // times.
        internal static void CopyListAdd(string ASource, string ATarget)
        {
            if (CopyList == null)
                return;

            /*if (CopyList.ContainsKey(ASource))
                LogDebug("CopyList key already exists: " + ASource);
            else
                CopyList[ASource] = ATarget;*/
            CopyList.Add(new KeyValuePair<string, string>(ASource, ATarget));
        }

        // Checks whether the file is compressed with Microsoft's new SxS compression
        // scheme.
        internal static bool IsCompressed(string AFileName)
        {
            // hacky workaround to not uncompress stuff when copying
            if (AFileName.ToLowerInvariant().Contains("winsxs"))
            {
                return false;
            }

            string sigAscii;
            using (FileStream fs = new FileStream(AFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] sigRaw = new byte[4];
                fs.Read(sigRaw, 0, 4);
                sigAscii = Encoding.ASCII.GetString(sigRaw);
            }

            // I've seen these signatures. I wonder if there's more?
            switch (sigAscii)
            {
                case "DCM\u0001":
                case "DCN\u0001":
                case "DCS\u0001":
                case "PA30":
                    return true;
                default:
                    return false;
            }
        }

        // Calls my tool to expand a file compressed by Microsoft's new SxS compression
        // scheme.
        internal static string SxSExpand(string AFileName)
        {
            string TempFileName;

            // Die if SxS Expand isn't available.
            if (!SxSExpandAvailable)
                return null;

            // Make a temporary file to expand to.
            TempFileName = Path.Combine(TempFolder, Path.GetTempFileName());

            using (Process p = new Process())
            {
                p.StartInfo.FileName = "SXSEXP64.EXE";
                if (DebugMode)
                    p.StartInfo.Arguments = $"\"{AFileName}\" \"{TempFileName}\"";
                else
                {
                    p.StartInfo.Arguments = $"\"{AFileName}\" \"{TempFileName}\"";
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                }
                LogDebug("Executing: " + p.StartInfo.FileName + " " + p.StartInfo.Arguments);
                p.Start();
                p.WaitForExit();
                if (p.ExitCode == 0)
                    return TempFileName;
                else
                {
                    LogError("SxSExpand call failed - error code: " + p.ExitCode.ToString());
                    return null;
                }
            }
        }

        // Check if the external tools are available.
        internal static void CheckExternals()
        {
            LogDebug("Checking for external tools - CABARC.EXE.");

            List<string> pathPaths = null;
            //if (File.Exists("SXSEXPAND.EXE"))
            //    SxSExpandAvailable = true;
            //else
            //{
            //    if (pathPaths == null)
            //    {
            //        foreach (string pp in Environment.GetEnvironmentVariable("PATH").Split(';'))
            //        {
            //            if (pp.Length > 0)
            //                pathPaths.Add(pp);
            //        }
            //    }
            //    foreach (string pp in pathPaths)
            //    {
            //        if (File.Exists(Path.Combine(pp, "SXSEXPAND.EXE")))
            //        {
            //            SxSExpandAvailable = true;
            //            break;
            //        }
            //    }
            //}
            if (File.Exists("CABARC.EXE"))
                CABArcAvailable = true;
            else
            {
                if (pathPaths == null)
                {
                    foreach (string pp in Environment.GetEnvironmentVariable("PATH").Split(';'))
                    {
                        if (pp.Length > 0)
                            pathPaths.Add(pp);
                    }
                }
                foreach (string pp in pathPaths)
                {
                    if (File.Exists(Path.Combine(pp, "CABARC.EXE")))
                    {
                        CABArcAvailable = true;
                        break;
                    }
                }
            }
        }

        // Use SUBST.EXE for path shortening. VBScript has a tight limit (260
        // characters) on path lengths.
        internal static string Subst(string APath)
        {
            char FoundDrive = 'A';

            // Find an unused drive letter.
            for (var Drive = 'A'; Drive <= 'Z'; Drive++)
            {
                try
                {
                    new DriveInfo(Drive.ToString());
                    FoundDrive = Drive;
                    break;
                }
                catch { }
            }

            UnSubst(FoundDrive + ":");

            using (Process p = new Process())
            {
                p.StartInfo.FileName = "SUBST.EXE";
                p.StartInfo.Arguments = FoundDrive + ": \"" + APath + "\"";
                if (!DebugMode)
                {
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                }
                LogDebug("Executing: " + p.StartInfo.FileName + " " + p.StartInfo.Arguments);
                p.Start();
                p.WaitForExit();
                if (p.ExitCode == 0)
                {
                    string returnStr = FoundDrive + ":\\";
                    LogInfo("SUBST called: " + APath + " associated with " + returnStr + ".");
                    return returnStr;
                }
                else
                {
                    LogError("SUBST failed - will perform file copying using full paths. Error code: " + p.ExitCode.ToString());
                    return APath;
                }
            }
        }

        internal static void UnSubst(string APath)
        {
            using (Process p = new Process())
            {
                p.StartInfo.FileName = "SUBST.EXE";
                p.StartInfo.Arguments = APath.Substring(0, 2) + " /D";
                if (!DebugMode)
                {
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                }
                LogDebug("Executing: " + p.StartInfo.FileName + " " + p.StartInfo.Arguments);
                p.Start();
                p.WaitForExit();
                if (p.ExitCode == 0)
                    LogInfo("SUBST /D called: " + APath + " deassociated.");
                else
                    LogInfo("SUBST /D failed - error code: " + p.ExitCode.ToString());
            }
        }

        internal static string CreatePackageID(string APackageName, string APublicKeyToken, string AArch, string ALang, string AVersion)
        {
            string returnStr = APackageName + "~" + APublicKeyToken + "~" + AArch + "~";
            switch (ALang.ToLower())
            {
                case "neutral":
                case "none":
                    break;
                default:
                    returnStr += ALang;
                    break;
            }
            return returnStr + "~" + AVersion;
        }

        public static string CreateAssemblyID(string APackageName, string APublicKeyToken, string AArch, string ALang, string AVersion, string AVersionScope)
        {
            string cultureVal;
            string ogPackageName = APackageName;
            APackageName = APackageName.Replace(" ", "").Replace("(", "").Replace(")", "").ToLower();
            string returnStr = AArch + "_";
            if (APackageName.Length > 40)
                returnStr += APackageName.Substring(0, 19) + ".." + APackageName.Substring(APackageName.Length - 19, 19);
            else
                returnStr += APackageName;
            returnStr += "_" + APublicKeyToken + "_" + AVersion + "_";
            if (string.IsNullOrEmpty(ALang) || ALang.ToLower() == "neutral")
                cultureVal = "none";
            else
                cultureVal = ALang;
            return returnStr + cultureVal + "_" + Helpers.sxsHash(
                new List<string>() { "name", "culture", "typename", "type", "version", "publickeytoken", "processorarchitecture", "versionscope" },
                // nasty hack! should read this from a reference
                new List<string>() { ogPackageName, cultureVal, "none", APackageName.StartsWith("dual_") ? "dualmodedriver" : "none", AVersion, APublicKeyToken, AArch, AVersionScope }).ToString("x16");
        }

        internal static List<string> FindPossibleManifestFiles(string APackageName, string APublicKeyToken, string AArch, string ALang, string AVersion, string AVersionScope)
        {
            string FPackageName = APackageName.Replace(" ", "");
            LogInfo("Finding matching manifest for assembly reference: " + FPackageName + "," + APublicKeyToken + "," + AArch + "," + ALang + "," + AVersion + "," + AVersionScope);
            string WorkPath = Path.Combine(SystemRoot, "WinSxS\\Manifests");
            string pathToTry = Path.Combine(WorkPath, CreateAssemblyID(APackageName, APublicKeyToken, AArch, ALang, AVersion, AVersionScope) + ".manifest");
            LogDebug("Trying file name: " + pathToTry);
            List<string> returnList = new List<string>();
            if (File.Exists(pathToTry))
                returnList.Add(pathToTry);
            WorkPath = Path.Combine(SystemRoot, "Servicing\\Packages");
            pathToTry = Path.Combine(WorkPath, CreatePackageID(FPackageName, APublicKeyToken, AArch, ALang, AVersion) + ".mum");
            LogDebug("Trying file name: " + pathToTry);
            if (File.Exists(pathToTry))
                returnList.Add(pathToTry);
            return returnList;
        }

        internal static void FindReferencedAssemblies(XElement AXML, string APath)
        {
            string ReferencedAssemblyName, ReferencedPublicKeyToken, ReferencedArch, ReferencedLang, ReferencedVersion, ReferencedVersionScope;

            LogDebug("Checking XML path for assembly references: " + APath);

            foreach (var CurrentElement in AXML.XPathSelectElements(APath))
            {
                ReferencedAssemblyName = CurrentElement.Attribute("name").Value;
                ReferencedPublicKeyToken = CurrentElement.Attribute("publicKeyToken").Value;
                ReferencedArch = CurrentElement.Attribute("processorArchitecture").Value;
                ReferencedLang = CurrentElement.Attribute("language").Value;
                ReferencedVersion = CurrentElement.Attribute("version").Value;
                ReferencedVersionScope = "none";
                if (CurrentElement.Attributes("versionScope").Any())
                    ReferencedVersionScope = CurrentElement.Attribute("versionScope").Value;

                if (ReferencedLang == "*" && !IncludeRes)
                {
                    // Handle /INCLUDERES switch by exiting here.
                    LogError("Appears to be a MUI reference - skipping: " + ReferencedAssemblyName);
                    return;
                }
                else
                {
                    RecurseManifestHierarchy(ReferencedAssemblyName, ReferencedPublicKeyToken, ReferencedArch, ReferencedLang, ReferencedVersion, ReferencedVersionScope);
                }
            }
        }

        internal static void ExtractAssemblyFolder(XElement XML, string AAssemblyDir)
        {
            var files = XML.Descendants("package").Descendants("file");

            foreach (var file in files)
            {
                // #TODOWCOS: parse partition
                var sourcePath = Path.Combine(Path.GetPathRoot(SystemRoot), file.Attribute("name").Value.Substring(1));
                var destPath = file.Attribute("cabpath").Value;

                CopyListAdd(sourcePath, destPath);
            }

            CopyListAdd(AAssemblyDir, Path.GetFileName(AAssemblyDir));
        }

        internal static bool RecurseManifestHierarchy(string APackageName, string APublicKeyToken, string AArch, string ALang, string AVersion, string AVersionScope)
        {

            XElement XML = null;

            bool FirstFile;
            List<string> FileList = new List<string>();

            string AssemblyName = null;
            string AssemblyPublicKeyToken = null;
            string AssemblyArch = null;
            string AssemblyLang = null;
            string AssemblyVersion = null;
            string AssemblyVersionScope = null;

            string SourceFile;
            string TargetFile;

            int FileFlag = 0;
            int ArraySize;

            string SxSPath = Path.Combine(SystemRoot, "WinSxS");

            // On first call, most of this will be empty. Just use the package name
            // argument as the file name.
            if (string.IsNullOrEmpty(APublicKeyToken) && string.IsNullOrEmpty(AArch) && string.IsNullOrEmpty(ALang) && string.IsNullOrEmpty(AVersion))
            {
                FirstFile = true;
                if (File.Exists(APackageName))
                    FileList.Add(APackageName);
            }
            else
            {
                FileList = FindPossibleManifestFiles(APackageName, APublicKeyToken, AArch, ALang, AVersion, AVersionScope);
                FirstFile = false;
            }

            if (FileList.Count == 0)
            {
                LogError("Couldn't find a matching manifest.");
                Environment.Exit(69);
                return false;
            }

            foreach (var CurrentManifest in FileList)
            {
                if (FilesTried.Contains(CurrentManifest))
                {
                    continue;
                }

                FilesTried.Add(CurrentManifest);

                string manifestPath = CurrentManifest;
                //if (IsCompressed(CurrentManifest))
                //{
                //    if (!SxSExpandAvailable)
                //    {
                //        LogError("SxS File Expander not available - can't access compressed files.");
                //        FileFlag |= FileFlagError;
                //    }
                //    else
                //    {
                //        manifestPath = SxSExpand(CurrentManifest);
                //        if (string.IsNullOrEmpty(manifestPath))
                //        {
                //            LogError("Couldn't decompress manifest.");
                //            FileFlag |= FileFlagError;
                //        }
                //        else
                //        {
                //            LogDebug("Decompressed manifest to temporary file: " + CurrentManifest);
                //            FileFlag |= FileFlagCompressed;
                //        }
                //    }
                //}

                if ((FileFlag & FileFlagError) != FileFlagError)
                {
                    // Load the manifest, and leave if anything goes wrong.
                    LogInfo("Loading manifest: " + CurrentManifest);
                    try { XML = LibSxS.Delta.DeltaAPI.GetManifest(manifestPath); } catch (Exception ex) { LogError("Couldn't load manifest. Exception = " + ex.ToString()); FileFlag |= FileFlagError; }
                }

                if ((FileFlag & FileFlagError) != FileFlagError)
                {
                    // Get all the information required...
                    var CurrentElement = XML.XPathSelectElement(IdentityPath);
                    AssemblyName = CurrentElement.Attribute("name").Value;
                    AssemblyPublicKeyToken = CurrentElement.Attribute("publicKeyToken").Value;
                    AssemblyArch = CurrentElement.Attribute("processorArchitecture").Value;
                    AssemblyLang = CurrentElement.Attribute("language").Value;
                    AssemblyVersion = CurrentElement.Attribute("version").Value;

                    AssemblyVersionScope = "none";
                    if (CurrentElement.Attributes("versionScope").Any())
                        AssemblyVersionScope = CurrentElement.Attribute("versionScope").Value;

                    // And sanity check
                    if (!FirstFile)
                    {
                        if (APackageName.ToLower() == AssemblyName.ToLower() &&
                            APublicKeyToken == AssemblyPublicKeyToken &&
                            AArch == AssemblyArch &&
                            AVersion == AssemblyVersion
                            //&& AVersionScope.ToLower() == AssemblyVersionScope.ToLower()
                            )
                        {
                            // Handle wildcard language references. Allows extraction
                            // of Snipping Tool.
                            if (ALang != "*")
                            {
                                if (ALang != AssemblyLang)
                                {
                                    ;
                                    //FileFlag |= FileFlagError;
                                }
                            }
                        }
                        else
                        {
                            FileFlag |= FileFlagError;
                        }

                        if ((FileFlag & FileFlagError) != FileFlagError)
                        {
                            LogDebug("Manifest matches parent assembly reference.");
                        }
                        else
                        {
                            LogError("Appear to have loaded the wrong manifest - skipping.");
                            Environment.Exit(69);
                        }
                    }
                }

                if ((FileFlag & FileFlagError) != FileFlagError)
                {
                    // Give some text to the user for head-scratching. :)
                    LogBare("");
                    LogBare(AssemblyName);
                    LogBare(Line79);
                    LogBare(("Version:          " + AssemblyVersion + Space79).Substring(0, 39) + " Architecture:     " + AssemblyArch);
                    LogBare(("Language:         " + AssemblyLang + Space79).Substring(0, 39) + " Public key token: " + AssemblyPublicKeyToken);
                    LogBare("");

                    // Fill associative array with file (and folders) to copy
                    if (Path.GetExtension(CurrentManifest).ToLower() == ".mum")
                    {
                        // If it's a .mum file, also copy associated catalogue file.
                        SourceFile = CurrentManifest.Substring(0, CurrentManifest.Length - 3) + "cat";
                        if (FirstFile)
                            TargetFile = "update.cat";
                        else
                            TargetFile = Path.GetFileName(SourceFile);
                        if (File.Exists(SourceFile))
                            CopyListAdd(SourceFile, TargetFile);
                    }

                    SourceFile = CurrentManifest;
                    if (FirstFile)
                        TargetFile = "update.mum";
                    else
                        TargetFile = Path.GetFileName(SourceFile);

                    if (File.Exists(SourceFile))
                        CopyListAdd(SourceFile, TargetFile);

                    // Copy any SxS assembly folders (of course). If it's a .mum file, convert
                    // the file name to assembly ID format. Manifests are already in assembly
                    // ID format, but I prefix match them anyway.
                    LogInfo("Finding possible associated assembly folders.");
                    ArraySize = CopyList.Count;
                    ExtractAssemblyFolder(XML, Path.Combine(SxSPath, CreateAssemblyID(AssemblyName, AssemblyPublicKeyToken, AssemblyArch, AssemblyLang, AssemblyVersion, AssemblyVersionScope)));

                    if (ViciousHacks)
                    {
                        // VICIOUS HACK to allow extraction of TFTP Client.
                        if (!AssemblyName.EndsWith("-Package"))
                        {
                            LogInfo("Vicious hack: add '-Package' to assembly name.");
                            ExtractAssemblyFolder(XML, Path.Combine(SxSPath, CreateAssemblyID(AssemblyName + "-Package", AssemblyPublicKeyToken, AssemblyArch, AssemblyLang, AssemblyVersion, AssemblyVersionScope)));
                        }

                        // REALLY VICIOUS HACK to allow extraction of Adobe Flash for
                        // Windows 8.x.
                        if (AssemblyName.EndsWith("-Package"))
                        {
                            LogInfo("Vicious hack: remove '-Package' from assembly name.");
                            ExtractAssemblyFolder(XML, Path.Combine(SxSPath, CreateAssemblyID(AssemblyName.Substring(0, AssemblyName.Length - 8), AssemblyPublicKeyToken, AssemblyArch, AssemblyLang, AssemblyVersion, AssemblyVersionScope)));
                        }

                        // ULTRA VICIOUS HACK to allow extraction of WinPE-SRT 8.1.
                        if (!AssemblyName.EndsWith(".Deployment"))
                        {
                            LogInfo("Vicious hack: add '.Deployment' to assembly name.");
                            ExtractAssemblyFolder(XML, Path.Combine(SxSPath, CreateAssemblyID(AssemblyName + ".Deployment", AssemblyPublicKeyToken, AssemblyArch, AssemblyLang, AssemblyVersion, AssemblyVersionScope)));
                        }
                    }


                    // Provide message if nothing was copied.
                    if (ArraySize == CopyList.Count)
                        LogInfo("Couldn't find any associated assembly folders.");

                    FindReferencedAssemblies(XML, DependencyPath);
                    FindReferencedAssemblies(XML, PackagePath);
                    FindReferencedAssemblies(XML, ComponentPath);
                    FindReferencedAssemblies(XML, DriverPath);

                    if (XML.Descendants("package").Attributes("preProcessed").Any(a => a.Value == "True"))
                    {
                        IsWIM = true;
                        WIMDir = Path.Combine(SystemRoot, XML.Descendants("package").Attributes("applyTo").FirstOrDefault().Value.Substring(1));
                    }
                }

                if ((FileFlag & FileFlagCompressed) == FileFlagCompressed)
                {
                    LogDebug("Deleting temporary manifest file: " + manifestPath);
                    File.Delete(manifestPath);
                }
            }
            return true;
        }

        internal static string CopyObject(string ASource, string ATarget)
        {

            // Make sure the paths aren't too long.
            if (ASource.Length >= 260)
                ASource = "\\\\?\\" + ASource;
            if (ATarget.Length >= 260 || Path.GetDirectoryName(ATarget).Length >= 248)
                ATarget = "\\\\?\\" + ATarget;

            if (Directory.Exists(ASource))
            {
                Directory.CreateDirectory(ATarget);
                LogDebug("Created folder: " + ATarget);
                foreach (var SourcePath in Directory.GetFileSystemEntries(ASource))
                {
                    string TargetPath = Path.Combine(ATarget, Path.GetFileName(SourcePath));
                    string nestedRet = CopyObject(SourcePath, TargetPath);
                    if (!string.IsNullOrEmpty(nestedRet))
                        return nestedRet;
                }
                return null;
            }
            else if (File.Exists(ASource))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ATarget));

                string UncompSource;
                bool FileCompressed = IsCompressed(ASource);
                if (FileCompressed)
                {
                    LogInfo("Decompressing and copying file: " + ASource + " --> " + ATarget);

                    // Decompress source file.
                    /*UncompSource = SxSExpand(ASource);

                    if (string.IsNullOrEmpty(UncompSource))
                    {
                        FileCompressed = false;
                        UncompSource = ASource;
                        LogDebug("Couldn't decompress file - copying it in compressed form.");
                    }*/

                    try
                    {
                        File.WriteAllBytes(ATarget, LibSxS.Delta.DeltaAPI.LoadManifest(ASource));
                        return null;
                    }
                    // might be unable to decompress (as it should remain compressed, like, say, the target WinSxS)
                    catch (Win32Exception)
                    {
                        FileCompressed = false;
                        UncompSource = ASource;
                    }

                }
                else
                {
                    LogInfo("Copying file: " + ASource + " --> " + ATarget);
                    UncompSource = ASource;
                }

                if (File.Exists(ATarget))
                {
                    var attrs = File.GetAttributes(ATarget) & ~FileAttributes.Hidden & ~FileAttributes.System;

                    File.SetAttributes(ATarget, attrs);
                }

                try { File.Copy(UncompSource, ATarget, true); } catch (Exception ex) {
                    return ex.ToString();
                }

                                if (File.Exists(ATarget))
                {
                    var attrs = File.GetAttributes(ATarget) & ~FileAttributes.Hidden & ~FileAttributes.System;

                    File.SetAttributes(ATarget, attrs);
                }

                // Remove possible temporary uncompressed file.
                if (FileCompressed)
                {
                    LogDebug("Deleting temporary decompressed file: " + UncompSource);
                    File.Delete(UncompSource);
                }
                return null;
            }
            else
            {
                // Non-fatal
                LogDebug("CopyObject called on non-existent/invalid file/folder: " + ASource);
                return null;
            }
        }

        internal static bool IsWIM = false;
        internal static string WIMDir;

        public static void InitNativeLibrary()
        {
            string arch = null;
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X86:
                    arch = "x86";
                    break;
                case Architecture.X64:
                    arch = "x64";
                    break;
                case Architecture.Arm:
                    arch = "armhf";
                    break;
                case Architecture.Arm64:
                    arch = "arm64";
                    break;
            }

            string libPath = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                libPath = Path.Combine(arch, "libwim-15.dll");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                libPath = Path.Combine(arch, "libwim.so");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                libPath = Path.Combine(arch, "libwim.dylib");

            if (libPath != null)
            {
                libPath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), libPath);
            }

            if (libPath == null || !File.Exists(libPath))
                throw new PlatformNotSupportedException();

            ManagedWimLib.Wim.GlobalInit(libPath);
        }

        internal static void MakeWIM(string OutputPath, string AOutputPath)
        {
            InitNativeLibrary();

            using (var wim = ManagedWimLib.Wim.CreateNewWim(ManagedWimLib.CompressionType.LZX))
            {
                ManagedWimLib.CallbackStatus ProgressCallback(ManagedWimLib.ProgressMsg msg, object info, object progctx)
                {
                    return ManagedWimLib.CallbackStatus.Continue;
                }

                wim.RegisterCallback(ProgressCallback);

                wim.AddImage(OutputPath, "Edition Package", null, ManagedWimLib.AddFlags.None);
                wim.AddImage(WIMDir, "", null, ManagedWimLib.AddFlags.None);

                wim.Write(AOutputPath, ManagedWimLib.Wim.AllImages, ManagedWimLib.WriteFlags.None, ManagedWimLib.Wim.DefaultThreads);
            }
        }

        internal static void CopyPackage(string AOutputPath)
        {
            string OutputPath;

            string Folder;

            LogInfo("Starting package creation. Number of files/folders: " + CopyList.Count.ToString());

            // Create (possibly temporary) target folder.
            if (MakingCAB)
            {
                Folder = Path.GetFileNameWithoutExtension(AOutputPath);
                if (Directory.Exists(Folder))
                {
                    Directory.Delete(Folder, true);
                }
                Directory.CreateDirectory(Folder);
                LogDebug("Created temporary target folder: " + Folder);
            }
            else
            {
                Folder = AOutputPath;
                Directory.CreateDirectory(Folder);
                LogDebug("Created target folder: " + Folder);
            }

            OutputPath = Subst(Folder);
            foreach (var kvp in CopyList)
            {
                string Source = kvp.Key;
                string COResult = CopyObject(Source, Path.Combine(OutputPath, kvp.Value));
                if (!string.IsNullOrEmpty(COResult))
                {
                    LogFatal("Copy failure - " + COResult);
                    UnSubst(OutputPath);
                    LogInfo("Deleting incomplete target folder: " + Folder);
                    // #WCOSTODO: no
                    //Directory.Delete(Folder, true);
                    return;
                }
            }

            if (IsWIM)
            {
                MakeWIM(OutputPath, Path.ChangeExtension(AOutputPath, ".wim"));
                return;
            }

            // Make a cabinet file.
            if (MakingCAB)
            {
                LogInfo("Compressing folder to cabinet file: " + AOutputPath);
                /*string TheCommand = "-m LZX:21 -r -p N \"" + Path.GetFullPath(AOutputPath) + "\" *.*";
                LogDebug("Command-line parameters for CABARC.EXE: " + TheCommand);

                // Write a batch file to invoke CABARC.EXE.
                string BatchFileName;
                do
                {
                    BatchFileName = Path.Combine(TempFolder, Path.GetTempFileName() + ".BAT");
                } while (File.Exists(BatchFileName));
                File.AppendAllLines(BatchFileName, new List<string>()
                {
                    "@ECHO OFF",
                    "CD /D " + OutputPath,
                    "CABARC.EXE " + TheCommand,
                    "IF ERRORLEVEL 9009 GOTO RETRY",
                    "GOTO END",
                    ":RETRY",
                    "\"" + Path.Combine(Directory.GetCurrentDirectory(), "CABARC.EXE") + "\" " + TheCommand,
                    ":END"
                });
                LogDebug("Created CABARC.EXE invocation batch file: " + BatchFileName);
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = "CMD.EXE";
                    p.StartInfo.Arguments = "/C \"" + BatchFileName + "\"";
                    if (!DebugMode)
                    {
                        p.StartInfo.UseShellExecute = false;
                        p.StartInfo.CreateNoWindow = true;
                    }
                    LogDebug("Executing: " + p.StartInfo.FileName + " " + p.StartInfo.Arguments);
                    p.Start();
                    p.WaitForExit();
                }
                UnSubst(OutputPath);
                if (!File.Exists(AOutputPath))
                {
                    // Error creating cabinet. Leave the target folder for the user to
                    // deal with.
                    LogError("Couldn't create target cabinet - not deleting temporary target folder: " + Folder);
                }
                else
                {
                    // Remove target folder, leaving just the cabinet.
                    LogDebug("Deleting temporary target folder: " + Folder);
                    //Directory.Delete(Folder, true);
                }

                LogDebug("Deleting CABARC.EXE invocation batch file: " + BatchFileName);
                File.Delete(BatchFileName);*/

                var cabEngine = new Microsoft.Deployment.Compression.Cab.CabEngine();
                cabEngine.CompressionLevel = Microsoft.Deployment.Compression.CompressionLevel.Max;
                cabEngine.Progress += (sender, args) =>
                {
                    Console.Write($"\r{args.FileBytesProcessed}/{args.TotalFileBytes}");
                };
                cabEngine.UseTempFiles = false;

                var files = Directory.GetFiles("\\\\?\\" + OutputPath, "*.*", SearchOption.AllDirectories).Select(a => a.Replace("\\\\?\\" + OutputPath, ""));

                Microsoft.Deployment.Compression.ArchiveFileStreamContext streamContext = new Microsoft.Deployment.Compression.ArchiveFileStreamContext(AOutputPath, "\\\\?\\" + OutputPath, null);
                cabEngine.Pack(streamContext, files);

                Console.WriteLine();
            }
            else
            {
                UnSubst(OutputPath);
            }
        }
    }
}
