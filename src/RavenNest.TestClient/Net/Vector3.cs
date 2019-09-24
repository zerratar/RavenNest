namespace RavenNest.TestClient
{
    public struct Vector3
    {
        public float x;
        public float y;
        public float z;

        public float magnitude => x * x + y * y + z * z;
    }
}
