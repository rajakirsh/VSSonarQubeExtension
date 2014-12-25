namespace SQPluginManager

open System.Xml
open System
open System.Text
open System.IO
open System.Reflection
open Microsoft.CodeAnalysis
open ExtensionTypes
open ZeroMQ
open System.Threading
open System.Diagnostics
open System.IO.Compression
open VSSonarPlugins

type SQPluginManager(basePath : string) =     
    let installFolder = Path.Combine(basePath, "plugins")
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

    let VerifyBinaryCompatibilityAnalysisPlugins(inPlugins : AnalysisPlugin List, currPlugins : AnalysisPlugin List) =        
        installErrors <- []

        let assemblyCheck(currPlugin : AnalysisPlugin, c : AssemblyInfo) =
            let data = currPlugin.RefAssemblies |> List.tryFind(fun elem -> c.Version.Equals(elem.Version) && c.Path.Equals(elem.Path))
            match data with
            | Some v -> installErrors <- installErrors @ [(sprintf "%s %s : %s vs %s : %s" v.FullName v.Path v.Version c.Path c.Version)]
            | None -> ()
            
        let checkAgainstInstalledPluginsAssemblies(c : AssemblyInfo) =
            currPlugins |> List.iter (fun elem -> assemblyCheck(elem, c))


        let checkAgainstInstalledPlugins(c : AnalysisPlugin) =
            c.RefAssemblies |> List.iter (fun c -> checkAgainstInstalledPluginsAssemblies(c))
            
        inPlugins |> List.iter (fun c -> checkAgainstInstalledPlugins(c))

        installErrors.IsEmpty

    let GetMenuPlugins(files : string List, privateBinPath : string) =        
        loadingErrors <- []

        let domaininfo = new AppDomainSetup(ApplicationBase = basePath, PrivateBinPath = privateBinPath)
        let adevidence = AppDomain.CurrentDomain.Evidence
        let domain = AppDomain.CreateDomain("PluginDomain", adevidence, domaininfo)

        AppDomain.CurrentDomain.add_AssemblyResolve(fun _ args ->
            let name = System.Reflection.AssemblyName(args.Name)
            let existingAssembly = 
                System.AppDomain.CurrentDomain.GetAssemblies()
                |> Seq.tryFind(fun a -> System.Reflection.AssemblyName.ReferenceMatchesDefinition(name, a.GetName()))
            match existingAssembly with
            | Some a -> a
            | None -> loadingErrors <- loadingErrors @ [args.Name]
                      null 
        )
        
        let typeDom = typeof<PluginProxyDomain>
        domain.CreateInstanceAndUnwrap(typeDom.Assembly.FullName, typeDom.FullName) |> ignore
        let data = Assembly.GetExecutingAssembly()
        let executingAssembly = data.FullName
        let asmLoaderProxy = domain.CreateInstanceAndUnwrap(executingAssembly, typeof<PluginProxyDomain>.FullName)
        
        let typesd = asmLoaderProxy.GetType()
        let getdiag = typesd.GetMethod("GetMenuPlugins")

        let mutable pluginsOut : MenuPlugin List = List.Empty

        let mutable listofAssemblyes : AssemblyInfo List = List.Empty

        for file in files do 
            let data : System.Object array = Array.zeroCreate 1
            let absPath = Path.Combine(basePath, file)
            data.[0] <- (absPath :> System.Object)

            let assemblyData = AssemblyInfo(AssemblyName.GetAssemblyName(absPath).FullName)
            assemblyData.Path <- absPath
            assemblyData.Version <- AssemblyName.GetAssemblyName(absPath).Version.ToString()
            listofAssemblyes <- listofAssemblyes @ [assemblyData]

            let plugins = (getdiag.Invoke(asmLoaderProxy, data) :?> IMenuCommandPlugin List)
            for plugin in plugins do
                let newPlug = MenuPlugin(plugin.GetHeader())    
                newPlug.Plugin <- plugin
                newPlug.Assembly <- assemblyData
                pluginsOut <- pluginsOut @ [newPlug]

        for plugin in pluginsOut do
            for refass in listofAssemblyes do
                if not(refass.FullName.Equals(plugin.Assembly.FullName)) then
                    plugin.RefAssemblies <- plugin.RefAssemblies @ [refass]
             
        AppDomain.Unload(domain)
        pluginsOut

    let VerifyBinaryCompatibilityMenuPlugins(inPlugins : MenuPlugin List, currPlugins : MenuPlugin  List) =        
        installErrors <- []

        let assemblyCheck(currPlugin : MenuPlugin , c : AssemblyInfo) =
            let data = currPlugin.RefAssemblies |> List.tryFind(fun elem -> not(c.Version.Equals(elem.Version)) && c.Path.Equals(elem.Path))
            match data with
            | Some v -> installErrors <- installErrors @ [(sprintf "%s %s : %s vs %s : %s" v.FullName v.Path v.Version c.Path c.Version)]
            | None -> ()
            
        let checkAgainstInstalledPluginsAssemblies(c : AssemblyInfo) =
            currPlugins |> List.iter (fun elem -> assemblyCheck(elem, c))


        let checkAgainstInstalledPlugins(c : MenuPlugin ) =
            c.RefAssemblies |> List.iter (fun c -> checkAgainstInstalledPluginsAssemblies(c))
            
        inPlugins |> List.iter (fun c -> checkAgainstInstalledPlugins(c))

        installErrors.IsEmpty

    let GetAnalysisPlugins(files:string List, privateBinPath : string) =        
        loadingErrors <- []

        let domaininfo = new AppDomainSetup(ApplicationBase = basePath, PrivateBinPath = privateBinPath)
        let adevidence = AppDomain.CurrentDomain.Evidence
        let domain = AppDomain.CreateDomain("PluginDomain", adevidence, domaininfo)

        AppDomain.CurrentDomain.add_AssemblyResolve(fun _ args ->
            let name = System.Reflection.AssemblyName(args.Name)
            let existingAssembly = 
                System.AppDomain.CurrentDomain.GetAssemblies()
                |> Seq.tryFind(fun a -> System.Reflection.AssemblyName.ReferenceMatchesDefinition(name, a.GetName()))
            match existingAssembly with
            | Some a -> a
            | None -> loadingErrors <- loadingErrors @ [args.Name]
                      null 
        )
        
        let typeDom = typeof<PluginProxyDomain>
        domain.CreateInstanceAndUnwrap(typeDom.Assembly.FullName, typeDom.FullName) |> ignore
        let data = Assembly.GetExecutingAssembly()
        let executingAssembly = data.FullName
        let asmLoaderProxy = domain.CreateInstanceAndUnwrap(executingAssembly, typeof<PluginProxyDomain>.FullName)
        
        let typesd = asmLoaderProxy.GetType()
        let getdiag = typesd.GetMethod("GetAnalysisPlugins")

        let mutable pluginsOut : AnalysisPlugin List = List.Empty

        let mutable listofAssemblyes : AssemblyInfo List = List.Empty

        for file in files do 
            let data : System.Object array = Array.zeroCreate 1
            let absPath = Path.Combine(basePath, file)
            data.[0] <- (absPath :> System.Object)

            let assemblyData = AssemblyInfo(AssemblyName.GetAssemblyName(absPath).FullName)
            assemblyData.Path <- absPath
            assemblyData.Version <- AssemblyName.GetAssemblyName(absPath).Version.ToString()
            listofAssemblyes <- listofAssemblyes @ [assemblyData]

            let plugins = (getdiag.Invoke(asmLoaderProxy, data) :?> IAnalysisPlugin List)
            for plugin in plugins do
                let newPlug = AnalysisPlugin(plugin.GetKey(null))    
                newPlug.Plugin <- plugin
                newPlug.Assembly <- assemblyData
                pluginsOut <- pluginsOut @ [newPlug]

        for plugin in pluginsOut do
            for refass in listofAssemblyes do
                if not(refass.FullName.Equals(plugin.Assembly.FullName)) then
                    plugin.RefAssemblies <- plugin.RefAssemblies @ [refass]
             
        AppDomain.Unload(domain)
        pluginsOut

    let GetAssembliesFromFolder(files : string [])=
        let mutable assembliesininstall : AssemblyInfo List = List.Empty
        
        for file in files do
            let assemblydata = AssemblyName.GetAssemblyName(file)
            let assembly = AssemblyInfo(assemblydata.FullName)
            assembly.Path <- file
            assembly.Version <- assemblydata.Version.ToString()
            assembliesininstall <- assembliesininstall @ [assembly]

        assembliesininstall

    member x.LoadingErrors = loadingErrors
    member x.InstallErrors = installErrors


    member x.TestLoadingOfMenuPlugins(vsqfiletoload:string) =        
        let files = UnzipFiles(vsqfiletoload, "tmp")
        let plugins = GetMenuPlugins(files, "tmp")
        Directory.Delete(Path.Combine(basePath, "tmp"), true)
        plugins

    member x.TestLoadingOfAnalysisPlugins(vsqfiletoload:string) =        
        let files = UnzipFiles(vsqfiletoload, "tmp")
        let plugins = GetAnalysisPlugins(files, "tmp")
        Directory.Delete(Path.Combine(basePath, "tmp"), true)  
        plugins

    member x.InstallAnalysisPlugin(vsqfiletoload:string, currentInstalledPlugins : AnalysisPlugin List) =
        let plugins = x.TestLoadingOfAnalysisPlugins(vsqfiletoload)

        if VerifyBinaryCompatibilityAnalysisPlugins(plugins, currentInstalledPlugins) then
            if not(plugins.IsEmpty) then
                UnzipFiles(vsqfiletoload, installFolder) |> ignore           
                        
            plugins
        else
            List.Empty

    member x.InstallMenuPlugin(vsqfiletoload:string, currentInstalledPlugins : MenuPlugin List) =
        let plugins = x.TestLoadingOfMenuPlugins(vsqfiletoload)

        if VerifyBinaryCompatibilityMenuPlugins(plugins, currentInstalledPlugins) then
            if not(plugins.IsEmpty) then
                UnzipFiles(vsqfiletoload, installFolder) |> ignore           
                        
            plugins
        else
            List.Empty

    member x.LoadMenuPlugins() =
        let pluginsFiles = Directory.GetFiles(installFolder)
        let assemblies = GetAssembliesFromFolder(pluginsFiles)
        let mutable plugins : MenuPlugin List = List.Empty
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
        let mutable plugins : AnalysisPlugin List = List.Empty
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

    member x.DeleteMenuPlugin(plugin : MenuPlugin, pluginsList : MenuPlugin List) =
        
        let lookAtAssemblies(pluginele : MenuPlugin, assembly : AssemblyInfo) = 
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

    member x.DeleteAnalysisPlugin(plugin : AnalysisPlugin, pluginsList : AnalysisPlugin List) =
        
        let lookAtAssemblies(pluginele : AnalysisPlugin, assembly : AssemblyInfo) = 
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

    
                        