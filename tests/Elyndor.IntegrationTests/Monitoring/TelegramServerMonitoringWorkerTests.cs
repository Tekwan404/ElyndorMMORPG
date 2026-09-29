using Elyndor.Server.Administration;
using Elyndor.Server.Monitoring;

namespace Elyndor.IntegrationTests.Monitoring;

public sealed class TelegramServerMonitoringWorkerTests
{
    [Fact]
    public void DefaultReportCadenceIsHourly()
    {
        Assert.Equal(60, new TelegramAdminOptions().ReportIntervalMinutes);
    }

    [Fact]
    public void HealthyEmptyServerSkipsRoutineReport()
    {
        Assert.False(TelegramServerMonitoringWorker.ShouldSendReport(
            0,
            databaseHealthy: true,
            previousState: null,
            currentState: "🟢 OK"));

        Assert.False(TelegramServerMonitoringWorker.ShouldSendReport(
            0,
            databaseHealthy: true,
            previousState: "🟢 OK",
            currentState: "🟢 OK"));
    }

    [Fact]
    public void OnlinePlayersKeepRoutineReportEnabled()
    {
        Assert.True(TelegramServerMonitoringWorker.ShouldSendReport(
            1,
            databaseHealthy: true,
            previousState: "🟢 OK",
            currentState: "🟢 OK"));
    }

    [Fact]
    public void EmptyServerStillSendsHealthAndStateAlerts()
    {
        Assert.True(TelegramServerMonitoringWorker.ShouldSendReport(
            0,
            databaseHealthy: false,
            previousState: "🟢 OK",
            currentState: "🔴 CRITICAL"));

        Assert.True(TelegramServerMonitoringWorker.ShouldSendReport(
            0,
            databaseHealthy: true,
            previousState: "🟢 OK",
            currentState: "🟡 WARNING"));

        Assert.True(TelegramServerMonitoringWorker.ShouldSendReport(
            0,
            databaseHealthy: true,
            previousState: "🟡 WARNING",
            currentState: "🟢 OK"));

        Assert.True(TelegramServerMonitoringWorker.ShouldSendReport(
            0,
            databaseHealthy: true,
            previousState: null,
            currentState: "🟡 WARNING"));
    }
}
