using System;

namespace RavenNest.Blazor.Discord.Models
{
    public record CharacterList(CharacterInfo[] Characters, string ErrorMessage);

    public record CharacterInfo(Guid Id, int Index, string Name, string Alias, string Stream, string Training, Stats[] Stats);

    public record Stats(string Name, int Level);
}
