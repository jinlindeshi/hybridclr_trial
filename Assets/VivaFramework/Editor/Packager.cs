using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Diagnostics;
using VivaFramework;
using Debug = UnityEngine.Debug;

public class Packager {
    public static string platform = string.Empty;
    static List<string> paths = new List<string>();
    static List<string> files = new List<string>();
    static List<AssetBundleBuild> maps = new List<AssetBundleBuild>();

    ///-----------------------------------------------------------
    static string[] exts = { ".txt", ".xml", ".lua", ".assetbundle", ".json" };
    static bool CanCopy(string ext) {   //能不能复制
        foreach (string e in exts) {
            if (ext.Equals(e)) return true;
        }
        return false;
    }

    /// <summary>
    /// 载入素材
    /// </summary>
    static UnityEngine.Object LoadAsset(string file) {
        if (file.EndsWith(".lua")) file += ".txt";
        return AssetDatabase.LoadMainAssetAtPath("Assets/LuaFramework/Examples/Builds/" + file);
    }

    [MenuItem("VivaFramework/Build iPhone Resource", false, 100)]
    public static void BuildiPhoneResource() {
        BuildAssetResource(BuildTarget.iOS);
    }

    [MenuItem("VivaFramework/Build Android Resource", false, 101)]
    public static void BuildAndroidResource() {
        BuildAssetResource(BuildTarget.Android);
    }

    [MenuItem("VivaFramework/Build Windows Resource", false, 102)]
    public static void BuildWindowsResource() {
        BuildAssetResource(BuildTarget.StandaloneWindows);
    }
    
    [MenuItem("VivaFramework/Build Mac Resource", false, 102)]
    public static void BuildMacResource() {
        BuildAssetResource(BuildTarget.StandaloneOSX);
    }
    
    [MenuItem("VivaFramework/Clear Resource", false, 102)]
    public static void ClearResource() {
        string streamPath = Application.streamingAssetsPath;
        Debug.LogWarning("ClearResource " + streamPath + " - " + Util.DataPath);
        if (Directory.Exists(streamPath)){
            Directory.Delete(streamPath, true);
        }

        if (Directory.Exists(Util.DataPath)) {
            Directory.Delete(Util.DataPath, true);
        }
        AssetDatabase.Refresh();
    }
    public static string GetVerNum(string str)
    {
        string VerDateFm = "yyyy/MM/dd";
        string Head = "LK.";
        if (!string.IsNullOrEmpty(str))
        {
            string[] arr = str.Split('-');
            if (arr.Length > 1 && Head+DateTime.Now.ToString(VerDateFm) == arr[0])
            {
                int num;
                if (int.TryParse(arr[1], out num))
                    return Head + DateTime.Now.ToString(VerDateFm) + "-" +
                           (((num + 1)<10)?(num + 1).ToString("D2"): (num + 1).ToString());
            }
        }
        return Head + DateTime.Now.ToString(VerDateFm) + "-01";
    }

    /// <summary>
    /// 生成AB包
    /// </summary>
    public static void BuildAssetResource(BuildTarget target) {
        
        string streamPath = Application.streamingAssetsPath;
        PlayerSettings.bundleVersion = GetVerNum(PlayerSettings.bundleVersion);
        Debug.LogWarning("BuildAssetResource " + streamPath + " - " + Util.DataPath + " - " + PlayerSettings.bundleVersion);
        if (AppConst.AddBundleBuild != true && Directory.Exists(streamPath)) {
            Directory.Delete(streamPath, true);
        }
        if (Directory.Exists(Util.DataPath)) {
            Directory.Delete(Util.DataPath, true);
        }
        Directory.CreateDirectory(streamPath);
        AssetDatabase.Refresh();
        
        maps.Clear();
        string rootPath = Application.dataPath + "/Res";
        HandlerScenes();
        HandleDeeperDirectory(rootPath, rootPath);
        string resPath = "Assets/" + AppConst.AssetDir;
        BuildPipeline.BuildAssetBundles(resPath, maps.ToArray(), BuildAssetBundleOptions.ChunkBasedCompression, target);
        BuildFileIndex();
        
        string streamDir = Application.dataPath + "/" + AppConst.LuaTempDir;
        if (Directory.Exists(streamDir)) Directory.Delete(streamDir, true);
        AssetDatabase.Refresh();
        Debug.LogWarning("BuildAssetResource 打包结束");
    }
    
