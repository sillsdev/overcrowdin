using RichardSzalay.MockHttp;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OvercrowdinTests
{
	/// <summary>
	/// Subclass of MockHttpMessageHandler that logs requests to the debug output. Mismatched requests can be a pain to debug.
	/// Usage: replace instances of MockHttpMessageHandler with MockHttpMessageHandlerDebugger in test code.
	/// </summary>
	internal class MockHttpMessageHandlerDebugger(BackendDefinitionBehavior backendDefinitionBehavior = BackendDefinitionBehavior.NoExpectations)
		: MockHttpMessageHandler(backendDefinitionBehavior)
	{
		/// <remarks>
		/// The base class method is not virtual, so we can't create a drop-in replacement class. Rather than calling this method, we can set a breakpoint
		/// in the base class method to log the expectations as they are added.
		/// </remarks>>
		public void AddAndLogRequestExpectation(IMockedRequest handler)
		{
			if (handler is IEnumerable<IMockedRequestMatcher> matchers)
			{
				Debug.WriteLine($@"Expecting Request: {string.Join(" ; ", matchers)}");
			}
			else
			{
				Debug.WriteLine($@"Expecting Request handled by {handler.GetType()}");
			}

			AddRequestExpectation(handler);
		}

		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Debug.WriteLine($@"Http{request.Method}Request: {request.RequestUri}");
			if (request.Content is StringContent content)
			{
				content.ReadAsStringAsync(cancellationToken).ContinueWith(t =>
				{
					Debug.WriteLine(t.Result);
				}, cancellationToken).Wait(cancellationToken);
			}
			else if (request.Content != null)
			{
				Debug.WriteLine($"\twith {request.Content.GetType()}");
			}

			return base.SendAsync(request, cancellationToken);
		}
	}
}
