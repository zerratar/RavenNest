using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Net.DeltaTcpLib;
using RavenNest.DataModels;
using RavenNest.Models;
using Spectre.Console;
using Spectre.Console.Rendering;
using System.Text;
using GameStateRequest = RavenNest.BusinessLogic.Net.DeltaTcpLib.GameStateRequest;

namespace GameStateValidator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var statesFolder = args.Length == 0 ? @"G:\Ravenfall\Data\user-states" : string.Empty;
            var latestStateFile = System.IO.Directory.GetFiles(statesFolder, "*.state")
                .OrderByDescending(f => new System.IO.FileInfo(f).LastWriteTime)
                .FirstOrDefault();

            if (args.Length > 0)
            {
                if (args[0] == "--help" || args[0] == "--h")
                {
                    AnsiConsole.MarkupLine("[bold yellow]GameStateValidator Help[/]");
                    AnsiConsole.MarkupLine("Usage: GameStateValidator [options] [file|directory]");
                    AnsiConsole.MarkupLine("Options:");
                    AnsiConsole.MarkupLine("  --help, --h        Show this help message.");
                    AnsiConsole.MarkupLine("  --version, --v     Show the version of GameStateValidator.");
                    AnsiConsole.MarkupLine("  --dir, --d <path>  Specify a directory to search for state files.");
                    AnsiConsole.MarkupLine("If no arguments are provided, the tool will look for the latest state file in the default directory.");
                    AnsiConsole.MarkupLine("You can also provide a specific file or directory as the first argument.");
                    return;
                }
                else if (args[0] == "--version" || args[0] == "--v")
                {
                    AnsiConsole.MarkupLine("[bold yellow]GameStateValidator Version:[/] 1.0.0");
                    return;
                }
                else if (args[0] == "--dir" || args[0] == "--d")
                {
                    if (args.Length < 2)
                    {
                        AnsiConsole.MarkupLine("[red]Error:[/] Please provide a directory path.");
                        return;
                    }
                    statesFolder = args[1];
                    if (!System.IO.Directory.Exists(statesFolder))
                    {
                        AnsiConsole.MarkupLine($"[red]Error:[/] Directory '{statesFolder}' does not exist.");
                        return;
                    }

                    latestStateFile = System.IO.Directory.GetFiles(statesFolder, "*.state")
                        .OrderByDescending(f => new System.IO.FileInfo(f).LastWriteTime)
                        .FirstOrDefault();
                    if (latestStateFile == null)
                    {
                        AnsiConsole.MarkupLine("[red]Error:[/] No state files found in the specified directory.");
                        return;
                    }
                }
                else
                {
                    // assume the first argument is a file or directory
                    if (System.IO.File.Exists(args[0]))
                    {
                        latestStateFile = args[0];
                    }
                    else if (System.IO.Directory.Exists(args[0]))
                    {
                        statesFolder = args[0];
                        latestStateFile = System.IO.Directory.GetFiles(statesFolder, "*.state")
                            .OrderByDescending(f => new System.IO.FileInfo(f).LastWriteTime)
                            .FirstOrDefault();
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Error:[/] '{args[0]}' is not a valid file or directory.");
                        return;
                    }
                }
            }

            string stateFile = latestStateFile;
            GameStateFile stateData = null;
            GameStateRequest gs = null;
            var xp = default(System.Collections.Generic.List<DeltaExperienceUpdate>);
            var ps = default(System.Collections.Generic.List<CharacterStateDelta>);

            void LoadStateFile(string file)
            {
                stateFile = file;
                Console.Title = $"GameStateValidator - {System.IO.Path.GetFileName(stateFile)}";
                stateData = GameStateParser.Parse(stateFile);
                gs = stateData.GameState;
                xp = stateData.ExperienceState;
                ps = stateData.PlayerState;
            }

            LoadStateFile(stateFile);

            while (true)
            {
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine($"[grey]Loaded state file: [bold]{System.IO.Path.GetFileName(stateFile)}[/][/]");
                AnsiConsole.MarkupLine("[grey]Press [bold]F[/] to change state file at any time.[/]");
                var tab = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[yellow]Select a tab[/]")
                        .AddChoices("Game State", "Players", "Compare States", "Help", "Exit"));

                if (tab == "Exit")
                    break;
                if (tab == "Compare States")
                {
                    CompareStates(statesFolder);
                }
                else if (tab == "Game State")
                {
                    var dungeonPanel = new Panel(gs.Dungeon != null
                        ? GetDungeonText(gs.Dungeon)
                        : new Markup("[grey]No Dungeon Data[/]"))
                        .Header("Dungeon", Justify.Center);

                    var raidPanel = new Panel(gs.Raid != null
                        ? GetRaidText(gs.Raid)
                        : new Markup("[grey]No Raid Data[/]"))
                        .Header("Raid", Justify.Center);

                    AnsiConsole.Write(
                        new Columns(dungeonPanel, raidPanel)
                            .PadRight(2)
                            .PadLeft(2)
                    );
                    AnsiConsole.MarkupLine($"[bold]Player Count:[/] {gs.PlayerCount}");
                    AnsiConsole.MarkupLine("\n[grey]Press any key to return to tab selection...[/]");
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.F)
                    {
                        if (TrySelectStateFile(statesFolder, ref stateFile))
                        {
                            LoadStateFile(stateFile);
                        }
                    }
                }
                else if (tab == "Players")
                {
                    ShowPlayersUI(ps, xp, statesFolder, LoadStateFile, stateFile);
                }
                else if (tab == "Help")
                {
                    ShowHelp();
                }
            }
        }

        static bool TrySelectStateFile(string statesFolder, ref string selectedFile)
        {
            var files = System.IO.Directory.GetFiles(statesFolder, "*.state")
                .OrderByDescending(f => new System.IO.FileInfo(f).LastWriteTime)
                .ToList();

            if (files.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No state files found in the directory.[/]");
                return false;
            }

            var fileNames = files.Select(System.IO.Path.GetFileName).ToList();
            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow]Select a state file to load[/]")
                    .PageSize(15)
                    .AddChoices(fileNames)
            );
            var idx = fileNames.IndexOf(selected);
            if (idx >= 0)
            {
                selectedFile = files[idx];
                return true;
            }
            return false;
        }

        static string FormatDuration(TimeSpan ts)
        {
            // Show days, hours, minutes, and seconds (1 decimal)
            if (ts.TotalDays >= 1)
            {
                if (ts.TotalDays >= 365)
                    return "Never";

                return $"{(int)ts.TotalDays}.{ts.Hours}:{ts.Minutes}:{ts.Seconds}";
            }
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}:{ts.Minutes}:{ts.Seconds}";
            if (ts.TotalMinutes >= 1)
                return $"{(int)ts.TotalMinutes}:{ts.Seconds}";
            return $"{ts.Seconds}.{ts.Milliseconds / 100:D1}s";
        }

        static void CompareStates(string statesFolder)
        {
            string file1 = null, file2 = null;
            if (!TrySelectStateFile(statesFolder, ref file1)) return;
            if (!TrySelectStateFile(statesFolder, ref file2)) return;

            var state1 = GameStateParser.Parse(file1);
            var state2 = GameStateParser.Parse(file2);

            var ps1 = state1.PlayerState.ToDictionary(x => x.CharacterId);
            var ps2 = state2.PlayerState.ToDictionary(x => x.CharacterId);

            var allIds = ps1.Keys.Union(ps2.Keys).ToList();

            var fieldNames = new[]
            {
        "HP", "Island", "Destination", "State", "Training", "Task Arg", "XP/H",
        "ETA LevelUp", "X", "Y", "Z", "JoinRaidCounter", "JoinDungeonCounter",
        "JoinRaidCount", "JoinDungeonCount", "AutoResting", "TrainTargetLevel",
        "Rest Target", "Rest Start", "Dungeon Skill", "Raid Skill" // original names: DungeonCombatStyle, RaidCombatStyle
    };

            int page = 0;

            while (true)
            {
                // Dynamically calculate page size based on window height
                int windowHeight = Console.WindowHeight;
                // Reserve lines for headers, table headers, and footers
                int reservedLines = 11;
                int pageSize = Math.Max(1, windowHeight - reservedLines);

                int totalPages = Math.Max(1, (int)Math.Ceiling(allIds.Count / (double)pageSize));

                AnsiConsole.Clear();
                AnsiConsole.MarkupLine($"[bold yellow]Comparing Player States[/]");
                AnsiConsole.MarkupLine($"[grey]File 1: {System.IO.Path.GetFileName(file1)}[/]");
                AnsiConsole.MarkupLine($"[grey]File 2: {System.IO.Path.GetFileName(file2)}[/]");
                AnsiConsole.MarkupLine($"[grey]Page {page + 1}/{totalPages} (Total Players: {allIds.Count})[/]");
                AnsiConsole.MarkupLine("[grey]n/p: Next/Prev page  q: Quit[/]");

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("PlatformUserName");

                foreach (var field in fieldNames)
                    table.AddColumn(field);

                int start = page * pageSize;
                int end = Math.Min(start + pageSize, allIds.Count);

                for (int i = start; i < end; i++)
                {
                    var id = allIds[i];
                    ps1.TryGetValue(id, out var p1);
                    ps2.TryGetValue(id, out var p2);

                    string name = p1.PlatformUserName ?? p2.PlatformUserName ?? "N/A";

                    var row = new List<string> { name };

                    string Diff<T>(T v1, T v2)
                    {
                        if (p1.CharacterId == Guid.Empty)
                            return $"[green]+{v2}[/]";
                        if (p2.CharacterId == Guid.Empty)
                            return $"[red]-{v1}[/]";
                        if (Equals(v1, v2))
                            return $"[grey]{v1}[/]";
                        var value1 = v1?.ToString() ?? "";
                        var value2 = v2?.ToString() ?? "";
                        if (v1 is DateTime dt1 && v2 is DateTime dt2)
                        {
                            var now = DateTime.UtcNow;
                            value1 = FormatDuration((dt1 - now).Duration());
                            value2 = FormatDuration((dt2 - now).Duration());
                        }
                        else if (v1 is IComparable cmp && v2 is IComparable)
                        {
                            int c = cmp.CompareTo(v2);
                            if (c < 0) return $"[yellow]{value1}>{value2}[/]";
                            if (c > 0) return $"[yellow]{value1}<{value2}[/]";
                        }
                        return $"[yellow]{value1}>{value2}[/]";
                    }

                    row.Add(Diff(p1.Health, p2.Health));
                    row.Add(Diff(p1.Island, p2.Island));
                    row.Add(Diff(p1.Destination, p2.Destination));
                    row.Add(Diff(p1.State, p2.State));
                    row.Add(Diff(p1.TrainingSkillIndex, p2.TrainingSkillIndex));
                    row.Add(Diff(p1.TaskArgument, p2.TaskArgument));
                    row.Add(Diff(p1.ExpPerHour, p2.ExpPerHour));
                    row.Add(Diff(p1.EstimatedTimeForLevelUp, p2.EstimatedTimeForLevelUp));
                    row.Add(Diff(p1.X, p2.X));
                    row.Add(Diff(p1.Y, p2.Y));
                    row.Add(Diff(p1.Z, p2.Z));
                    row.Add(Diff(p1.AutoJoinRaidCounter, p2.AutoJoinRaidCounter));
                    row.Add(Diff(p1.AutoJoinDungeonCounter, p2.AutoJoinDungeonCounter));
                    row.Add(Diff(p1.AutoJoinRaidCount, p2.AutoJoinRaidCount));
                    row.Add(Diff(p1.AutoJoinDungeonCount, p2.AutoJoinDungeonCount));
                    row.Add(Diff(p1.IsAutoResting, p2.IsAutoResting));
                    row.Add(Diff(p1.AutoTrainTargetLevel, p2.AutoTrainTargetLevel));
                    row.Add(Diff(p1.AutoRestTarget, p2.AutoRestTarget));
                    row.Add(Diff(p1.AutoRestStart, p2.AutoRestStart));
                    row.Add(Diff(p1.DungeonCombatStyle, p2.DungeonCombatStyle));
                    row.Add(Diff(p1.RaidCombatStyle, p2.RaidCombatStyle));

                    table.AddRow(row.ToArray());
                }

                AnsiConsole.Write(table);

                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.N || key.Key == ConsoleKey.PageDown || key.Key == ConsoleKey.DownArrow)
                {
                    if (page < totalPages - 1) page++;
                }
                else if (key.Key == ConsoleKey.P || key.Key == ConsoleKey.PageUp || key.Key == ConsoleKey.UpArrow)
                {
                    if (page > 0) page--;
                }
                else if (key.Key == ConsoleKey.Q || key.Key == ConsoleKey.Escape)
                {
                    break;
                }
            }
        }

        static void ShowPlayersUI(
            System.Collections.Generic.List<CharacterStateDelta> ps,
            System.Collections.Generic.List<DeltaExperienceUpdate> xp,
            string statesFolder,
            Action<string> reloadState,
            string currentStateFile)
        {
            var playerData = ps
                .Select(p => new
                {
                    State = p,
                    Exp = xp.FirstOrDefault(e => e.CharacterId == p.CharacterId)
                })
                .ToList();

            string search = "";
            int selectedIndex = 0;
            int page = 0;
            const int pageSize = 10;

            while (true)
            {
                // Filtered list
                var filtered = string.IsNullOrWhiteSpace(search)
                    ? playerData
                    : playerData.Where(p =>
                        (p.State.PlatformUserName ?? "")
                            .Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                int totalPlayers = filtered.Count;
                int totalPages = Math.Max(1, (int)Math.Ceiling(totalPlayers / (double)pageSize));
                page = Math.Clamp(page, 0, totalPages - 1);

                int start = page * pageSize;
                int end = Math.Min(start + pageSize, totalPlayers);

                if (totalPlayers == 0)
                {
                    selectedIndex = 0;
                }
                else
                {
                    selectedIndex = Math.Clamp(selectedIndex, 0, end - start - 1);
                }

                // Draw UI
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine($"[grey]Loaded state file: [bold]{System.IO.Path.GetFileName(currentStateFile)}[/][/]");
                AnsiConsole.MarkupLine("[grey]Press [bold]F[/] to change state file at any time.[/]");
                AnsiConsole.MarkupLine("[bold yellow]Players[/]");
                AnsiConsole.MarkupLine($"[grey]Total: {playerData.Count} | Filtered: {totalPlayers} | Page: {page + 1}/{totalPages}[/]");
                if (!string.IsNullOrWhiteSpace(search))
                    AnsiConsole.MarkupLine($"[grey]Filter: '{search}'[/]");
                AnsiConsole.MarkupLine("[grey]Up/Down: Move  Enter: Details  / or s: Search  n/p: Next/Prev page  Home/End: First/Last page  ?: Help  F: Change file  Esc: Clear search/exit[/]");

                // Show a summary panel for the currently selected player (if any)
                if (end - start > 0)
                {
                    var summary = GetPlayerSummary(filtered[start + selectedIndex].State, filtered[start + selectedIndex].Exp);
                    var summaryPanel = new Panel(summary)
                        .Header("[bold]Selected Player Summary[/]", Justify.Left)
                        .BorderColor(Color.Grey);
                    AnsiConsole.Write(summaryPanel);
                }

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("No.")
                    .AddColumn("PlatformUserName")
                    .AddColumn("Health")
                    .AddColumn("Island")
                    .AddColumn("State");

                for (int i = start; i < end; i++)
                {
                    var p = filtered[i];
                    var isSelected = (i - start) == selectedIndex;
                    var userName = p.State.PlatformUserName ?? "N/A";
                    if (!string.IsNullOrWhiteSpace(search) && userName.Contains(search, StringComparison.OrdinalIgnoreCase))
                    {
                        // Highlight search match
                        int idx = userName.IndexOf(search, StringComparison.OrdinalIgnoreCase);
                        if (idx >= 0)
                        {
                            userName = userName.Substring(0, idx)
                                + "[yellow]"
                                + userName.Substring(idx, search.Length)
                                + "[/]"
                                + userName.Substring(idx + search.Length);
                        }
                    }

                    table.AddRow(
                        isSelected ? $"[green]> {i + 1} <[/]" : (i + 1).ToString(),
                        isSelected ? $"[bold green]{userName}[/]" : userName,
                        $"[aqua]{p.State.Health}[/]",
                        $"[teal]{p.State.Island}[/]",
                        GetStateSummary(p.State.State)
                    );
                }

                AnsiConsole.Write(table);

                // Handle key input
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.DownArrow && end - start > 0)
                {
                    selectedIndex = (selectedIndex + 1) % (end - start);
                }
                else if (key.Key == ConsoleKey.UpArrow && end - start > 0)
                {
                    selectedIndex = (selectedIndex - 1 + (end - start)) % (end - start);
                }
                else if (key.Key == ConsoleKey.Enter && end - start > 0)
                {
                    var p = filtered[start + selectedIndex];
                    ShowPlayerDetailsPanel(p.State, p.Exp);
                }
                else if (key.KeyChar == '/' || key.KeyChar == 's')
                {
                    search = AnsiConsole.Ask<string>("Search:");
                    selectedIndex = 0;
                    page = 0;
                }
                else if (key.Key == ConsoleKey.Escape)
                {
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        search = "";
                        selectedIndex = 0;
                        page = 0;
                    }
                    else
                    {
                        break;
                    }
                }
                else if (key.Key == ConsoleKey.N || key.Key == ConsoleKey.PageDown)
                {
                    if (page < totalPages - 1)
                    {
                        page++;
                        selectedIndex = 0;
                    }
                }
                else if (key.Key == ConsoleKey.P || key.Key == ConsoleKey.PageUp)
                {
                    if (page > 0)
                    {
                        page--;
                        selectedIndex = 0;
                    }
                }
                else if (key.Key == ConsoleKey.Home)
                {
                    page = 0;
                    selectedIndex = 0;
                }
                else if (key.Key == ConsoleKey.End)
                {
                    page = totalPages - 1;
                    selectedIndex = 0;
                }
                else if (key.KeyChar == '?')
                {
                    ShowHelp();
                }
                else if (key.Key == ConsoleKey.F)
                {
                    string newFile = currentStateFile;
                    if (TrySelectStateFile(statesFolder, ref newFile))
                    {
                        reloadState(newFile);
                        break;
                    }
                }
            }
        }

        static void ShowPlayerDetailsPanel(CharacterStateDelta state, DeltaExperienceUpdate exp)
        {
            // Left: player details, Right: skills with progress bars
            var detailsPanel = new Panel(GetPlayerDetails(state, exp))
                .Header($"[bold yellow]{state.PlatformUserName ?? "N/A"}[/]", Justify.Center)
                .BorderColor(Color.Green);

            var skillsPanel = new Panel(GetSkillsBarChart(exp))
                    .Header("[bold blue]Skills[/]", Justify.Center)
                    .BorderColor(Color.Blue);

            AnsiConsole.Clear();
            AnsiConsole.Write(
                new Columns(detailsPanel, skillsPanel)
                    .PadRight(2)
                    .PadLeft(2)
            );
            AnsiConsole.MarkupLine("[grey]Press any key to return to the player list...[/]");
            Console.ReadKey(true);
        }

        static IRenderable GetSkillsBarChart(DeltaExperienceUpdate exp)
        {
            if (exp.Changes == null || exp.Changes.Length == 0)
                return new Markup("[grey]No skills data available.[/]");

            var chart = new BarChart()
                .Width(40)
                .Label("[bold]Level Progress[/]")
                .WithMaxValue(100)
                .CenterLabel();

            foreach (var c in exp.Changes.OrderBy(x => x.Index))
            {
                double nextLevelExp = GameMath.ExperienceForLevel(c.Level + 1);
                double progress = nextLevelExp > 0 ? Math.Clamp(c.Experience / nextLevelExp, 0, 1) : 0;
                var color = progress >= 0.99 ? Color.Green : Color.Yellow;
                var skillLabel = $"[aqua]{((GameMath.Skill)c.Index)}[/] [yellow]L{c.Level}[/]";
                var value = Math.Round((progress * 100), 2);
                chart.AddItem(skillLabel, value, color);
            }

            return chart;
        }

        static Markup GetPlayerSummary(CharacterStateDelta state, DeltaExperienceUpdate exp)
        {
            // Show a short summary: PlatformUserName, Health, Island, State, and first 3 skills
            var skills = exp.Changes != null
                ? string.Join(", ", exp.Changes.Take(3).Select(c =>
                    $"[aqua]{((GameMath.Skill)c.Index)}[/]:[yellow]L{c.Level}[/]"))
                : "N/A";

            return new Markup(
                $"[bold]User:[/] [green]{state.PlatformUserName ?? "N/A"}[/]  " +
                $"[bold]Health:[/] [aqua]{state.Health}[/]  " +
                $"[bold]Island:[/] [teal]{state.Island}[/]  " +
                $"[bold]State:[/] [purple]{GetStateSummary(state.State)}[/]  " +
                $"[bold]Skills:[/] {skills}"
            );
        }

        static string GetStateSummary(CharacterFlags state)
        {
            if (state == CharacterFlags.None) return "[grey]Idle[/]";
            var sb = new StringBuilder();
            if (state.HasFlag(CharacterFlags.InRaid)) sb.Append("[red]Raid[/],");
            if (state.HasFlag(CharacterFlags.InArena)) sb.Append("[blue]Arena[/],");
            if (state.HasFlag(CharacterFlags.InDungeon)) sb.Append("[yellow]Dungeon[/],");
            if (state.HasFlag(CharacterFlags.InOnsen)) sb.Append("[aqua]Onsen[/],");
            if (state.HasFlag(CharacterFlags.InDuel)) sb.Append("[magenta]Duel[/],");
            if (state.HasFlag(CharacterFlags.InStreamRaidWar)) sb.Append("[purple]StreamRaid[/],");
            if (state.HasFlag(CharacterFlags.InDungeonQueue)) sb.Append("[grey]DungeonQ[/],");
            if (state.HasFlag(CharacterFlags.OnFerry)) sb.Append("[teal]Ferry[/],");
            if (state.HasFlag(CharacterFlags.IsCaptain)) sb.Append("[green]Captain[/],");
            if (sb.Length > 0) sb.Length--; // Remove trailing comma
            return sb.ToString();
        }

        static Markup GetPlayerDetails(CharacterStateDelta state, DeltaExperienceUpdate exp)
        {
            return new Markup(
                $"[bold]CharacterId:[/] [grey]{state.CharacterId}[/]\n" +
                $"[bold]Health:[/] [aqua]{state.Health}[/]\n" +
                $"[bold]Island:[/] [teal]{state.Island}[/]\n" +
                $"[bold]Destination:[/] [teal]{state.Destination}[/]\n" +
                $"[bold]State:[/] {GetStateSummary(state.State)}\n" +
                $"[bold]TrainingSkillIndex:[/] [yellow]{state.TrainingSkillIndex}[/]\n" +
                $"[bold]TaskArgument:[/] [grey]{state.TaskArgument}[/]\n" +
                $"[bold]ExpPerHour:[/] [yellow]{state.ExpPerHour}[/]\n" +
                $"[bold]EstimatedTimeForLevelUp:[/] [yellow]{state.EstimatedTimeForLevelUp}[/]\n" +
                $"[bold]X:[/] [grey]{state.X}[/], [bold]Y:[/] [grey]{state.Y}[/], [bold]Z:[/] [grey]{state.Z}[/]\n" +
                $"[bold]AutoJoinRaidCounter:[/] [yellow]{state.AutoJoinRaidCounter}[/]\n" +
                $"[bold]AutoJoinDungeonCounter:[/] [yellow]{state.AutoJoinDungeonCounter}[/]\n" +
                $"[bold]AutoJoinRaidCount:[/] [yellow]{state.AutoJoinRaidCount}[/]\n" +
                $"[bold]AutoJoinDungeonCount:[/] [yellow]{state.AutoJoinDungeonCount}[/]\n" +
                $"[bold]IsAutoResting:[/] [yellow]{state.IsAutoResting}[/]\n" +
                $"[bold]AutoTrainTargetLevel:[/] [yellow]{state.AutoTrainTargetLevel}[/]\n" +
                $"[bold]AutoRestTarget:[/] [yellow]{state.AutoRestTarget}[/]\n" +
                $"[bold]AutoRestStart:[/] [yellow]{state.AutoRestStart}[/]\n" +
                $"[bold]DungeonCombatStyle:[/] [yellow]{state.DungeonCombatStyle}[/]\n" +
                $"[bold]RaidCombatStyle:[/] [yellow]{state.RaidCombatStyle}[/]\n" +
                $"[bold]Platform:[/] [grey]{state.Platform}[/]\n" +
                $"[bold]PlatformUserId:[/] [grey]{state.PlatformUserId}[/]\n" +
                $"[bold]PlatformUserName:[/] [green]{state.PlatformUserName}[/]\n"
            );
        }

        static Markup GetDungeonText(dynamic dungeon)
        {
            if (dungeon == null) return new Markup("[grey]No Dungeon Data[/]");
            var sb = new StringBuilder();
            sb.AppendLine($"[bold]IsActive:[/] {dungeon.IsActive}");
            if (dungeon.IsActive)
            {
                sb.AppendLine($"[bold]Name:[/] [yellow]{dungeon.Name}[/]");
                sb.AppendLine($"[bold]HasStarted:[/] [green]{dungeon.HasStarted}[/]");
                sb.AppendLine($"[bold]BossCombatLevel:[/] [red]{dungeon.BossCombatLevel}[/]");
                sb.AppendLine($"[bold]CurrentBossHealth:[/] [aqua]{dungeon.CurrentBossHealth}[/]");
                sb.AppendLine($"[bold]MaxBossHealth:[/] [aqua]{dungeon.MaxBossHealth}[/]");
                sb.AppendLine($"[bold]PlayersAlive:[/] [green]{dungeon.PlayersAlive}[/]");
                sb.AppendLine($"[bold]PlayersJoined:[/] [yellow]{dungeon.PlayersJoined}[/]");
                sb.AppendLine($"[bold]EnemiesLeft:[/] [red]{dungeon.EnemiesLeft}[/]");
                sb.AppendLine($"[bold]StartTime:[/] [grey]{dungeon.StartTime}[/]");
            }
            sb.AppendLine($"[bold]NextDungeon:[/] [grey]{dungeon.NextDungeon}[/]");

            return new Markup(
                sb.ToString()
            );
        }

        static Markup GetRaidText(dynamic raid)
        {
            if (raid == null) return new Markup("[grey]No Raid Data[/]");

            var sb = new StringBuilder();
            sb.AppendLine($"[bold]IsActive:[/] {raid.IsActive}");
            if (raid.IsActive)
            {
                if (raid.Boss != null)
                {
                    sb.AppendLine($"[bold]Boss:[/] [yellow]{raid.Boss.Name}[/] (Level [red]{raid.Boss.CombatLevel}[/])");
                    sb.AppendLine($"[bold]CurrentBossHealth:[/] [aqua]{raid.Boss.CurrentHealth}[/]");
                    sb.AppendLine($"[bold]MaxBossHealth:[/] [aqua]{raid.Boss.MaxHealth}[/]");
                }
                else
                {
                    sb.AppendLine("[bold]Boss:[/] N/A");
                }

                sb.AppendLine($"[bold]PlayersJoined:[/] [yellow]{raid.PlayersJoined}[/]");
                sb.AppendLine($"[bold]EndTime:[/] [grey]{raid.EndTime}[/]");
            }
            sb.AppendLine($"[bold]NextRaid:[/] [grey]{raid.NextRaid}[/]");

            return new Markup(
                sb.ToString()
            );
        }

        static void ShowHelp()
        {
            AnsiConsole.Clear();
            var help = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("[bold]Key[/]")
                .AddColumn("[bold]Action[/]")
                .AddRow("Up/Down", "Move selection in player list")
                .AddRow("Enter", "Show details for selected player")
                .AddRow("/ or s", "Search/filter by PlatformUserName")
                .AddRow("n or PageDown", "Next page")
                .AddRow("p or PageUp", "Previous page")
                .AddRow("Home", "First page")
                .AddRow("End", "Last page")
                .AddRow("Esc", "Clear search or exit player list")
                .AddRow("?", "Show this help")
                .AddRow("Tab", "Switch between main tabs");

            AnsiConsole.Write(new Panel(help).Header("[bold yellow]Help[/]", Justify.Center));
            AnsiConsole.MarkupLine("\n[grey]Press any key to return...[/]");
            Console.ReadKey(true);
        }
    }
}
