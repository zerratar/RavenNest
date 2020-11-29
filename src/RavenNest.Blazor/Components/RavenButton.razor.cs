using Microsoft.AspNetCore.Components;
using System;

namespace RavenNest.Blazor.Components
{
    public partial class RavenButton
    {
        public event EventHandler OnClick;

        [Parameter]
        public RenderFragment ChildContent { get; set; }

        [Parameter]
        public string CssClass { get; set; }

        private void ClickEvent()
        {
            if (OnClick != null)
            {
                OnClick.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
