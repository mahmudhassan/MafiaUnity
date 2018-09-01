﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace MafiaUnity
{
    [Serializable]
    public class Mod
    {
        string modPath, modName;
        public string name;
        public string author;
        public string version;
        public string gameVersion;
        public Assembly assembly;
        [SerializeField] public List<string> dependencies;

        public Mod(string name)
        {
            modName = name;
            modPath = Path.Combine("Mods", name);
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="modEntries"></param>
        public void Init(List<ModEntry> modEntries)
        {
            bool isMissingDependency = false;

            foreach (var dep in dependencies)
            {
                Mod pkg = null;
                if (modEntries == null)
                {
                    pkg = GameAPI.instance.modManager.GetActiveMod(dep);
                }
                else
                {
                    pkg = modEntries.First(x => x.modName == dep && x.status == ModEntryStatus.Active)?.modMeta;
                }

                if (pkg == null)
                {
                    Debug.LogErrorFormat("Mod: '{0}' is missing a dependency '{1}'", name, dep);
                    isMissingDependency = true;
                }
            }

            if (isMissingDependency)
            {
                Debug.LogErrorFormat("Make sure all dependencies for mod '{0}' are met.", name);
                return;
            }

            GameAPI.instance.fileSystem.AddOptionalPath(modPath);

            var scriptsPath = Path.Combine(modPath, "Scripts");

            if (Directory.Exists(scriptsPath))
            {
                var fileNames = Directory.GetFiles(scriptsPath);
                var sources = new List<string>();

                foreach (var fileName in fileNames)
                {
                    if (File.Exists(fileName) && Path.GetExtension(fileName) == ".cs")
                        sources.Add(File.ReadAllText(fileName));
                }

                if (sources.Count < 1)
                    return;

                assembly = Compiler.CompileSource(modName, sources.ToArray(), true);

                if (assembly == null)
                {
                    Debug.LogError("Assembly for " + modName + " couldn't be compiled!");
                    return;
                }
            }
        }

        public void Start()
        {
            if (assembly == null)
                return;
                
            var allTypes = Compiler.GetLoadableTypes(assembly);

            foreach (var type in allTypes)
            {
                if (type.ToString() == "ScriptMain")
                {
                    IModScript entry = (IModScript)assembly.CreateInstance(type.ToString(), true);

                    if (entry == null)
                        break;

                    entry.Start(this);

                    break;
                }
            }
        }

        public void Destroy()
        {
            GameAPI.instance.fileSystem.RemoveOptionalPath(modPath);
        }

        /// <summary>
        /// Returns Unity's AssetBundle. It follows Unity's API but handles the paths for user.
        /// See https://docs.unity3d.com/ScriptReference/AssetBundle.LoadFromFile.html
        /// </summary>
        /// <param name="path"></param>
        /// <param name="crc"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public AssetBundle LoadFromFile(string path, uint crc = 0, ulong offset = 0)
        {
            var bundlesPath = Path.Combine(modPath, "Bundles");

            if (!Directory.Exists(bundlesPath))
                return null;

            return AssetBundle.LoadFromFile(Path.Combine(bundlesPath, path), crc, offset);
        }

        /// <summary>
        /// Async variant of LoadFromFile. It follows Unity's API but handles the paths for user.
        /// See https://docs.unity3d.com/ScriptReference/AssetBundle.LoadFromFileAsync.html
        /// </summary>
        /// <param name="path"></param>
        /// <param name="crc"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public AssetBundleCreateRequest LoadFromFileAsync(string path, uint crc = 0, ulong offset = 0)
        {
            var bundlesPath = Path.Combine(modPath, "Bundles");

            if (!Directory.Exists(bundlesPath))
                return null;

            return AssetBundle.LoadFromFileAsync(Path.Combine(bundlesPath, path), crc, offset);
        }

        /// <summary>
        /// Returns the path to the mod.
        /// </summary>
        /// <returns></returns>
        public string GetModPath()
        {
            return modPath;
        }
    }

    public enum ModEntryStatus
    {
        Inactive,
        Active,
        Incomplete
    }

    public class ModEntry
    {
        public string modName;
        public Mod modMeta;
        public ModEntryStatus status;
        public List<string> missingDependencies = new List<string>();
    }
}