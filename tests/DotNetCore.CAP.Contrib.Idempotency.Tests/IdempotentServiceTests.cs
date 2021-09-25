﻿using DotNetCore.CAP.Contrib.Idempotency.Storage;
using DotNetCore.CAP.Contrib.Idempotency.Tests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace DotNetCore.CAP.Contrib.Idempotency.Tests
{
    public class IdempotentServiceTests : TestFixture
    {
        [Fact]
        public async Task ProcessMessageAsync_NewMessage_CallServiceProcessMessage()
        {
            // Arrange
            var mockService = new Mock<IConsumerService<TestMessage>>();
            var mockLogger = new Mock<ILogger<IdempotencyService<TestMessage, TestDbContext>>>();
            var mockStorageHelper = new Mock<IStorageHelper>();

            var service = new IdempotencyService<TestMessage, TestDbContext>(
                Context,
                mockService.Object,
                mockStorageHelper.Object,
                mockLogger.Object);
            var message = new TestMessage("message1", "group1");

            // Act
            await service.ProcessMessageAsync(message);

            // Assert
            mockService.Verify(x => x.ProcessMessageAsync(message), Times.Once);
        }

        [Fact]
        public async Task ProcessMessageAsync_SameMessageIdDifferentGroup_CallServiceProcessMessage()
        {
            // Arrange
            var mockService = new Mock<IConsumerService<TestMessage>>();
            var mockLogger = new Mock<ILogger<IdempotencyService<TestMessage, TestDbContext>>>();
            var mockStorageHelper = new Mock<IStorageHelper>();
            Context.Messages.Add(new MessageTracking("message1", "group1"));
            await Context.SaveChangesAsync();

            var service = new IdempotencyService<TestMessage, TestDbContext>(
                Context,
                mockService.Object,
                mockStorageHelper.Object,
                mockLogger.Object);
            var message = new TestMessage("message1", "group2");

            // Act
            await service.ProcessMessageAsync(message);

            // Assert
            mockService.Verify(x => x.ProcessMessageAsync(message), Times.Once);
        }

        [Fact]
        public async Task ProcessMessageAsync_NewMessage_SaveMessage()
        {
            // Arrange
            var mockService = new Mock<IConsumerService<TestMessage>>();
            mockService
                .Setup(x => x.ProcessMessageAsync(It.IsAny<TestMessage>()))
                .Callback(() => Context.SaveChanges());
            var mockLogger = new Mock<ILogger<IdempotencyService<TestMessage, TestDbContext>>>();
            var mockStorageHelper = new Mock<IStorageHelper>();

            var service = new IdempotencyService<TestMessage, TestDbContext>(
                Context,
                mockService.Object,
                mockStorageHelper.Object,
                mockLogger.Object);
            var message = new TestMessage("message1", "group1");

            // Act
            await service.ProcessMessageAsync(message);

            // Assert
            var storedMessage = await Context.Messages.AsNoTracking().SingleOrDefaultAsync();
            storedMessage.Id.Should().Be(message.MessageId);
            storedMessage.Type.Should().Be(message.MessageGroup);
        }

        [Fact]
        public async Task ProcessMessageAsync_RepeatedMessage_DontCallServiceProcessMessage()
        {
            // Arrange
            var mockService = new Mock<IConsumerService<TestMessage>>();
            var mockLogger = new Mock<ILogger<IdempotencyService<TestMessage, TestDbContext>>>();
            var mockStorageHelper = new Mock<IStorageHelper>();
            Context.Messages.Add(new MessageTracking("message1", "group1"));
            await Context.SaveChangesAsync();

            var service = new IdempotencyService<TestMessage, TestDbContext>(
                Context,
                mockService.Object,
                mockStorageHelper.Object,
                mockLogger.Object);
            var message = new TestMessage("message1", "group1");

            // Act
            await service.ProcessMessageAsync(message);

            // Assert
            mockService.Verify(x => x.ProcessMessageAsync(message), Times.Never);
            mockLogger.VerifyLog(logger =>
                logger.LogInformation($"Message was processed already. Ignoring message1:group1."));
        }

        public record TestMessage : IMessage
        {
            public TestMessage(string messageId, string messageGroup)
            {
                MessageId = messageId;
                MessageGroup = messageGroup;
            }

            public string MessageId { get; set; }
            public string MessageGroup { get; set; }
        }
    }
}