    /**
     * 该目录下所有文件打成一个Bundle包
     */
    static void AddBuildMapAll(string bundleName, string path)
    {
        files.Clear();
        Recursive(path);
        if (files.Count == 0) return;
        string[] fs = files.ToArray();

        for (int i = 0; i < fs.Length; i++) {
            fs[i] = fs[i].Replace('\\', '/');
        }
        AssetBundleBuild build = new AssetBundleBuild();
        build.assetBundleName = bundleName;
        build.assetNames = fs;
        maps.Add(build);
    }

    
    /**
     * 场景单独打包
     */
    static void HandlerScenes()
    {
        string[] files = Directory.GetFiles("Assets/Res/Scenes");
        for (int i = 0; i < files.Length; i++)
        {
            List<string> fixedFiles = new List<string>();
            if (files[i].EndsWith(".unity") == true)
            {
                string fullName = files[i].Replace('\\', '/');
                fixedFiles.Add(fullName);
                
                AssetBundleBuild build = new AssetBundleBuild();
                fullName = fullName.Substring(fullName.LastIndexOf("/")+1);
                string name = fullName.Substring(0, fullName.IndexOf("."));
//                Debug.Log("HandlerScenes1 - " + fullName + " - " + name);
                build.assetBundleName = "scene_" + name.ToLower() + AppConst.ExtName;
//                Debug.Log("HandlerScenes2 - " + build.assetBundleName + " - " + fixedFiles.Count);
                build.assetNames = fixedFiles.ToArray();
                maps.Add(build);
            }
            
        }
    }


    static void HandleDeeperDirectory(string nowPath, string rootPath)
    {
        string[] dirs = Directory.GetDirectories(nowPath, "*", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < dirs.Length; i++)
        {
            string name = dirs[i].Replace(rootPath, string.Empty);
            name = name.Replace('\\', '^').Replace('/', '^');
            name = name.ToLower() + AppConst.ExtName;
            name = name.Substring(1);
            Debug.LogWarning("HandleDeeperDirectory1 - " + dirs[i] + " - " + name);
            if (File.Exists(dirs[i] + "/nopack.txt"))
            {
                Debug.LogWarning("HandleDeeperDirectory2 - " + dirs[i]);
                continue;
            }
            string path = "Assets" + dirs[i].Replace(Application.dataPath, "");
            string[] files = Directory.GetFiles(path);
            List<string> fixedFiles = new List<string>();
            for (int j = 0; j < files.Length; j++)
            {
                if (files[j].EndsWith(".meta") == false && files[j].EndsWith(".cs") == false
                                                        && files[j].EndsWith(".unity3d") == false
                                                        && files[j].EndsWith(".unity") == false
                                                        && files[j].EndsWith(".DS_Store") == false)
                {
                    fixedFiles.Add(files[j].Replace('\\', '/'));
                    Debug.LogWarning("HandleDeeperDirectory3 - " + files[j]);
                }
                else
                {
                    Debug.LogWarning("HandleDeeperDirectory4 - " + files[j]);
                }

            }

            if (fixedFiles.Count > 0)
            {
                AssetBundleBuild build = new AssetBundleBuild();
                build.assetBundleName = name;
                build.assetNames = fixedFiles.ToArray();
                maps.Add(build);
            }
            HandleDeeperDirectory(dirs[i], rootPath);
        }
    }

    static void BuildFileIndex() {
        string resPath = AppDataPath + "/StreamingAssets/";
        ///----------------------创建文件列表-----------------------
        string newFilePath = resPath + "/files.txt";
        if (File.Exists(newFilePath)) File.Delete(newFilePath);

        paths.Clear(); files.Clear();
        Recursive(resPath);

        FileStream fs = new FileStream(newFilePath, FileMode.CreateNew);
        StreamWriter sw = new StreamWriter(fs);
        for (int i = 0; i < files.Count; i++) {
            string file = files[i];
            string ext = Path.GetExtension(file);
            if (file.EndsWith(".meta") || file.Contains(".DS_Store")) continue;

            string md5 = Util.md5file(file);
            string value = file.Replace(resPath, string.Empty);
            sw.WriteLine(value + "|" + md5);
        }
        sw.Close(); fs.Close();
    }

