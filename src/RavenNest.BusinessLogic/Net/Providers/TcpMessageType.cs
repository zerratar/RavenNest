namespace RavenNest.BusinessLogic.Net
{
    /// <summary>
    /// A “typed” packet header so we don’t do repeated TryDeserialize for each message type.
    /// </summary>
    public enum TcpMessageType : byte
    {
        None = 0,
        AuthenticationRequest,
        SaveExperienceRequest,
        SaveStateRequest,
        GameStateRequest,

        // new partial updates.
        PlayerUpdatesBatch
    }
}
