## Overcrowdin - A Crowdin dotnet CLI

<a href="https://crowdin.com" target="_blank">Crowdin</a> is an amazing, cloud-based localization management system which many projects are using for localization.
It provides a CLI in Java (<a href="https://github.com/crowdin/crowdin-cli-2" target="_blank">https://github.com/crowdin/crowdin-cli-2</a>), but that adds dependencies that many projects do not want. This project provides an alternative in the form of a <a href="https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools" target="_blank">.NET Core Global Tool</a>. 

**Overcrowdin** is a dotnet CLI built on top of the Crowdin project <a href="https://github.com/crowdin/crowdin-dotnet-client" target="_blank">crowdin-dotnet-client</a>, which provides a .NET Api over the Crowdin REST API.

This project is not yet feature complete with the Crowdin client. It was built to meet some immediate project needs and could be enhanced.

### Requirements

* .NET Core 2.1 : To install and use Overcrowdin, you need .NET Core 2.1, which can be downloaded from <a href="https://dotnet.microsoft.com/download/dotnet-core/2.1" target="_blank">Microsoft's website</a>.

### Installation

Overcrowdin can be installed using the following command:
```dotnet tool install -g overcrowdin```

### Usage

After installation ```overcrowdin``` will be available from the command prompt.

To see the program help, run ```overcrowdin``` with no options.

### FAQ

Q: Why don't you save the API Key in the configuration file?

A: I don't want to make it easy to commit secrets into a github repository so I encourage the environment variable option that Crowdin supports in their CLI.

### Developing Overcrowdin

#### Status
![Build Status](<https://build.palaso.org/app/rest/builds/buildType:(id:Overcrowdin_OvercrowdinCi)/statusIcon>)
![Test coverage](<https://img.shields.io/badge/dynamic/xml?label=Test%20coverage&suffix=%&query=//property[@name=%22CodeCoverageS%22]/@value&url=https%3A%2F%2Fbuild.palaso.org%2Fapp%2Frest%2Fbuilds%2FbuildType%3A(id%3AOvercrowdin_OvercrowdinCi)%2Fstatistics%3Fguest%3D1&style=flat>)
[![GitHub](https://img.shields.io/github/license/sillsdev/overcrowdin)](https://github.com/sillsdev/overcrowdin/blob/master/LICENSE)


#### Developer Requirements
* .NET Core 2.1

#### Recommendations
* Visual Studio Community Edition 2017 or later

#### Cloning
```git clone https://github.com/sillsdev/overcrowdin```
And until there is a Crowdin.Api Nuget package
```
cd overcrowdin
git submodule update --init
```

Then you should be able to build the solution and run the unit tests
```
dotnet build
cd OvercrowdinTests
dotnet test
```