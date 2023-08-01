namespace RavenNest.DataModels
{
    public partial class Achievement : Entity<Achievement>
    {
        private string name; public string Name { get => name; set => Set(ref name, value); }
        private string description; public string Description { get => description; set => Set(ref description, value); }
    }
}
