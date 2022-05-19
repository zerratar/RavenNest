using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenNest.BusinessLogic.Extended
{
    public class WebsiteAdminPlayer : WebsitePlayer
    {
        public string PasswordHash { get; set; }
        public DateTime Created { get; set; }
        public string SessionName { get; set; }
    }
}
