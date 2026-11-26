using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RavenNest.Blazor.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Components
{
    /// <summary>
    ///     How the Inventory tab lays the bag out. Grouped (the default): a part for each kind with its first two rows,
    ///     "Show all" for the rest, and a part can be folded away. Everything: the one grid of before. Small or big
    ///     tiles. A Discord player found the one grid of everything too much at once, others like it: both are here.
    ///
    ///     What the viewer picks is kept in this browser (localStorage through js/rf-gear.js, which never throws), so
    ///     the page works the same without it: it then opens grouped with small tiles every time.
    /// </summary>
    public partial class PlayerGear
    {
        /// <summary>The rows a part shows before "Show all".</summary>
        private const int SectionRows = 2;

        private bool everything;
        private bool bigTiles;
        // the kinds folded away, and the kinds showing all of theirs
        private readonly HashSet<GearKind> closedKinds = new HashSet<GearKind>();
        private readonly HashSet<GearKind> openKinds = new HashSet<GearKind>();
        private bool invPrefsRead;

        // tiles a row holds, measured by the page's script on a hidden grid; a guess until it has
        private int invColumns = 8;
        private ElementReference invProbe;
        private string watchedProbe;
        private bool measureAgain;

        private bool Searching => !string.IsNullOrWhiteSpace(search);

        /// <summary>The parts are shown: grouped, and no kind chip picked (a picked kind shows all of it in one grid).</summary>
        private bool Grouped => !everything && invKind == GearKind.All;

        private int RowsCap => Math.Max(1, invColumns) * SectionRows;

        internal sealed record Section(GearKind Kind, List<Entry> Entries);

        /// <summary>The shown stacks by kind, in the order of the chips; each part keeps the sort.</summary>
        private static List<Section> Sections(List<Entry> visible)
        {
            return visible.GroupBy(x => x.Kind).OrderBy(x => x.Key).Select(x => new Section(x.Key, x.ToList())).ToList();
        }

        /// <summary>A search shows every match, so nothing is folded away while searching.</summary>
        private bool IsClosed(GearKind kind) => !Searching && closedKinds.Contains(kind);

        /// <summary>
        ///     The tiles a part shows: all while searching or after "Show all", else two rows. When less than half a row
        ///     would be left over, all of them: a button for one or two more tiles is only in the way.
        /// </summary>
        private int Cap(GearKind kind, int count)
        {
            if (Searching || openKinds.Contains(kind)) return count;
            return count - RowsCap < (Math.Max(1, invColumns) + 1) / 2 ? count : RowsCap;
        }

        /// <summary>A part long enough to have been cut to two rows (so "Show fewer" means something).</summary>
        private bool IsLong(int count) => count - RowsCap >= (Math.Max(1, invColumns) + 1) / 2;

        private async Task ToggleSection(GearKind kind)
        {
            if (!closedKinds.Remove(kind)) closedKinds.Add(kind);
            await SavePref("inv.closed", string.Join(",", closedKinds));
        }

        private async Task ToggleMore(GearKind kind)
        {
            var fewer = openKinds.Remove(kind);
            if (!fewer) openKinds.Add(kind);
            await SavePref("inv.open", string.Join(",", openKinds));

            // back to two rows from far down a long part: its top comes back into sight, not the parts under it
            if (fewer && module != null)
            {
                try { await module.InvokeVoidAsync("revealSection", root, "rf-invsec-" + kind); }
                catch (JSException) { }
                catch (JSDisconnectedException) { }
            }
        }

        private async Task SetEverything(bool on)
        {
            if (everything == on) return;
            everything = on;
            await SavePref("inv.view", on ? "everything" : "grouped");
        }

        private async Task SetBig(bool on)
        {
            if (bigTiles == on) return;
            bigTiles = on;
            measureAgain = true;
            await SavePref("inv.tiles", on ? "big" : "small");
        }

        /// <summary>Picking several: every stack of one part that can be picked, or none of them.</summary>
        private void PickSection(List<Entry> movable, bool all)
        {
            foreach (var e in movable)
            {
                if (all) selected.Add(e.Stack.Id); else selected.Remove(e.Stack.Id);
            }
        }

        /// <summary>The page's script says how many tiles a row holds now (the window or the panel beside it changed).</summary>
        [JSInvokable]
        public void InvColumns(int columns)
        {
            if (columns <= 0 || columns == invColumns) return;
            invColumns = columns;
            StateHasChanged();
        }

        /// <summary>After a render of the Inventory tab: the viewer's choices once, and the row length when it can have changed.</summary>
        private async Task SectionsAfterRenderAsync()
        {
            if (module == null) return;

            if (!invPrefsRead)
            {
                invPrefsRead = true;
                try
                {
                    var saved = await module.InvokeAsync<string[]>("prefs", (object)new[] { "inv.view", "inv.tiles", "inv.closed", "inv.open" });
                    if (saved != null && saved.Length == 4)
                    {
                        everything = saved[0] == "everything";
                        bigTiles = saved[1] == "big";
                        ReadKinds(saved[2], closedKinds);
                        ReadKinds(saved[3], openKinds);
                        if (everything || bigTiles || closedKinds.Count > 0 || openKinds.Count > 0)
                        {
                            measureAgain = true;
                            StateHasChanged();
                            return;
                        }
                    }
                }
                catch (JSException)
                {
                }
            }

            if (!Grouped || invProbe.Id == null) return;
            try
            {
                int columns;
                if (invProbe.Id != watchedProbe)
                {
                    watchedProbe = invProbe.Id;
                    measureAgain = false;
                    columns = await module.InvokeAsync<int>("watchColumns", root, invProbe);
                }
                else if (measureAgain)
                {
                    measureAgain = false;
                    columns = await module.InvokeAsync<int>("columns", root, invProbe);
                }
                else
                {
                    return;
                }

                InvColumns(columns);
            }
            catch (JSException)
            {
            }
        }

        private static void ReadKinds(string text, HashSet<GearKind> into)
        {
            into.Clear();
            if (string.IsNullOrEmpty(text)) return;
            foreach (var word in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse<GearKind>(word.Trim(), out var kind) && kind != GearKind.All) into.Add(kind);
            }
        }

        private async Task SavePref(string name, string value)
        {
            if (module == null) return;
            try { await module.InvokeVoidAsync("setPref", name, value); }
            catch (JSException) { }
            catch (JSDisconnectedException) { }
        }
    }
}
