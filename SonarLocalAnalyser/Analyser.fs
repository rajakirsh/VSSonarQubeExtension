﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Analyser.fs" company="Copyright © 2014 Tekla Corporation. Tekla is a Trimble Company">
//     Copyright (C) 2013 [Jorge Costa, Jorge.Costa@tekla.com]
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
// This program is free software; you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License
// as published by the Free Software Foundation; either version 3 of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty
// of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details. 
// You should have received a copy of the GNU Lesser General Public License along with this program; if not, write to the Free
// Software Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
// --------------------------------------------------------------------------------------------------------------------
namespace SonarLocalAnalyser

open System
open System.Runtime.InteropServices
open System.Security.Permissions
open System.Threading
open System.IO
open System.Diagnostics
open VSSonarPlugins
open ExtensionTypes
open CommonExtensions
open System.Diagnostics
open Microsoft.Build.Utilities
open SonarRestService
open ExtensionHelpers
open CommandExecutor
open SQPluginManager

type LanguageType =
   | SingleLang = 0
   | MultiLang = 1

[<ComVisible(false)>]
[<HostProtection(SecurityAction.LinkDemand, Synchronization = true, ExternalThreading = true)>]
type SonarLocalAnalyser(plugins : System.Collections.Generic.List<AnalysisPluginHolder>, restService : ISonarRestService, vsinter : IConfigurationHelper) =
    let completionEvent = new DelegateEvent<System.EventHandler>()
    let stdOutEvent = new DelegateEvent<System.EventHandler>()
    let jsonReports : System.Collections.Generic.List<String> = new System.Collections.Generic.List<String>()
    let localissues : System.Collections.Generic.List<Issue> = new System.Collections.Generic.List<Issue>()
    let mutable cachedProfiles : Map<string, Profile> = Map.empty
    let syncLock = new System.Object()
    let mutable exec : CommandExecutor = new CommandExecutor(null, int64(1000 * 60 * 60)) // TODO timeout to be passed as parameter

    let triggerException(x, msg : string, ex : Exception) = 
        let errorInExecution = new LocalAnalysisEventArgs("LA: ", "INVALID OPTIONS: " + msg, ex)
        completionEvent.Trigger([|x; errorInExecution|])

    let GetAPluginInListThatSupportSingleLanguage(project : Resource, conf : ISonarConfiguration) =              
        (List.ofSeq plugins) |> List.find (fun plugin -> plugin.Plugin.IsSupported(conf, project)) 

    let GetPluginThatSupportsResource(itemInView : VsProjectItem) =      
        if itemInView = null then        
            raise(new NoFileInViewException())

        let IsSupported(plugin : IAnalysisPlugin, item : VsProjectItem) = 
            let name = plugin.GetPluginDescription(vsinter).Name
            let isEnabled = vsinter.ReadOptionFromApplicationData(GlobalIds.PluginEnabledControlId, name)
            if String.IsNullOrEmpty(isEnabled) then
                plugin.IsSupported(item)
            else
                plugin.IsSupported(item) && isEnabled.Equals("true", StringComparison.CurrentCultureIgnoreCase)

        let elem = List.ofSeq plugins |> List.tryFind (fun plugin -> IsSupported(plugin.Plugin, itemInView)) 
        match elem with
        | Some value -> value
        | None -> null

    let GetExtensionThatSupportsThisProject(conf : ISonarConfiguration, project : Resource) = 
        let plugin = GetAPluginInListThatSupportSingleLanguage(project, conf)
        if plugin <> null then
            plugin.Plugin.GetLocalAnalysisExtension(conf)
        else
            null

    let GetExtensionThatSupportsThisFile(vsprojitem : VsProjectItem, conf:ISonarConfiguration) = 
        let plugin = GetPluginThatSupportsResource(vsprojitem)
        if plugin <> null then
            plugin.Plugin.GetLocalAnalysisExtension(conf)
        else
            null

    let GetQualityProfile(conf:ISonarConfiguration, project:Resource, itemInView:VsProjectItem) =
        if cachedProfiles.ContainsKey(project.Lang) then
            cachedProfiles.[project.Lang]
        else
            
            let profiles = restService.GetQualityProfilesForProject(conf, project.Key)
            let profileResource = restService.GetQualityProfile(conf, project.Key)

            if profileResource.Count > 0 then
                let enabledrules = restService.GetEnabledRulesInProfile(conf, project.Lang, profileResource.[0].Metrics.[0].Data)                            
                cachedProfiles <- cachedProfiles.Add(project.Lang, enabledrules.[0])
                enabledrules.[0]
            else
                if project.Lang = null then
                    let plugin = GetPluginThatSupportsResource(itemInView)
                    project.Lang <- plugin.Plugin.GetLanguageKey()

                let profile = profiles |> Seq.find (fun x -> x.Language = project.Lang)
                if profile <> null then
                    let enabledrules = restService.GetEnabledRulesInProfile(conf, project.Lang, profile.Name)                            
                    cachedProfiles <- cachedProfiles.Add(project.Lang, enabledrules.[0])
                    enabledrules.[0]
                else
                    null



    let GetDoubleParent(x, path : string) =
        if String.IsNullOrEmpty(path) then
            triggerException(x, "Path not set in options", new Exception("Path Give in Options was not defined, be sure configuration is correctly set"))
            ""
        else
            let data = Directory.GetParent(Directory.GetParent(path).ToString()).ToString()
            if not(Directory.Exists(data)) then
                triggerException(x, "DirectoryNotFound For Required Variable: " + data, new Exception("Directory Not Foud"))
            data

    let GetParent(x, path : string) =
        if String.IsNullOrEmpty(path) then
            triggerException(x, "Path not set in options", new Exception("Path Give in Options was not defined, be sure configuration is correctly set"))
            ""
        else
            let data = Directory.GetParent(path).ToString()
            if not(Directory.Exists(data)) then
                triggerException(x, "DirectoryNotFound For Required Variable: " + data, new Exception("Directory Not Foud"))
            data

    let GenerateCommonArgumentsForAnalysis(mode : AnalysisMode, version : double, conf : ISonarConfiguration, project : Resource, sonarProps : string []) =
        let builder = new CommandLineBuilder()
        // mandatory properties
        builder.AppendSwitchIfNotNull("-Dsonar.host.url=", conf.Hostname.Trim())
        if not(String.IsNullOrEmpty(conf.Username)) then
            builder.AppendSwitchIfNotNull("-Dsonar.login=", conf.Username)

        if not(String.IsNullOrEmpty(conf.Password)) then
            builder.AppendSwitchIfNotNull("-Dsonar.password=", conf.Password)

        builder.AppendSwitchIfNotNull("-Dsonar.projectKey=", project.Key)
        builder.AppendSwitchIfNotNull("-Dsonar.projectName=", project.Name)
        builder.AppendSwitchIfNotNull("-Dsonar.projectVersion=", project.Version)

        // mandatory project properties
        let solId = VsSonarUtils.SolutionGlobPropKey(project.Key)
        let optionsInDsk = vsinter.ReadAllAvailableOptionsInSettings(solId)

        let foundinconfingFile = sonarProps |> Seq.tryFind (fun c -> c.Contains("sonar.sources="))
        match foundinconfingFile with
        | None -> builder.AppendSwitchIfNotNull("-Dsonar.sources=", "")
        | Some x -> ()

        // optional project properties
        let foundinconfingFile = sonarProps |> Seq.tryFind (fun c -> c.Contains("sonar.sourceEncoding="))
        match foundinconfingFile with
        | None -> builder.AppendSwitchIfNotNull("-Dsonar.sourceEncoding=UTF-8", optionsInDsk.[GlobalIds.SourceEncodingKey])
        | Some x -> ()
        
        // analysis
        if version >= 4.0 && not(mode.Equals(AnalysisMode.Full)) then
            match mode with
            | AnalysisMode.Incremental -> builder.AppendSwitchIfNotNull("-Dsonar.analysis.mode=", "incremental")
                                          builder.AppendSwitchIfNotNull("-Dsonar.preview.excludePlugins=", vsinter.ReadOptionFromApplicationData(GlobalIds.GlobalPropsId, GlobalIds.ExcludedPluginsKey))
            | AnalysisMode.Preview -> builder.AppendSwitchIfNotNull("-Dsonar.analysis.mode=", "preview")
                                      builder.AppendSwitchIfNotNull("-Dsonar.preview.excludePlugins=", vsinter.ReadOptionFromApplicationData(GlobalIds.GlobalPropsId, GlobalIds.ExcludedPluginsKey))
            | _ -> ()

        elif version >= 3.4 && not(mode.Equals(AnalysisMode.Full)) then
            match mode with
            | AnalysisMode.Incremental -> raise (new InvalidOperationException("Analysis Method Not Available in this Version of SonarQube"))
            | AnalysisMode.Preview -> builder.AppendSwitchIfNotNull("-Dsonar.dryRun=", "true")
                                      builder.AppendSwitchIfNotNull("-Dsonar.dryRun.excludePlugins=", vsinter.ReadOptionFromApplicationData(GlobalIds.GlobalPropsId, GlobalIds.ExcludedPluginsKey))
            | _ -> ()
        elif not(mode.Equals(AnalysisMode.Full)) then
            raise (new InvalidOperationException("Analysis Method Not Available in this Version of SonarQube"))
                   
        builder.AppendSwitch("-X")                    

        builder

    let SetupEnvironment(x)= 
        let javaexec = vsinter.ReadOptionFromApplicationData(GlobalIds.GlobalPropsId, GlobalIds.JavaExecutableKey)
        let runnerexec = vsinter.ReadOptionFromApplicationData(GlobalIds.GlobalPropsId, GlobalIds.RunnerExecutableKey)
        let javahome = GetDoubleParent(x, javaexec)
        let sonarrunnerhome = GetDoubleParent(x, runnerexec)            
        let path = GetParent(x, javaexec) + ";" + GetParent(x, runnerexec) + ";" + Environment.GetEnvironmentVariable("PATH")
                   
        if not(Environment.GetEnvironmentVariable("JAVA_HOME") = null) then
            Environment.SetEnvironmentVariable("JAVA_HOME", "")

        if not(Environment.GetEnvironmentVariable("SONAR_RUNNER_HOME") = null) then
            Environment.SetEnvironmentVariable("SONAR_RUNNER_HOME", "")

        if not(Environment.GetEnvironmentVariable("PATH") = null) then
            Environment.SetEnvironmentVariable("PATH", "")

        Map.ofList [("PATH", path); ("JAVA_HOME", javahome); ("SONAR_RUNNER_HOME", sonarrunnerhome)]


    let monitor = new Object()
    let numberofFilesToAnalyse = ref 0;
      
    member val Abort = false with get, set
    member val ProjectRoot = "" with get, set
    member val Project = null : Resource with get, set
    member val Version = 0.0 with get, set
    member val Conf = null with get, set
    member val ExecutingThread = null with get, set
    member val CurrentAnalysisType = AnalysisMode.File with get, set
    member val RunningLanguageMethod = LanguageType.SingleLang with get, set
    member val ItemInView = null : VsProjectItem with get, set
    member val OnModifyFiles = false with get, set
    member val AnalysisIsRunning = false with get, set
    member val SonarRunnerPropsFile : string []  = null with get, set

    member x.ProcessExecutionEventComplete(e : EventArgs) =
        let data = e :?> LocalAnalysisEventArgs
        let message = new LocalAnalysisEventArgs("LA: ", data.ErrorMessage, null)
        completionEvent.Trigger([|x; message|])
          
    member x.ProcessOutputPluginData(e : EventArgs) = 
        let data = e :?> LocalAnalysisEventArgs
        let message = new LocalAnalysisEventArgs("LA:", data.ErrorMessage, null)
        stdOutEvent.Trigger([|x; message|])

    member x.ProcessOutputDataReceived(e : DataReceivedEventArgs) =
        try
            if e.Data <> null && not(x.Abort) then
                if e.Data.EndsWith(".json") then
                    let elems = e.Data.Split(' ');
                    jsonReports.Add(elems.[elems.Length-1])

                let isDebugOn = vsinter.ReadOptionFromApplicationData(GlobalIds.GlobalPropsId, GlobalIds.IsDebugAnalysisOnKey)
        
                if e.Data.Contains(" DEBUG - ") then
                    if isDebugOn.Equals("True") then
                        let message = new LocalAnalysisEventArgs("LA:", e.Data, null)
                        stdOutEvent.Trigger([|x; message|])
                else
                    let message = new LocalAnalysisEventArgs("LA:", e.Data, null)
                    stdOutEvent.Trigger([|x; message|])

                if e.Data.Contains("DEBUG - Populating index from") then
                    let message = new LocalAnalysisEventArgs("LA:", e.Data, null)
                    stdOutEvent.Trigger([|x; message|])
                    let separator = [| "Populating index from" |]
                    let mutable fileName = e.Data.Split(separator, StringSplitOptions.RemoveEmptyEntries).[1]

                    if fileName.Contains("abs=") then 
                        let separator = [| "abs=" |]
                        let absfileName = fileName.Split(separator, StringSplitOptions.RemoveEmptyEntries).[1].Trim()
                        fileName <- absfileName.TrimEnd(']')

                    if File.Exists(fileName) then
                        let vsprojitem = new VsProjectItem(Path.GetFileName(fileName), fileName, "", "", "", x.ProjectRoot)                    
                        let extension = GetExtensionThatSupportsThisFile(vsprojitem, x.Conf)
                        if extension <> null then
                            let plugin = GetPluginThatSupportsResource(vsprojitem)
                            let project = new Resource()
                            project.Date <- x.Project.Date
                            project.Lang <- x.Project.Lang
                            project.Id <- x.Project.Id
                            project.Key <- x.Project.Key
                            project.Lname <- x.Project.Lname
                            project.Version <- x.Project.Version
                            project.Name <- x.Project.Name
                            project.Qualifier <- x.Project.Qualifier
                            project.Scope <- x.Project.Scope

                            project.Lang <- plugin.Plugin.GetLanguageKey()
                            let issues = extension.ExecuteAnalysisOnFile(vsprojitem, GetQualityProfile(x.Conf, project, vsprojitem), project)
                            lock syncLock (
                                fun () -> 
                                    localissues.AddRange(issues)
                                )
            with
            | ex -> ()


    member x.generateCommandArgs(mode : AnalysisMode, version : double, conf : ISonarConfiguration, project : Resource) =
        GenerateCommonArgumentsForAnalysis(mode, version, conf, project, x.SonarRunnerPropsFile).ToString()        
                            
    member x.RunPreviewBuild() =
        try
            jsonReports.Clear()
            localissues.Clear()
            let executable = vsinter.ReadOptionFromApplicationData(GlobalIds.GlobalPropsId, GlobalIds.RunnerExecutableKey)

            let incrementalArgs = x.generateCommandArgs(AnalysisMode.Preview, x.Version, x.Conf, x.Project)
            if not(String.IsNullOrEmpty(x.Conf.Password)) then
                let message = sprintf "[%s] %s %s" x.ProjectRoot executable (incrementalArgs.Replace(x.Conf.Password, "xxxxx"))
                let commandData = new LocalAnalysisEventArgs("CMD: ", message, null)
                stdOutEvent.Trigger([|x; commandData|])               
        
            let errorCode = (exec :> ICommandExecutor).ExecuteCommand(executable, incrementalArgs, SetupEnvironment x, x.ProcessOutputDataReceived, x.ProcessOutputDataReceived, x.ProjectRoot)
            if errorCode > 0 then
                let errorInExecution = new LocalAnalysisEventArgs("LA", "Failed To Execute", new Exception("Error Code: " + errorCode.ToString()))
                completionEvent.Trigger([|x; errorInExecution|])               
            else
                completionEvent.Trigger([|x; null|])
        with
        | ex ->
            let errorInExecution = new LocalAnalysisEventArgs("LA", "Exception while executing", ex)
            completionEvent.Trigger([|x; errorInExecution|])

        x.AnalysisIsRunning <- false

    member x.RunFullBuild() =
        try
            jsonReports.Clear()
            localissues.Clear()
            let executable = vsinter.ReadOptionFromApplicationData(GlobalIds.GlobalPropsId, GlobalIds.RunnerExecutableKey)

            let incrementalArgs = x.generateCommandArgs(AnalysisMode.Full, x.Version, x.Conf, x.Project)

            if not(String.IsNullOrEmpty(x.Conf.Password)) then
                let message = sprintf "[%s] %s %s" x.ProjectRoot executable (incrementalArgs.Replace(x.Conf.Password, "xxxxx"))
                let commandData = new LocalAnalysisEventArgs("TODO SORT KEY", message, null)
                stdOutEvent.Trigger([|x; commandData|])
          
            let errorCode = (exec :> ICommandExecutor).ExecuteCommand(executable, incrementalArgs, SetupEnvironment x, x.ProcessOutputDataReceived, x.ProcessOutputDataReceived, x.ProjectRoot)
            if errorCode > 0 then
                let errorInExecution = new LocalAnalysisEventArgs("LA", "Failed To Execute", new Exception("Error Code: " + errorCode.ToString()))
                completionEvent.Trigger([|x; errorInExecution|])               
            else
                completionEvent.Trigger([|x; null|])
        with
        | ex ->
            let errorInExecution = new LocalAnalysisEventArgs("LA", "Exception while executing", ex)
            completionEvent.Trigger([|x; errorInExecution|])

        x.AnalysisIsRunning <- false

    member x.RunIncrementalBuild() = 
        try
            jsonReports.Clear()
            localissues.Clear()
            let executable = vsinter.ReadOptionFromApplicationData(GlobalIds.GlobalPropsId, GlobalIds.RunnerExecutableKey)            
            let incrementalArgs = x.generateCommandArgs(AnalysisMode.Incremental, x.Version, x.Conf, x.Project)

            if not(String.IsNullOrEmpty(x.Conf.Password)) then
                let message = sprintf "[%s] %s %s" x.ProjectRoot executable (incrementalArgs.Replace(x.Conf.Password, "xxxxx"))
                let commandData = new LocalAnalysisEventArgs("CMD: ", message, null)
                stdOutEvent.Trigger([|x; commandData|])         
         
            let errorCode = (exec :> ICommandExecutor).ExecuteCommand(executable, incrementalArgs, SetupEnvironment x, x.ProcessOutputDataReceived, x.ProcessOutputDataReceived, x.ProjectRoot)
            if errorCode > 0 then
                let errorInExecution = new LocalAnalysisEventArgs("LA", "Failed To Execute", new Exception("Error Code: " + errorCode.ToString()))
                completionEvent.Trigger([|x; errorInExecution|])               
            else
                completionEvent.Trigger([|x; null|])
        with
        | ex ->
            let errorInExecution = new LocalAnalysisEventArgs("LA", "Exception while executing", ex)
            completionEvent.Trigger([|x; errorInExecution|])

        x.AnalysisIsRunning <- false

    member x.RunFileAnalysisThread() = 

            let plugin = GetPluginThatSupportsResource(x.ItemInView)
            if plugin = null then
                let errorInExecution = new LocalAnalysisEventArgs("Local Analyser", "Cannot Run Analyis: Not Supported: ", new ResourceNotSupportedException())
                completionEvent.Trigger([|x; errorInExecution|])
                x.AnalysisIsRunning <- false
            else   
                let GetSourceFromServer() = 
                    try
                        let keyOfItem = (x :> ISonarLocalAnalyser).GetResourceKey(x.ItemInView, x.Project, x.Conf, true)
                        let source = restService.GetSourceForFileResource(x.Conf, keyOfItem)
                        VsSonarUtils.GetLinesFromSource(source, "\r\n")
                    with
                    | ex ->
                        try
                            let keyOfItem = (x :> ISonarLocalAnalyser).GetResourceKey(x.ItemInView, x.Project, x.Conf, false)
                            let source = restService.GetSourceForFileResource(x.Conf, keyOfItem)
                            VsSonarUtils.GetLinesFromSource(source, "\r\n")
                        with
                        | ex -> ""
               
                let extension = GetExtensionThatSupportsThisFile(x.ItemInView, x.Conf)
                extension.StdOutEvent.Add(x.ProcessOutputPluginData)
                extension.LocalAnalysisCompleted.Add(x.ProcessOutputPluginData)
                x.ExecutingThread <- extension.GetFileAnalyserThread(x.ItemInView, x.Project, GetQualityProfile(x.Conf, x.Project, x.ItemInView), GetSourceFromServer(), x.OnModifyFiles)            

                if x.ExecutingThread = null then
                    let errorInExecution = new LocalAnalysisEventArgs("Local Analyser", "Cannot Run Analyis: Not Supported: ", new ResourceNotSupportedException())
                    completionEvent.Trigger([|x; errorInExecution|])
                else
                    x.ExecutingThread.Start()
                    x.ExecutingThread.Join()

                    lock syncLock (
                        fun () -> 
                            localissues.AddRange(extension.GetIssues())
                        )

                    completionEvent.Trigger([|x; null|])

                x.AnalysisIsRunning <- false
                            
    member x.CancelExecution(thread : Thread) =
        if not(obj.ReferenceEquals(exec, null)) then
            (exec :> ICommandExecutor).CancelExecution |> ignore
        ()
                   
    member x.IsMultiLanguageAnalysis(res : Resource) =

        if plugins = null then
            raise(new NoPluginInstalledException())
        elif res = null then
            raise(new ProjectNotAssociatedException())
        elif String.IsNullOrEmpty(res.Lang) then
            true
        else
            false
           
    interface ISonarLocalAnalyser with
        [<CLIEvent>]
        member x.LocalAnalysisCompleted = completionEvent.Publish

        [<CLIEvent>]
        member x.StdOutEvent = stdOutEvent.Publish

        member x.GetIssuesInFile(conf : ISonarConfiguration, file:VsProjectItem) =
            let extension = GetExtensionThatSupportsThisFile(file, conf)
            if extension <> null then
                extension.GetIssues()
            else
                new System.Collections.Generic.List<Issue>()


        member x.GetIssues(conf : ISonarConfiguration, project : Resource) =
                                        
            let issues = new System.Collections.Generic.List<Issue>()

            let ParseReport(report : string) =
                if File.Exists(report) then
                    try
                        issues.AddRange(restService.ParseReportOfIssues(report))
                    with
                    | ex -> try
                                issues.AddRange(restService.ParseDryRunReportOfIssues(report));
                            with 
                            | ex -> try
                                        issues.AddRange(restService.ParseReportOfIssuesOld(report));
                                    with
                                    | ex -> ()

            List.ofSeq jsonReports |> Seq.iter (fun m -> ParseReport(m))

            let GetIssuesFromPlugins(plug : IAnalysisPlugin) = 
                try
                    localissues.AddRange(plug.GetLocalAnalysisExtension(conf).GetSupportedIssues(issues))
                with
                | ex -> ()                        

            List.ofSeq plugins |> Seq.iter (fun m -> GetIssuesFromPlugins(m.Plugin))

            localissues

        member x.AnalyseFile(itemInView : VsProjectItem, project : Resource, onModifiedLinesOnly : bool, version : double, conf : ISonarConfiguration) =
            x.CurrentAnalysisType <- AnalysisMode.File
            x.Conf <- conf
            x.Version <- version
            x.Abort <- false            
            x.Project <- project
            x.ItemInView <- itemInView
            x.OnModifyFiles <- onModifiedLinesOnly

            if plugins = null then
                raise(new ResourceNotSupportedException())

            if itemInView = null then
                raise(new NoFileInViewException())

            if GetPluginThatSupportsResource(x.ItemInView) = null then
                raise(new ResourceNotSupportedException())
            
            x.AnalysisIsRunning <- true
            localissues.Clear()
            (new Thread(new ThreadStart(x.RunFileAnalysisThread))).Start()

        member x.RunIncrementalAnalysis(projectRoot : string, project : Resource, version : double, conf : ISonarConfiguration) =
            x.CurrentAnalysisType <- AnalysisMode.Incremental
            x.ProjectRoot <- projectRoot
            x.Conf <- conf
            x.Version <- version
            x.Abort <- false            
            x.Project <- project

            x.SonarRunnerPropsFile <- File.ReadAllLines(Path.Combine(x.ProjectRoot, "sonar-project.properties"))

            if not(String.IsNullOrEmpty(project.Lang)) then
                x.RunningLanguageMethod <- LanguageType.SingleLang
            else
                x.RunningLanguageMethod <- LanguageType.MultiLang

            localissues.Clear()
            x.AnalysisIsRunning <- true
            (new Thread(new ThreadStart(x.RunIncrementalBuild))).Start()
            
        member x.RunPreviewAnalysis(projectRoot : string, project : Resource, version : double, conf : ISonarConfiguration) =
            x.CurrentAnalysisType <- AnalysisMode.Preview
            x.ProjectRoot <- projectRoot
            x.Project <- project
            x.Conf <- conf
            x.Version <- version
            x.Abort <- false
            x.SonarRunnerPropsFile <- File.ReadAllLines(Path.Combine(x.ProjectRoot, "sonar-project.properties"))

            if not(String.IsNullOrEmpty(project.Lang)) then
                x.RunningLanguageMethod <- LanguageType.SingleLang
            else
                x.RunningLanguageMethod <- LanguageType.MultiLang

            localissues.Clear()
            x.AnalysisIsRunning <- true
            (new Thread(new ThreadStart(x.RunPreviewBuild))).Start()
                                
        member x.RunFullAnalysis(projectRoot : string, project : Resource,  version : double, conf : ISonarConfiguration) =
            x.CurrentAnalysisType <- AnalysisMode.Full
            x.ProjectRoot <- projectRoot
            x.Project <- project
            x.Conf <- conf
            x.Version <- version
            x.Abort <- false
            x.SonarRunnerPropsFile <- File.ReadAllLines(Path.Combine(x.ProjectRoot, "sonar-project.properties"))

            if project.Lang = null then
                x.RunningLanguageMethod <- LanguageType.MultiLang

            localissues.Clear()
            x.AnalysisIsRunning <- true
            (new Thread(new ThreadStart(x.RunFullBuild))).Start()

        member x.StopAllExecution() =
            x.Abort <- true
            x.AnalysisIsRunning <- false
            if x.ExecutingThread = null || not(x.ExecutingThread.IsAlive) then
                ()
            else
                if not(obj.ReferenceEquals(exec, null)) then
                    try
                        (exec :> ICommandExecutor).CancelExecution |> ignore
                    with
                    | ex -> ()

        member x.IsExecuting() =
            x.AnalysisIsRunning
                
        member x.GetResourceKey(itemInView : VsProjectItem, associatedProject : Resource, conf : ISonarConfiguration, safeIsOn:bool) = 
            if plugins = null then
                raise(new ResourceNotSupportedException())

            if itemInView = null then
                raise(new NoFileInViewException())

            let plugin = GetPluginThatSupportsResource(itemInView)

            plugin.Plugin.GetResourceKey(itemInView, associatedProject.Key, safeIsOn)