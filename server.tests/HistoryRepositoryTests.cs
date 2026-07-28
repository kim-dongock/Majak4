using MajakServer.Models.Protocol;
using MajakServer.Repositories.MySQL;
using Moq;

namespace MajakServer.Tests;

public class HistoryRepositoryTests
{
    [Fact]
    public async Task InsertGameMoneyHistAsync_MissingTransactionCode_DoesNotWriteLog()
    {
        var log = CreateLogRepositoryMock();
        var repository = new HistoryRepository(
            log.Object,
            _ => Task.FromResult<TransactionCodeMetadata?>(null));

        await repository.InsertGameMoneyHistAsync("member", "UNKNOWN", 10, 20, 30, "127.0.0.1");

        log.Verify(repository => repository.InsertGameMoneyHistAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
            It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task InsertGameMoneyHistAsync_ExistingTransactionCode_WritesResolvedMetadata()
    {
        var log = CreateLogRepositoryMock();
        var repository = new HistoryRepository(
            log.Object,
            _ => Task.FromResult<TransactionCodeMetadata?>(
                new TransactionCodeMetadata("大会報酬", "MAJAK_EVENT", true)));

        await repository.InsertGameMoneyHistAsync("member", "JM00214", 10, 20, 30, "127.0.0.1");

        log.Verify(repository => repository.InsertGameMoneyHistAsync(
            "member", "JM00214", 10, 20, 30, "127.0.0.1",
            "大会報酬", null, "MAJAK_EVENT", true), Times.Once);
    }

    [Fact]
    public async Task InsertGameMoneyHistAsync_HistoryDisabled_WritesInvalidLogRow()
    {
        var log = CreateLogRepositoryMock();
        var repository = new HistoryRepository(
            log.Object,
            _ => Task.FromResult<TransactionCodeMetadata?>(
                new TransactionCodeMetadata("無効履歴", "MAJAK2", false)));

        await repository.InsertGameMoneyHistAsync("member", "JM00999", 10, 20, 30, "127.0.0.1");

        log.Verify(repository => repository.InsertGameMoneyHistAsync(
            "member", "JM00999", 10, 20, 30, "127.0.0.1",
            "無効履歴", null, "MAJAK2", false), Times.Once);
    }

    [Fact]
    public void CreateMetadata_UsesLegacyColumnsAndFallbacks()
    {
        var resolved = TransactionCodeMetadataResolver.CreateMetadata(
            "JM00070", "ログタイトル", "MAJAK_SPECIAL", false);
        var fallback = TransactionCodeMetadataResolver.CreateMetadata(
            "JM00071", null, null, true);

        Assert.Equal(new TransactionCodeMetadata("ログタイトル", "MAJAK_SPECIAL", false), resolved);
        Assert.Equal(new TransactionCodeMetadata("JM00071", GameConst.ServiceId, true), fallback);
    }

    private static Mock<LogRepository> CreateLogRepositoryMock()
    {
        var log = new Mock<LogRepository>(MockBehavior.Loose, (LogDbContext)null!);
        log.Setup(repository => repository.InsertGameMoneyHistAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>(),
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        return log;
    }
}