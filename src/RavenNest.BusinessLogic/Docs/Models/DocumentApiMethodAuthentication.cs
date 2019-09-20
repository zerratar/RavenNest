namespace RavenNest.BusinessLogic.Docs.Models
{
    public class DocumentApiMethodAuthentication
    {
        public DocumentApiMethodAuthentication(bool requiresTwitch, bool requiresAuth, bool requiresSession, bool requiresAdmin)
        {
            RequiresTwitch = requiresTwitch;
            this.RequiresAuth = requiresAuth;
            this.RequiresSession = requiresSession;
            this.RequiresAdmin = requiresAdmin;
        }

        public bool RequiresTwitch { get; }
        public bool RequiresAuth { get; }
        public bool RequiresSession { get; }
        public bool RequiresAdmin { get; }
    }
}