using Amazon.SQS.Model;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TennisBookings.ScoreProcessor.BackgroundServices;
using TennisBookings.ScoreProcessor.Sqs;

namespace TennisBookings.ScoreProcessor.Tests;

public class QueueReadingServiceTests
{
	[Fact]
	public async Task ShouldSwallowExceptions_AndCompleteWriter()
	{
		// Arrange

		var sqsChannel = new Mock<ISqsMessageChannel>();

		var sqsMessageQueue = new Mock<ISqsMessageQueue>();
		sqsMessageQueue.Setup(x => x
			.ReceiveMessageAsync(
				It.IsAny<ReceiveMessageRequest>(),
				It.IsAny<CancellationToken>()))
			.ThrowsAsync(new Exception("My exception"));

		using var sut = new QueueReadingService(
			NullLogger<QueueReadingService>.Instance,
			sqsMessageQueue.Object,
			Options.Create(new AwsServicesConfiguration
			{
				ScoresQueueUrl = "https://www.example.com"
			}),
			sqsChannel.Object);

		// Act

		await sut.StartAsync(default);

		// Assert

		sqsChannel.Verify(x => x.TryCompleteWriter(null), Times.Once);
	}

	[Fact]
	public async Task ShouldStopWithoutException_WhenCancelled()
	{
		var sqsChannel = new SqsMessageChannel(NullLogger<SqsMessageChannel>.Instance);

		var sqsMessageQueue = new Mock<ISqsMessageQueue>();
		sqsMessageQueue.Setup(x => x
			.ReceiveMessageAsync(
				It.IsAny<ReceiveMessageRequest>(),
				It.IsAny<CancellationToken>()))
			.ReturnsAsync(new ReceiveMessageResponse
			{
				HttpStatusCode = System.Net.HttpStatusCode.OK,
				Messages = new List<Message>()
			});

		var sut = new QueueReadingService(
			NullLogger<QueueReadingService>.Instance,
			sqsMessageQueue.Object,
			Options.Create(new AwsServicesConfiguration
			{
				ScoresQueueUrl = "https://www.example.com"
			}),
			sqsChannel);

		await sut.StartAsync(default);

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

		Func<Task> act = async () => { await sut.StopAsync(cts.Token); };

		await act.Should().NotThrowAsync();
	}
}
