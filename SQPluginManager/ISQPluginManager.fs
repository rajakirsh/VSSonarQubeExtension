namespace SQPluginManager

open System.Collections.ObjectModel
open System
open System.Web

type ISQPluginManager = 
    abstract member LoadingErrors : unit -> string List
    abstract member InstallErrors : unit -> string List

    abstract member TestLoadingOfMenuPlugins : vsqfiletoload:string -> MenuPluginHolder List
    abstract member TestLoadingOfAnalysisPlugins : vsqfiletoload:string -> AnalysisPluginHolder List

    abstract member InstallAnalysisPlugin : vsqfiletoload:string * currentInstalledPlugins : AnalysisPluginHolder List -> AnalysisPluginHolder List
    abstract member InstallMenuPlugin : vsqfiletoload:string * currentInstalledPlugins : MenuPluginHolder List -> MenuPluginHolder List

    abstract member LoadMenuPlugins : unit -> MenuPluginHolder List
    abstract member LoadAnalysisPlugins : unit -> AnalysisPluginHolder List

    abstract member DeleteMenuPlugin : plugin : MenuPluginHolder * pluginsList : MenuPluginHolder List ->  unit
    abstract member DeleteAnalysisPlugin : plugin : AnalysisPluginHolder * pluginsList : AnalysisPluginHolder List ->  unit


