using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RavenNest.BusinessLogic.Extended;
using RavenNest.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    public partial class PlayerCustomization
    {
        [Parameter]
        public WebsitePlayer Player { get; set; }

        [Parameter]
        public bool CanManage { get; set; }

        private static readonly (string Key, string Label, string Icon)[] AllTabs =
        {
            ("body", "Body", "fa-child"),
            ("face", "Face", "fa-smile"),
            ("hair", "Hair", "fa-cut"),
            ("beard", "Beard", "fa-user-tie"),
        };

        // a few to start from; any colour is a click on the dropper away
        private static readonly string[] SkinTones =
        {
            "#F2D3C2", "#E8C0A8", "#D6B8AE", "#E8A584", "#FAA276", "#C68863", "#A86B4A", "#7C4A32", "#5A3424", "#40251E",
            "#9DB07A", "#7E9CC2", "#B9B3C9",
        };
        private static readonly string[] HairColors =
        {
            "#0E0B0A", "#3C2823", "#6B4226", "#A0461E", "#C52A4A", "#A8912A", "#E0C068", "#595959", "#D9D4CC",
            "#0D9BB9", "#4A9F55", "#7B4CC2", "#DF62C7",
        };
        private static readonly string[] EyeColors =
        {
            "#000000", "#3B2A1E", "#6E4B2A", "#2F6FB5", "#57A4D9", "#3D8A4A", "#8A8F99", "#C9A227", "#C62828", "#7E3FBF",
        };
        private static readonly string[] PaintColors =
        {
            "#000000", "#E8E4DA", "#8A1C1C", "#C9A227", "#1C3F8A", "#2E6B2E", "#6A1B9A", "#0D9BB9",
        };

        private SyntyAppearance look;
        private SyntyAppearance saved;
        private bool notAllowed;
        private string tab = "face";
        private string view = "body";
        private string mode;
        private string message;
        private bool messageIsBad;
        private bool saving;
        private string loadError;

        private ElementReference root;
        private ElementReference canvasHost;
        private IJSObjectReference module;
        private IJSObjectReference designer;
        private string shown;
        private string thumbsFor;
        private Guid? loadedFor;

        private bool Female => look?.Gender == Gender.Female;

        private (string Key, string Label, string Icon)[] Tabs => Female ? AllTabs.Where(t => t.Key != "beard").ToArray() : AllTabs;

        private bool Dirty => look != null && saved != null && Key(look) != Key(saved);

        protected override void OnParametersSet()
        {
            if (Player == null || loadedFor == Player.Id) return;
            loadedFor = Player.Id;
            look = LookService.GetLook(Player.Id);
            notAllowed = look == null;
            if (look == null) return;
            Tidy(look);
            saved = Clone(look);
            message = null;
            shown = null;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (look == null) return;
            try
            {
                if (designer == null)
                {
                    module ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/rf-character-designer.js");
                    designer = await module.InvokeAsync<IJSObjectReference>("create", canvasHost, new { });
                    mode = await designer.InvokeAsync<string>("getMode");
                    await designer.InvokeVoidAsync("listen", root);
                    StateHasChanged();
                }
                var key = Key(look);
                if (key != shown)
                {
                    shown = key;
                    await designer.InvokeVoidAsync("show", look);
                }
                var thumbs = key + "|" + tab;
                if (thumbs != thumbsFor)
                {
                    thumbsFor = thumbs;
                    await designer.InvokeVoidAsync("paintThumbs", root, look);
                }
            }
            catch (JSDisconnectedException)
            {
            }
            catch (JSException e)
            {
                loadError = "The character could not be shown: " + e.Message;
                StateHasChanged();
            }
        }

        private async Task Focus(string to)
        {
            view = to;
            if (designer != null) await designer.InvokeVoidAsync("focus", to);
        }

        private async Task Open(string key)
        {
            tab = key;
            // the face's parts are best seen up close, the body whole
            var want = key == "body" ? "body" : "face";
            if (want != view) await Focus(want);
        }

        private void Pick(Action<int> pick, int value)
        {
            pick(value);
            message = null;
        }

        private void PickColor(Action<string> pick, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            pick(value.ToUpperInvariant());
            message = null;
        }

        private void SetGender(Gender gender)
        {
            if (look.Gender == gender) return;
            look.Gender = gender;
            Tidy(look);
            if (gender == Gender.Female && tab == "beard") tab = "face";
            message = null;
        }

        private void Randomize()
        {
            var r = Random.Shared;
            var female = Female;
            look.Head = r.Next(female ? Services.CharacterLookService.FemaleHeads : Services.CharacterLookService.MaleHeads);
            look.Hair = r.Next(10) == 0 ? -1 : r.Next(Services.CharacterLookService.Hairs);
            look.Eyebrows = r.Next(female ? Services.CharacterLookService.FemaleEyebrows : Services.CharacterLookService.MaleEyebrows);
            look.FacialHair = female || r.Next(4) == 0 ? -1 : r.Next(Services.CharacterLookService.MaleBeards);
            look.SkinColor = SkinTones[r.Next(SkinTones.Length)];
            look.BeardColor = look.HairColor = HairColors[r.Next(HairColors.Length)];
            look.EyeColor = EyeColors[r.Next(EyeColors.Length)];
            look.WarPaintColor = PaintColors[r.Next(PaintColors.Length)];
            look.StubbleColor = r.Next(2) == 0 ? look.BeardColor : look.SkinColor;
            message = null;
        }

        private void Undo()
        {
            look = Clone(saved);
            message = null;
        }

        private async Task Save()
        {
            if (saving || !Dirty) return;
            saving = true;
            message = null;
            try
            {
                var error = LookService.SaveLook(Player.Id, look);
                if (error == null)
                {
                    saved = Clone(look);
                    if (Player.Appearance != null) Copy(look, Player.Appearance);
                    Say("Saved. Your character looks like this in the game now.", false);
                    if (designer != null) await designer.InvokeVoidAsync("cheer");
                }
                else
                {
                    Say(error switch
                    {
                        "notsignedin" => "You are not signed in any more. Sign in and try again.",
                        "nocharacter" => "This character is not yours to change.",
                        "invalid" => "Something in this look is not in the game. Undo and try again.",
                        _ => "The look could not be saved. Try again in a moment.",
                    }, true);
                }
            }
            finally
            {
                saving = false;
            }
        }

        private void Say(string text, bool bad)
        {
            message = text;
            messageIsBad = bad;
        }

        // the look within what the game has for its body type
        private static void Tidy(SyntyAppearance a)
        {
            var female = a.Gender == Gender.Female;
            var heads = female ? Services.CharacterLookService.FemaleHeads : Services.CharacterLookService.MaleHeads;
            var brows = female ? Services.CharacterLookService.FemaleEyebrows : Services.CharacterLookService.MaleEyebrows;
            if (a.Head < 0 || a.Head >= heads) a.Head = Math.Clamp(a.Head, 0, heads - 1);
            if (a.Eyebrows >= brows) a.Eyebrows = brows - 1;
            if (a.Eyebrows < -1) a.Eyebrows = -1;
            if (a.Hair >= Services.CharacterLookService.Hairs || a.Hair < -1) a.Hair = 0;
            if (female) a.FacialHair = -1;
            else if (a.FacialHair >= Services.CharacterLookService.MaleBeards || a.FacialHair < -1) a.FacialHair = -1;
            a.SkinColor = Norm(a.SkinColor, "#D6B8AE");
            a.BeardColor = Norm(a.BeardColor ?? a.HairColor, "#3C2823");
            a.HairColor = Norm(a.HairColor ?? a.BeardColor, a.BeardColor);
            a.EyeColor = Norm(a.EyeColor, "#000000");
            a.StubbleColor = Norm(a.StubbleColor, a.SkinColor);
            a.WarPaintColor = Norm(a.WarPaintColor, "#000000");
        }

        private static string Norm(string c, string fallback = "#000000")
        {
            if (string.IsNullOrWhiteSpace(c)) return fallback;
            c = c.Trim();
            if (c[0] != '#') c = "#" + c;
            return c.Length == 7 && c.Skip(1).All(Uri.IsHexDigit) ? c.ToUpperInvariant() : fallback;
        }

        private static string Key(SyntyAppearance a) =>
            string.Join("|", a.Gender, a.Head, a.Hair, a.Eyebrows, a.FacialHair, a.SkinColor, a.BeardColor, a.HairColor, a.EyeColor, a.StubbleColor, a.WarPaintColor);

        private static SyntyAppearance Clone(SyntyAppearance a)
        {
            var c = new SyntyAppearance();
            Copy(a, c);
            c.Id = a.Id;
            c.Cape = a.Cape;
            c.HelmetVisible = a.HelmetVisible;
            return c;
        }

        private static void Copy(SyntyAppearance from, SyntyAppearance to)
        {
            to.Gender = from.Gender;
            to.Head = from.Head;
            to.Hair = from.Hair;
            to.Eyebrows = from.Eyebrows;
            to.FacialHair = from.FacialHair;
            to.SkinColor = from.SkinColor;
            to.HairColor = from.HairColor;
            to.BeardColor = from.BeardColor;
            to.EyeColor = from.EyeColor;
            to.StubbleColor = from.StubbleColor;
            to.WarPaintColor = from.WarPaintColor;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (designer != null)
                {
                    await designer.InvokeVoidAsync("dispose");
                    await designer.DisposeAsync();
                }
                if (module != null) await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
