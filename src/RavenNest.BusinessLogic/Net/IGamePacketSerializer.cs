namespace RavenNest.BusinessLogic.Net
{
    public interface IGamePacketSerializer
    {
        byte[] Serialize(GamePacket packet);
        GamePacket Deserialize(byte[] data);
        GamePacket Deserialize(byte[] data, int length);

    }

}
