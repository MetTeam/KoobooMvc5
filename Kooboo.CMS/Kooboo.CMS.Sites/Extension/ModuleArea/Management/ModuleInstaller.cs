﻿using Kooboo.CMS.Sites.Extension.Management;
using Kooboo.CMS.Sites.Extension.ModuleArea.Management.Events;
using Kooboo.Globalization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Mvc;

namespace Kooboo.CMS.Sites.Extension.ModuleArea.Management
{
    /// <summary>
    /// Module installation steps:
    /// 1. Unzip
    /// 2. Check conflict assembly references
    /// 2. Copy assemblies
    /// 3. Run module installing event
    /// </summary>
    [Kooboo.CMS.Common.Runtime.Dependency.Dependency(typeof(IModuleInstaller))]
    public class ModuleInstaller : IModuleInstaller
    {
        #region .ctor
        IAssemblyReferences _assemblyReferences;
        IModuleVersioning _moduleVersioning;
        public ModuleInstaller(IAssemblyReferences assemblyReferences, IModuleVersioning moduleVersioning)
        {
            this._assemblyReferences = assemblyReferences;
            this._moduleVersioning = moduleVersioning;
        }
        #endregion

        #region Unzip
        public string Unzip(ref string moduleName, Stream moduleStream, string user)
        {
            string error = "";
            using (ModuleStreamEntry moduleEntry = new ModuleStreamEntry(moduleName, moduleStream))
            {
                if (!moduleEntry.IsValid())
                {
                    error = "The module is invalid.".Localize();
                    return error;
                }

                var moduleInfo = moduleEntry.ModuleInfo;
                if (moduleInfo == null)
                {
                    error = "The module.config file is invalid.".Localize();
                    return error;
                }

                moduleName = moduleEntry.ModuleName;

                ModulePath modulePath = new ModulePath(moduleEntry.ModuleName);
                var modulePhysicalPath = modulePath.PhysicalPath;

                if (Directory.Exists(modulePath.PhysicalPath))
                {
                    error = "The module name already exists.".Localize();
                    return error;
                }

                //save the module version
                var installationFile = this._moduleVersioning.SaveInstallationFile(moduleEntry);
                this._moduleVersioning.LogInstallation(moduleName, new InstallationContext(moduleInfo.ModuleName, moduleInfo.Version, DateTime.UtcNow) { User = user, InstallationFileName = installationFile });


                moduleEntry.Extract(modulePhysicalPath);
            }
            return error;
        }
        #endregion

        #region CheckConflictedAssemblyReferences
        public IEnumerable<ConflictedAssemblyReference> CheckConflictedAssemblyReferences(string moduleName)
        {
            var assemblyFiles = GetAssemblyFiles(moduleName);
            return _assemblyReferences.Check(assemblyFiles);
        }
        #endregion

        #region GetAssemblyFiles
        private IEnumerable<string> GetAssemblyFiles(string moduleName)
        {
            ModulePath modulePath = new ModulePath(moduleName);
            ModuleItemPath moduleBinPath = new ModuleItemPath(moduleName, "Bin");
            if (Directory.Exists(moduleBinPath.PhysicalPath))
            {
                return Directory.EnumerateFiles(moduleBinPath.PhysicalPath);
            }
            else
            {
                return new string[0];
            }
        }
        #endregion

        #region CopyAssemblies
        public void CopyAssemblies(string moduleName, bool @overrideSystemVersion)
        {
            var assemblyFiles = GetAssemblyFiles(moduleName);
            var binPath = Settings.BinDirectory;
            foreach (var item in assemblyFiles)
            {
                var fileName = Path.GetFileName(item);
                var fileNameInBin = Path.Combine(binPath, fileName);
                var exists = File.Exists(fileNameInBin);
                if (!exists || (exists && overrideSystemVersion))
                {
                    File.Copy(item, fileNameInBin, overrideSystemVersion);
                }
                _assemblyReferences.AddReference(fileNameInBin, moduleName);
            }
        }
        #endregion

        #region RunEvent
        public void RunEvent(string moduleName, ControllerContext controllerContext)
        {
            var moduleEvents = Kooboo.CMS.Common.Runtime.EngineContext.Current.TryResolve<IModuleInstallingEvents>(moduleName);

            if (moduleEvents != null)
            {
                moduleEvents.OnInstalling(new ModuleContext(moduleName), controllerContext);
            }
        }

        #endregion
    }
}