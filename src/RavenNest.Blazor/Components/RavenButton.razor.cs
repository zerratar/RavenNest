using Microsoft.AspNetCore.Components;
using System;

namespace RavenNest.Blazor.Components
{
    public partial class RavenButton
    {
        [Parameter]
        public EventCallback<RavenButton> OnClick { get; set; }

        [Parameter]
        public string NavigateTo { get; set; }

        [Parameter]
        public RenderFragment ChildContent { get; set; }

        [Parameter]
        public string CssClass { get; set; }

        [Parameter]
        public string Type { get; set; }
        private void ClickEvent()
        {
            OnClick.InvokeAsync(this);
            if (!string.IsNullOrEmpty(NavigateTo))
            {
                NavigationManager.NavigateTo(NavigateTo);
            }
        }

        protected override void OnInitialized()
        {
            if (string.IsNullOrEmpty(Type))
                Type = "submit";

            base.OnInitialized();
        }
    }
}
