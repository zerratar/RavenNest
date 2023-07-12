namespace RavenNest.DataModels
{
    public partial class ServerSettings : Entity<ServerSettings>
    {
        private string name; public string Name { get => name; set => Set(ref name, value); }
        private string value; public string Value { get => value; set => Set(ref this.value, value); }
    }
}
