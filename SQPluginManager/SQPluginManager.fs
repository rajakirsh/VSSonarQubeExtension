namespace SQPluginManager

open System.Xml
open System
open System.Text
open System.IO
open System.Reflection
open System.Threading
open System.Diagnostics
open System.IO.Compression
open VSSonarPlugins

type SQPluginManager(basePath : string) =     
    let installFolder = 
        let pathdata = Path.Combine(basePath, "plugins")
        if not(Directory.Exists(pathdata)) then
            Directory.CreateDirectory(pathdata) |> ignore

        Assembly.LoadFrom(Path.Combine(basePath, "System.Windows.dll")) |> ignore

        AppDomain.CurrentDomain.add_AssemblyResolve(fun _ args ->

            let assemblyToGet = args.Name.Replace(".resources,", ",")

            let GetAssemblyFromCurrentDomain() =
                let mutable assembly : Assembly = null          
                try
                    for assemb in System.AppDomain.CurrentDomain.GetAssemblies() do
                        let fullName = assemb.FullName
                        if fullName.Equals(assemblyToGet) then
                            assembly <- assemb
                with
                | ex -> ()
                assembly

            let GetAssemblyFromInstallFolder() =
                let mutable assembly : Assembly = null          
                try
                    let files = Directory.GetFiles(basePath)
                    for file in files do
                        if file.ToLower().EndsWith(".dll") || file.ToLower().EndsWith(".exe") then
                            let fullName = AssemblyName.GetAssemblyName(file).FullName
                            if fullName.Equals(assemblyToGet) then
                                assembly <- Assembly.LoadFrom(file)
                with
                | ex -> ()
                assembly

            let ref = GetAssemblyFromCurrentDomain()
            if ref <> null then
                ref
            else
                let ref =  GetAssemblyFromInstallFolder()
                if ref <> null then
                    ref
                else
                    null                
        )
        pathdata

    let mutable loadingErrors = List.Empty
    let mutable installErrors = List.Empty

    let UnzipFiles(fileName : string, folder : string) =
        let mutable files : string List = List.Empty
        if not(Directory.Exists(folder)) then
            Directory.CreateDirectory(folder) |> ignore

        use archive = ZipFile.OpenRead(fileName)
        for entry in archive.Entries do
            let endFile = Path.Combine(folder, entry.FullName)
            try
                entry.ExtractToFile(endFile, true)
                files <- files @ [endFile]
            with
            | ex -> Debug.WriteLine("Cannot Extract File: " + ex.Message + " : " + ex.StackTrace)

        files

    let VerifyBinaryCompatibilityAnalysisPlugins(inPlugins : AnalysisPluginHolder List, currPlugins : AnalysisPluginHolder List) =        
        installErrors <- []

        let assemblyCheck(currPlugin : AnalysisPluginHolder, c : AssemblyInfo) =
            let data = currPlugin.RefAssemblies |> List.tryFind(fun elem -> c.Version.Equals(elem.Version) && c.Path.Equals(elem.Path))
            match data with
            | Some v -> installErrors <- installErrors @ [(sprintf "%s %s : %s vs %s : %s" v.FullName v.Path v.Version c.Path c.Version)]
            | None -> ()
            
        let checkAgainstInstalledPluginsAssemblies(c : AssemblyInfo) =
            currPlugins |> List.iter (fun elem -> assemblyCheck(elem, c))


        let checkAgainstInstalledPlugins(c : AnalysisPluginHolder) =
            c.RefAssemblies |> List.iter (fun c -> checkAgainstInstalledPluginsAssemblies(c))
            
        inPlugins |> List.iter (fun c -> checkAgainstInstalledPlugins(c))

        installErrors.IsEmpty

    let GetMenuPlugins(files : string List, privateBinPath : string) =        
        loadingErrors <- []
       
        let typeDom = typeof<PluginProxyDomain>
        AppDomain.CurrentDomain.CreateInstanceAndUnwrap(typeDom.Assembly.FullName, typeDom.FullName) |> ignore
        let data = Assembly.GetExecutingAssembly()
        let executingAssembly = data.FullName
        let asmLoaderProxy = AppDomain.CurrentDomain.CreateInstanceAndUnwrap(executingAssembly, typeof<PluginProxyDomain>.FullName)
        
        let typesd = asmLoaderProxy.GetType()
        let getdiag = typesd.GetMethod("GetMenuPlugins")

        let mutable pluginsOut : MenuPluginHolder List = List.Empty

        let mutable listofAssemblyes : AssemblyInfo List = List.Empty

        for file in files do 
            if file.EndsWith(".dll") then
                let data : System.Object array = Array.zeroCreate 1
                let absPath = Path.Combine(basePath, file)
                data.[0] <- (absPath :> System.Object)

                let assemblyData = AssemblyInfo(AssemblyName.GetAssemblyName(absPath).FullName)
                assemblyData.Path <- absPath
                assemblyData.Version <- AssemblyName.GetAssemblyName(absPath).Version.ToString()
                listofAssemblyes <- listofAssemblyes @ [assemblyData]

                let plugins = (getdiag.Invoke(asmLoaderProxy, data) :?> IMenuCommandPlugin List)
                for plugin in plugins do
                    let newPlug = MenuPluginHolder(plugin.GetHeader())    
                    newPlug.Plugin <- plugin
                    newPlug.Assembly <- assemblyData
                    pluginsOut <- pluginsOut @ [newPlug]

        for plugin in pluginsOut do
            for refass in listofAssemblyes do
                if not(refass.FullName.Equals(plugin.Assembly.FullName)) then
                    plugin.RefAssemblies <- plugin.RefAssemblies @ [refass]
             
        pluginsOut

    let VerifyBinaryCompatibilityMenuPlugins(inPlugins : MenuPluginHolder List, currPlugins : MenuPluginHolder  List) =        
        installErrors <- []

        let assemblyCheck(currPlugin : MenuPluginHolder , c : AssemblyInfo) =
            let data = currPlugin.RefAssemblies |> List.tryFind(fun elem -> not(c.Version.Equals(elem.Version)) && c.Path.Equals(elem.Path))
            match data with
            | Some v -> installErrors <- installErrors @ [(sprintf "%s %s : %s vs %s : %s" v.FullName v.Path v.Version c.Path c.Version)]
            | None -> ()
            
        let checkAgainstInstalledPluginsAssemblies(c : AssemblyInfo) =
            currPlugins |> List.iter (fun elem -> assemblyCheck(elem, c))


        let checkAgainstInstalledPlugins(c : MenuPluginHolder ) =
            c.RefAssemblies |> List.iter (fun c -> checkAgainstInstalledPluginsAssemblies(c))
            
        inPlugins |> List.iter (fun c -> checkAgainstInstalledPlugins(c))

        installErrors.IsEmpty

    let GetAnalysisPlugins(files:string List, privateBinPath : string) =        
        loadingErrors <- []
                
        let typeDom = typeof<PluginProxyDomain>
        AppDomain.CurrentDomain.CreateInstanceAndUnwrap(typeDom.Assembly.FullName, typeDom.FullName) |> ignore
        let data = Assembly.GetExecutingAssembly()
        let executingAssembly = data.FullName
        let asmLoaderProxy = AppDomain.CurrentDomain.CreateInstanceAndUnwrap(executingAssembly, typeof<PluginProxyDomain>.FullName)
        
        let typesd = asmLoaderProxy.GetType()
        let getdiag = typesd.GetMethod("GetAnalysisPlugins")

        let mutable pluginsOut : AnalysisPluginHolder List = List.Empty

        let mutable listofAssemblyes : AssemblyInfo List = List.Empty

        for file in files do
            if file.EndsWith(".dll") then
                let data : System.Object array = Array.zeroCreate 1
                let absPath = Path.Combine(basePath, file)
                data.[0] <- (absPath :> System.Object)

                let assemblyData = AssemblyInfo(AssemblyName.GetAssemblyName(absPath).FullName)
                assemblyData.Path <- absPath
                assemblyData.Version <- AssemblyName.GetAssemblyName(absPath).Version.ToString()
                listofAssemblyes <- listofAssemblyes @ [assemblyData]

                let plugins = (getdiag.Invoke(asmLoaderProxy, data) :?> IAnalysisPlugin List)
                for plugin in plugins do
                    let newPlug = AnalysisPluginHolder(plugin.GetKey(null))    
                    newPlug.Plugin <- plugin
                    newPlug.Assembly <- assemblyData
                    pluginsOut <- pluginsOut @ [newPlug]

        for plugin in pluginsOut do
            for refass in listofAssemblyes do
                if not(refass.FullName.Equals(plugin.Assembly.FullName)) then
                    plugin.RefAssemblies <- plugin.RefAssemblies @ [refass]
             
        pluginsOut

    let GetAssembliesFromFolder(files : string [])=
        let mutable assembliesininstall : AssemblyInfo List = List.Empty
        
        for file in files do
            if file.EndsWith(".dll") then
                let assemblydata = AssemblyName.GetAssemblyName(file)
                let assembly = AssemblyInfo(assemblydata.FullName)
                assembly.Path <- file
                assembly.Version <- assemblydata.Version.ToString()
                assembliesininstall <- assembliesininstall @ [assembly]

        assembliesininstall

    interface ISQPluginManager with
        member x.LoadingErrors() = loadingErrors
        member x.InstallErrors() = installErrors

        member x.TestLoadingOfMenuPlugins(vsqfiletoload:string) =        
            let files = UnzipFiles(vsqfiletoload, Path.Combine(basePath, "tmp"))
            let plugins = GetMenuPlugins(files, "tmp")
            Directory.Delete(Path.Combine(basePath, "tmp"), true)
            plugins

        member x.TestLoadingOfAnalysisPlugins(vsqfiletoload:string) =        
            let files = UnzipFiles(vsqfiletoload, Path.Combine(basePath, "tmp"))
            let plugins = GetAnalysisPlugins(files, "tmp")
            Directory.Delete(Path.Combine(basePath, "tmp"), true)  
            plugins

        member x.InstallAnalysisPlugin(vsqfiletoload:string, currentInstalledPlugins : AnalysisPluginHolder List) =
            let plugins = (x :> ISQPluginManager).TestLoadingOfAnalysisPlugins(vsqfiletoload)

            if VerifyBinaryCompatibilityAnalysisPlugins(plugins, currentInstalledPlugins) then
                if not(plugins.IsEmpty) then
                    UnzipFiles(vsqfiletoload, installFolder) |> ignore
                    for plugin in plugins do
                        plugin.Assembly.Path <- Path.Combine(installFolder, Path.GetFileName(plugin.Assembly.Path))
                        for reference in plugin.RefAssemblies do
                            reference.Path <- Path.Combine(installFolder, Path.GetFileName(reference.Path))                      
                plugins
            else
                List.Empty

        member x.InstallMenuPlugin(vsqfiletoload:string, currentInstalledPlugins : MenuPluginHolder List) =
            let plugins = (x :> ISQPluginManager).TestLoadingOfMenuPlugins(vsqfiletoload)

            if VerifyBinaryCompatibilityMenuPlugins(plugins, currentInstalledPlugins) then
                if not(plugins.IsEmpty) then
                    UnzipFiles(vsqfiletoload, installFolder) |> ignore
                    for plugin in plugins do
                        plugin.Assembly.Path <- Path.Combine(installFolder, Path.GetFileName(plugin.Assembly.Path))
                        for reference in plugin.RefAssemblies do
                            reference.Path <- Path.Combine(installFolder, Path.GetFileName(reference.Path))                              
                        
                plugins
            else
                List.Empty

        member x.LoadMenuPlugins() =
            let pluginsFiles = Directory.GetFiles(installFolder)
            let assemblies = GetAssembliesFromFolder(pluginsFiles)
            let mutable plugins : MenuPluginHolder List = List.Empty
            for file in pluginsFiles do
                for plugin in GetMenuPlugins([file], "plugins") do
                    plugins <- plugins @ [plugin] 

            for plugin in plugins do
                let typedata = plugin.Plugin.GetType()
                let referenceAssemblies = typedata.Assembly.GetReferencedAssemblies()
                for refassembly in referenceAssemblies do
                    let assemblydata = assemblies |> List.tryFind (fun elem -> elem.FullName.Equals(refassembly.FullName))
                    match assemblydata with
                    | Some c -> plugin.RefAssemblies <- plugin.RefAssemblies @ [c]
                    | None -> ()

            plugins

        member x.LoadAnalysisPlugins() =
            let pluginsFiles = Directory.GetFiles(installFolder)
            let assemblies = GetAssembliesFromFolder(pluginsFiles)
            let mutable plugins : AnalysisPluginHolder List = List.Empty
            for file in pluginsFiles do
                for plugin in GetAnalysisPlugins([file], "plugins") do
                    plugins <- plugins @ [plugin] 

            for plugin in plugins do
                let typedata = plugin.Plugin.GetType()
                let referenceAssemblies = typedata.Assembly.GetReferencedAssemblies()
                for refassembly in referenceAssemblies do
                    let assemblydata = assemblies |> List.tryFind (fun elem -> elem.FullName.Equals(refassembly.FullName) && elem.Version.Equals(refassembly.Version))
                    match assemblydata with
                    | Some c -> plugin.RefAssemblies <- plugin.RefAssemblies @ [c]
                    | None -> ()

            plugins

        member x.DeleteMenuPlugin(plugin : MenuPluginHolder, pluginsList : MenuPluginHolder List) =
        
            let lookAtAssemblies(pluginele : MenuPluginHolder, assembly : AssemblyInfo) = 
                let found = pluginele.RefAssemblies |> List.tryFind (fun c -> assembly.Version.Equals(c.Version))

                if pluginele.Id.Equals(plugin.Id) then
                    false
                else
                    match found with
                    | Some c -> true
                    | None -> false
                
            let isFoundinList(elem : AssemblyInfo) =
                let  found = pluginsList |> List.tryFind (fun c -> lookAtAssemblies(c, elem))
                match found with
                | Some c -> true
                | None -> false

            for assembly in plugin.RefAssemblies do
              if not(isFoundinList(assembly)) then
                File.Delete(assembly.Path)

            File.Delete(plugin.Assembly.Path)

        member x.DeleteAnalysisPlugin(plugin : AnalysisPluginHolder, pluginsList : AnalysisPluginHolder List) =
        
            let lookAtAssemblies(pluginele : AnalysisPluginHolder, assembly : AssemblyInfo) = 
                let found = pluginele.RefAssemblies |> List.tryFind (fun c -> assembly.Version.Equals(c.Version))

                if pluginele.Id.Equals(plugin.Id) then
                    false
                else
                    match found with
                    | Some c -> true
                    | None -> false
                
            let isFoundinList(elem : AssemblyInfo) =
                let  found = pluginsList |> List.tryFind (fun c -> lookAtAssemblies(c, elem))
                match found with
                | Some c -> true
                | None -> false

            for assembly in plugin.RefAssemblies do
              if not(isFoundinList(assembly)) then
                File.Delete(assembly.Path)

            File.Delete(plugin.Assembly.Path)

    
                        