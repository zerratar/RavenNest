using Microsoft.AspNetCore.Components;
using System;

namespace RavenNest.Blazor.Components
{
    public partial class RavenLink
    {
        [Parameter]
        public RenderFragment ChildContent { get; set; }

        [Parameter]
        public string Href { get; set; }

        [Parameter]
        public string CssClass { get; set; }
    }
}
