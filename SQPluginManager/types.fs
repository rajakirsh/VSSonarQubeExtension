namespace SQPluginManager

open System.Xml
open System.Xml.Linq
open System
open System.Text
open System.IO
open System.Reflection
open System.Threading
open VSSonarPlugins

[<AllowNullLiteral>]
type AssemblyInfo(fullName:string) =
    member val FullName : string = fullName with get, set
    member val Version = "" with get, set
    member val Path = "" with get, set

[<AllowNullLiteral>]
type MenuPluginHolder(idIn:string) =    
    member val Id : string = idIn with get, set    
    member val RefAssemblies : AssemblyInfo List = List.Empty with get, set
    member val Assembly : AssemblyInfo = null with get, set
    member val Plugin : IMenuCommandPlugin = null with get, set
    member val ErrorMessage : string = "" with get, set

[<AllowNullLiteral>]
type AnalysisPluginHolder(idIn:string) =    
    member val Id : string = idIn with get, set    
    member val RefAssemblies : AssemblyInfo List = List.Empty with get, set
    member val Assembly : AssemblyInfo = null with get, set
    member val Plugin : IAnalysisPlugin = null with get, set
    member val ErrorMessage : string = "" with get, set

type PluginProxyDomain() =

    member x.GetAnalysisPlugins(path:string) = 
        let mutable plugins = List.Empty
        try
            let b = File.ReadAllBytes(path)
            let assembly = Assembly.Load(b)
            let types2 = assembly.GetTypes()
            
            for typedata in types2 do   
                let interfaces = typedata.GetInterfaces()
                let data = interfaces |> Seq.tryFind ( fun elem -> elem = typeof<IAnalysisPlugin>)
                match data with
                | None -> ()
                | Some value -> 
                    let properties = typedata.GetFields(BindingFlags.NonPublic)                    
                    let data : System.Object array = Array.zeroCreate 1
                                        
                    let plugin = typedata.GetConstructor(Type.EmptyTypes)
                    let pluginObj = plugin.Invoke(null) :?> IAnalysisPlugin
                    plugins <- plugins @ [pluginObj]

            plugins
        with
        | ex -> plugins

    member x.GetMenuPlugins(path:string) = 
        let mutable plugins = List.Empty
        try
            let b = File.ReadAllBytes(path)
            let assembly = Assembly.Load(b)
            let types2 = assembly.GetTypes()
            
            for typedata in types2 do   
                let interfaces = typedata.GetInterfaces()
                let data = interfaces |> Seq.tryFind ( fun elem -> elem = typeof<IMenuCommandPlugin>)
                match data with
                | None -> ()
                | Some value -> 
                    let properties = typedata.GetFields(BindingFlags.NonPublic)                    
                    let data : System.Object array = Array.zeroCreate 1
                                        
                    let plugin = typedata.GetConstructor(Type.EmptyTypes)
                    let pluginObj = plugin.Invoke(null) :?> IMenuCommandPlugin
                    plugins <- plugins @ [pluginObj]

            plugins
        with
        | ex -> plugins