    /// <summary>
    /// 数据目录
    /// </summary>
    static string AppDataPath {
        get { return Application.dataPath.ToLower(); }
    }

    /// <summary>
    /// 遍历目录及其子目录
    /// </summary>
    static void Recursive(string path) {
        string[] names = Directory.GetFiles(path);
        string[] dirs = Directory.GetDirectories(path);
        foreach (string filename in names) {
            string ext = Path.GetExtension(filename);
            if (ext.Equals(".meta")) continue;
            files.Add(filename.Replace('\\', '/'));
        }
        foreach (string dir in dirs) {
            paths.Add(dir.Replace('\\', '/'));
            Recursive(dir);
        }
    }

    static void UpdateProgress(int progress, int progressMax, string desc) {
        string title = "Processing...[" + progress + " - " + progressMax + "]";
        float value = (float)progress / (float)progressMax;
        EditorUtility.DisplayProgressBar(title, desc, value);
    }

    public static void EncodeLuaFile(string srcFile, string outFile) {
        if (!srcFile.ToLower().EndsWith(".lua")) {
            File.Copy(srcFile, outFile, true);
            return;
        }
        bool isWin = true; 
        string luaexe = string.Empty;
        string args = string.Empty;
        string exedir = string.Empty;
        string currDir = Directory.GetCurrentDirectory();
        if (Application.platform == RuntimePlatform.WindowsEditor) {
            isWin = true;
            luaexe = "luajit.exe";
            args = "-b -g " + srcFile + " " + outFile;
            exedir = AppDataPath.Replace("assets", "") + "LuaEncoder/luajit/";
        } else if (Application.platform == RuntimePlatform.OSXEditor) {
            isWin = false;
            luaexe = "./luajit";
            args = "-b -g " + srcFile + " " + outFile;
            exedir = AppDataPath.Replace("assets", "") + "LuaEncoder/luajit_mac/";
        }
        Directory.SetCurrentDirectory(exedir);
        ProcessStartInfo info = new ProcessStartInfo();
        info.FileName = luaexe;
        info.Arguments = args;
        info.WindowStyle = ProcessWindowStyle.Hidden;
        info.UseShellExecute = isWin;
        info.ErrorDialog = true;
        Util.Log(info.FileName + " " + info.Arguments);

        Process pro = Process.Start(info);
        pro.WaitForExit();
        Directory.SetCurrentDirectory(currDir);
    }

//    [MenuItem("VivaFramework/Build Protobuf-lua-gen File")]
    public static void BuildProtobufFile() {
//        if (!AppConst.ExampleMode) {
//            UnityEngine.Debug.LogError("若使用编码Protobuf-lua-gen功能，需要自己配置外部环境！！");
//            return;
//        }
        string dir = AppDataPath + "/Lua/3rd/pblua";
        paths.Clear(); files.Clear(); Recursive(dir);

        string protoc = "d:/protobuf-2.4.1/src/protoc.exe";
        string protoc_gen_dir = "\"d:/protoc-gen-lua/plugin/protoc-gen-lua.bat\"";

        foreach (string f in files) {
            string name = Path.GetFileName(f);
            string ext = Path.GetExtension(f);
            if (!ext.Equals(".proto")) continue;

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = protoc;
            info.Arguments = " --lua_out=./ --plugin=protoc-gen-lua=" + protoc_gen_dir + " " + name;
            info.WindowStyle = ProcessWindowStyle.Hidden;
            info.UseShellExecute = true;
            info.WorkingDirectory = dir;
            info.ErrorDialog = true;
            Util.Log(info.FileName + " " + info.Arguments);

            Process pro = Process.Start(info);
            pro.WaitForExit();
        }
        AssetDatabase.Refresh();
    }
}