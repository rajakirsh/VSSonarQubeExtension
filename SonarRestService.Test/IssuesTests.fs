﻿namespace SonarRestService.Test

open NUnit.Framework
open FsUnit
open SonarRestService
open Foq
open System.IO

open ExtensionTypes

type IssuesTests() =

    [<Test>]
    member test.``Should Get Corret Number of Issues In Component`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "jocs1", "jocs1")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(any(), any()) @>).Returns(File.ReadAllText("testdata/issuessearchbycomponent.txt"))
                .Create()
        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).GetIssuesInResource(conf, "groupid:projectid:directory/file.cpp")
        issues.Count |> should equal 59

    [<Test>]
    member test.``Should Get Corret Number of Issues In Component Using the old Format`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "jocs1", "jocs1")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/violations?resource=groupid:projectid:directory/file.cpp") @>).Returns(File.ReadAllText("testdata/vilationsReply.txt"))
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/reviews?resources=groupid:projectid:directory/file.cpp") @>).Returns(File.ReadAllText("testdata/reviewByUser.txt"))
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/issues/search?components=groupid:projectid:directory/file.cpp") @>).Returns(File.ReadAllText("testdata/NonExistentPage.xml"))
                .Create()
        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).GetIssuesInResource(conf, "groupid:projectid:directory/file.cpp")
        issues.Count |> should equal 30
  
    [<Test>]
    member test.``Should Get Issues in Project by user`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "jocs1", "jocs1")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(any(), any()) @>).Returns(File.ReadAllText("testdata/issuesByUser.txt"))
                .Create()

        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).GetIssuesByAssigneeInProject(conf, "groupid:projectid:directory/file.cpp", "jocs1")
        issues.Count |> should equal 2

    [<Test>]
    member test.``Should Get Issues In Project by user using old format`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "login1", "password")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/reviews?projects=groupid:projectid&assignees=login1") @>).Returns(File.ReadAllText("testdata/reviewByUser.txt"))
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/issues/search?componentRoots=groupid:projectid&assignees=login1") @>).Returns(File.ReadAllText("testdata/NonExistentPage.xml"))
                .Create()

        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).GetIssuesByAssigneeInProject(conf, "groupid:projectid", "login1")
        issues.Count |> should equal 5

    [<Test>]
    member test.``Should Get All Issues by user`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "jocs1", "jocs1")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(any(), any()) @>).Returns(File.ReadAllText("testdata/issuesByUser.txt"))
                .Create()

        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).GetAllIssuesByAssignee(conf, "jocs1")
        issues.Count |> should equal 2

    [<Test>]
    member test.``Should Get All Issues by user using old format`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "login1", "password")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/reviews?assignees=login1") @>).Returns(File.ReadAllText("testdata/reviewByUser.txt"))
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/issues/search?assignees=login1") @>).Returns(File.ReadAllText("testdata/NonExistentPage.xml"))
                .Create()

        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).GetAllIssuesByAssignee(conf, "login1")
        issues.Count |> should equal 5

    [<Test>]
    member test.``Should Get All Issues In Project`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "jocs1", "jocs1")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(any(), any()) @>).Returns(File.ReadAllText("testdata/issuessearchbycomponent.txt"))
                .Create()

        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).GetIssuesForProjects(conf, "project:id")
        issues.Count |> should equal 59

    [<Test>]
    member test.``Should Get All Issues In Project using old format`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "login1", "password")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/reviews?projects=project:id") @>).Returns(File.ReadAllText("testdata/reviewByUser.txt"))
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/violations?resource=project:id&depth=-1") @>).Returns(File.ReadAllText("testdata/vilationsReply.txt"))               
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/issues/search?componentRoots=project:id") @>).Returns(File.ReadAllText("testdata/NonExistentPage.xml"))
                .Create()

        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).GetIssuesForProjects(conf, "project:id")
        issues.Count |> should equal 30

    [<Test>]
    member test.``Should Get All Violations As Issues In Project using old format`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "login1", "password")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/violations?resource=filename") @>).Returns(File.ReadAllText("testdata/vilationsReply.txt"))
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/reviews?resources=filename") @>).Returns(File.ReadAllText("testdata/reviewByUser.txt"))
                .Setup(fun x -> <@ x.HttpSonarGetRequest(conf, "/api/issues/search?components=filename") @>).Returns(File.ReadAllText("testdata/NonExistentPage.xml"))
                .Create()

        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).GetIssuesInResource(conf, "filename")
        issues.Count |> should equal 30

    [<Test>]
    member test.``Should Parse Dry Run Reports Before 4.0`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "jocs1", "jocs1")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(any(), any()) @>).Returns(File.ReadAllText("testdata/dryRunReport.txt"))
                .Create()

        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).ParseReportOfIssuesOld("testdata/dryRunReport.txt")
        issues.Count |> should equal 10


    [<Test>]
    member test.``Should Parse Dry Run Reports Before 3.7`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "jocs1", "jocs1")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(any(), any()) @>).Returns(File.ReadAllText("testdata/dryrunissues.txt"))
                .Create()

        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).ParseDryRunReportOfIssues("testdata/dryrunissues.txt")
        issues.Count |> should equal 10

    [<Test>]
    member test.``Should Parse issue with sonar 4.2`` () =
        let conf = ConnectionConfiguration("http://localhost:9000", "jocs1", "jocs1")

        let mockHttpReq =
            Mock<IHttpSonarConnector>()
                .Setup(fun x -> <@ x.HttpSonarGetRequest(any(), any()) @>).Returns(File.ReadAllText("testdata/incrementalrun.txt"))
                .Create()

        let service = SonarRestService(mockHttpReq)
        let issues = (service :> ISonarRestService).ParseReportOfIssues("testdata/incrementalrun.txt")
        issues.Count |> should equal 1

