namespace MyRemoteControll.Agent.Hosting;

/// <summary>Leva avisos das features até a bandeja (via <see cref="AgentRuntime.NoticeRaised"/>).</summary>
internal sealed class AgentNotifications(Action<AgentNotice> raise)
{
    public void Raise(string title, string message) => raise(new AgentNotice(title, message));
}
