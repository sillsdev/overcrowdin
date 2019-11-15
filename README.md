## Overcrowdin - A Crowdin dotnet CLI

Crowdin is an amazing cloud based localization management system <a href="https://crowdin.com" target="_blank">Crowdin.com</a> which many projects are using for localization.

It provides a CLI in java (<a href="https://github.com/crowdin/crowdin-cli-2" target="_blank">https://github.com/crowdin/crowdin-cli-2</a>) but that adds dependencies that many projects do not want. This project provides an alternative in the form of a <a href="https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools" target="_blank">.NET Core Global Tool</a>. 

They also have a project under development for a .NET Client of the Crowdin API (<a href="https://github.com/crowdin/crowdin-dotnet-client" target="_blank">https://github.com/crowdin/crowdin-dotnet-client</a>) 

This project is built on top of that client. This project is not yet as fully featured as the official Crowdin client.

### Requirements

In order to install and use the dotnet version of the Crowdin cli you need .NET Core 2.1 which can be downloaded from <a href="https://dotnet.microsoft.com/download/dotnet-core/2.1" target="_blank">Microsoft's website</a>.

### Installation

Crowdin CLI can be installed using the following command:
```dotnet tool install -g overcrowdin```

### Usage

After installation ```overcrowdin``` will be available from the command prompt.

To see the program help run ```overcrowdin``` with no options.

### FAQ

Q: Why don't you save the API Key in the configuration file?

A: I don't want to make it easy to commit secrets into a github repository so I encourage the environment variable option that Crowdin supports in their CLI.
