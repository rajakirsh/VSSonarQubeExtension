namespace SQPluginManager.Test

open NUnit.Framework
open FsUnit
open SQPluginManager
open System.IO
open System
open Foq
open SonarRestService
open ExtensionTypes
open System.Text

type SQPluginManagerTest() = 
    [<SetUp>]
    member test.``SetUp`` () = ()

    [<TearDown>]
    member test.``tearDown`` () = ()
            
    [<Test>]
    member test.``Test Loading of MenuPlugin Correctly`` () = 
        let adapter = SQPluginManager(Environment.CurrentDirectory) :> ISQPluginManager
        let plugin = adapter.TestLoadingOfMenuPlugins("SqaleUi.VSQ")

        if adapter.LoadingErrors().Length <> 0 then
            adapter.LoadingErrors() |> List.iter (fun elem -> printf "%s" elem)

        Directory.Exists(Path.Combine(Environment.CurrentDirectory, "tmp")) |> should be False
        plugin.[0].Id |> should equal "Sonar Quality Editor"
        plugin.[0].RefAssemblies.[0].Version |> should equal "1.4.2.1"
        plugin.[0].RefAssemblies.Length |> should equal 2

    [<Test>]
    member test.``Install MenuPlugin Correctly`` () = 
        let adapter = SQPluginManager(Environment.CurrentDirectory) :> ISQPluginManager
        let plugins = adapter.InstallMenuPlugin("SqaleUi.VSQ", List.Empty)

        if adapter.LoadingErrors().Length <> 0 then
            adapter.LoadingErrors() |> List.iter (fun elem -> printf "%s" elem)

        Directory.Exists(Path.Combine(Environment.CurrentDirectory, "tmp")) |> should be False
        Directory.Exists(Path.Combine(Environment.CurrentDirectory, "plugins")) |> should be True
        plugins.Length |> should equal 1
        plugins.[0].Id |> should equal "Sonar Quality Editor"

        Directory.Delete(Path.Combine(Environment.CurrentDirectory, "plugins"), true)

    [<Test>]
    member test.``Test Loading of Analysis Plugin Correctly`` () = 
        let adapter = SQPluginManager(Environment.CurrentDirectory) :> ISQPluginManager
        let plugins = adapter.TestLoadingOfAnalysisPlugins("CxxPlugin.VSQ")

        if adapter.LoadingErrors().Length <> 0 then
            adapter.LoadingErrors() |> List.iter (fun elem -> printf "%s" elem)

        Directory.Exists(Path.Combine(Environment.CurrentDirectory, "tmp")) |> should be False
        plugins.[0].Id |> should equal "CxxPlugin"
        plugins.[0].RefAssemblies.Length |> should equal 0
        plugins.Length |> should equal 1

    [<Test>]
    member test.``Install Analysis Plugin Correctly`` () = 
        let adapter = SQPluginManager(Environment.CurrentDirectory) :> ISQPluginManager
        let plugins = adapter.InstallAnalysisPlugin("CxxPlugin.VSQ", List.Empty)

        if adapter.LoadingErrors().Length <> 0 then
            adapter.LoadingErrors() |> List.iter (fun elem -> printf "%s" elem)

        Directory.Exists(Path.Combine(Environment.CurrentDirectory, "tmp")) |> should be False
        Directory.Exists(Path.Combine(Environment.CurrentDirectory, "plugins")) |> should be True
        plugins.Length |> should equal 1
        plugins.[0].Plugin.GetKey(null) |> should equal "CxxPlugin"

        Directory.Delete(Path.Combine(Environment.CurrentDirectory, "plugins"), true)

    [<Test>]
    member test.``Load Menu Plugins`` () = 
        let adapter = SQPluginManager(Environment.CurrentDirectory) :> ISQPluginManager
        adapter.InstallMenuPlugin("SqaleUi.VSQ", List.Empty) |> ignore
        adapter.InstallAnalysisPlugin("CxxPlugin.VSQ", List.Empty)  |> ignore

        let plugins = adapter.LoadMenuPlugins()
        plugins.Length |> should equal 1
        plugins.[0].RefAssemblies.Length |> should equal 2

        Directory.Delete(Path.Combine(Environment.CurrentDirectory, "plugins"), true)

    [<Test>]
    member test.``Load Analysis Plugins`` () = 
        let adapter = SQPluginManager(Environment.CurrentDirectory) :> ISQPluginManager
        adapter.InstallMenuPlugin("SqaleUi.VSQ", List.Empty) |> ignore
        adapter.InstallAnalysisPlugin("CxxPlugin.VSQ", List.Empty)  |> ignore

        let plugins = adapter.LoadAnalysisPlugins()
        plugins.Length |> should equal 1
        plugins.[0].RefAssemblies.Length |> should equal 0

        Directory.Delete(Path.Combine(Environment.CurrentDirectory, "plugins"), true)

    [<Test>]
    member test.``Delete Menu Plugins`` () = 
        let adapter = SQPluginManager(Environment.CurrentDirectory) :> ISQPluginManager
        adapter.InstallMenuPlugin("SqaleUi.VSQ", List.Empty) |> ignore
        adapter.InstallAnalysisPlugin("CxxPlugin.VSQ", List.Empty)  |> ignore

        let plugins = adapter.LoadMenuPlugins()
        plugins.Length |> should equal 1
        plugins.[0].RefAssemblies.Length |> should equal 2

        adapter.DeleteMenuPlugin(plugins.[0], plugins)

        let files = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "plugins"))
        files.Length |> should equal 1

        Directory.Delete(Path.Combine(Environment.CurrentDirectory, "plugins"), true)

    [<Test>]
    member test.``Should Keep Reference Assemblies in Place for Menu Plugins`` () = 
        let adapter = SQPluginManager(Environment.CurrentDirectory) :> ISQPluginManager
        adapter.InstallMenuPlugin("SqaleUi.VSQ", List.Empty) |> ignore
        adapter.InstallAnalysisPlugin("CxxPlugin.VSQ", List.Empty)  |> ignore

        let mutable plugins = adapter.LoadMenuPlugins()
        plugins.Length |> should equal 1
        plugins.[0].RefAssemblies.Length |> should equal 2

        let plugin2 = MenuPluginHolder("another")
        plugin2.RefAssemblies <- plugins.[0].RefAssemblies
        plugins <- plugins @ [plugin2]

        adapter.DeleteMenuPlugin(plugins.[0], plugins)

        let files = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "plugins"))
        files.Length |> should equal 3

        Directory.Delete(Path.Combine(Environment.CurrentDirectory, "plugins"), true)

    [<Test>]
    member test.``Delete Analysis Plugins`` () = 
        let adapter = SQPluginManager(Environment.CurrentDirectory) :> ISQPluginManager
        adapter.InstallMenuPlugin("SqaleUi.VSQ", List.Empty) |> ignore
        adapter.InstallAnalysisPlugin("CxxPlugin.VSQ", List.Empty)  |> ignore

        let plugins = adapter.LoadAnalysisPlugins()
        plugins.Length |> should equal 1
        plugins.[0].RefAssemblies.Length |> should equal 0

        adapter.DeleteAnalysisPlugin(plugins.[0], plugins)

        let files = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "plugins"))
        files.Length |> should equal 3

        Directory.Delete(Path.Combine(Environment.CurrentDirectory, "plugins"), true)







        