﻿using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Crowdin.Api;
using Crowdin.Api.Typed;
using Microsoft.Extensions.Configuration;
using Moq;
using Newtonsoft.Json.Linq;
using Overcrowdin;
using Xunit;

namespace OvercrowdinTests
{
	public class AddCommandTests : CrowdinApiTestBase
	{
		[Fact]
		public async void AddCommandWithCommandLine()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			_mockConfig.Setup(config => config["api_key_env"]).Returns(apiKeyEnvVar);
			_mockConfig.Setup(config => config["project_identifier"]).Returns(projectId);
			// Only setup the expected call to AddFile (any calls without the expected file params will return null)
			_mockClient.Setup(x => x.AddFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<AddFileParameters>(fp => fp.Files.ContainsKey(inputFileName))))
				.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));
			var gate = new AutoResetEvent(false);
			var result = await AddCommand.AddFilesToCrowdin(_mockConfig.Object, new AddCommand.Options { Files = new[] { inputFileName } },
				gate, mockFileSystem);
			gate.WaitOne();
			_mockClient.Verify();
			Assert.Equal(0, result);
		}

		[Fact]
		public async void AddCommandWithConfigFile()
		{
			var mockFileSystem = new MockFileSystem();
			const string inputFileName = "test.txt";
			const string apiKeyEnvVar = "KEYEXISTS";
			const string projectId = "testcrowdinproject";
			Environment.SetEnvironmentVariable(apiKeyEnvVar, "fakecrowdinapikey");
			mockFileSystem.File.WriteAllText(inputFileName, "mock contents");
			dynamic configJson = new JObject();

			configJson.project_id = projectId;
			configJson.api_key_env = apiKeyEnvVar;
			configJson.base_path = ".";
			dynamic file = new JObject();
			file.source = inputFileName;
			var files = new JArray {file};
			configJson.files = files;

			using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(configJson.ToString())))
			{
				var configurationBuilder = new ConfigurationBuilder().AddNewtonsoftJsonStream(memStream).Build();

				// Only setup the expected call to AddFile (any calls without the expected file params will return null)
				_mockClient.Setup(x => x.AddFile(It.IsAny<string>(), It.IsAny<ProjectCredentials>(), It.Is<AddFileParameters>(fp => fp.Files.ContainsKey(inputFileName))))
					.Returns(Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));
				var gate = new AutoResetEvent(false);
				var result = await AddCommand.AddFilesToCrowdin(configurationBuilder, new AddCommand.Options(), gate, mockFileSystem);
				gate.WaitOne();
				_mockClient.Verify();
				Assert.Equal(0, result);
			}
		}
	}
}
