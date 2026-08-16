using System.Collections.Generic;
using System.Linq;
using RavenNest.BusinessLogic.AI;

namespace RavenNest.BusinessLogic.Settings
{
    /// <summary>
    ///     What kind of value a setting holds, which decides how it is shown and how it is read back.
    /// </summary>
    public enum ServerSettingKind
    {
        Text,

        /// <summary>
        ///     An API key, a token, a webhook URL. Never rendered back to the page, never logged.
        /// </summary>
        Secret,

        Number,

        Toggle
    }

    /// <summary>
    ///     Where the value in use came from.
    /// </summary>
    public enum ServerSettingSource
    {
        /// <summary>Nothing is set anywhere; the built in default applies.</summary>
        Default,

        /// <summary>From appsettings.json or an environment variable.</summary>
        Configuration,

        /// <summary>Set from the admin panel, and overrides the configuration file.</summary>
        Database
    }

    /// <summary>
    ///     One knob the admin panel knows about.
    /// </summary>
    /// <remarks>
    ///     The panel is driven by this list rather than by whatever rows happen to exist in the
    ///     database. A settings page that lets you type any name and any value is a page that lets
    ///     you silently misspell one and spend an afternoon wondering why nothing happened.
    ///
    ///     <para>
    ///     <see cref="Key"/> is both the row name and the configuration path, so a value already
    ///     living in appsettings.json keeps working and shows up here as the one in use.
    ///     </para>
    /// </remarks>
    public sealed class ServerSettingDefinition
    {
        public ServerSettingDefinition(
            string key,
            string group,
            string label,
            string description,
            ServerSettingKind kind = ServerSettingKind.Text,
            string defaultValue = null,
            string placeholder = null,
            string help = null,
            IReadOnlyList<ServerSettingChoice> choices = null)
        {
            Key = key;
            Group = group;
            Label = label;
            Description = description;
            Kind = kind;
            DefaultValue = defaultValue;
            Placeholder = placeholder;
            Help = help;
            Choices = choices;
        }

        /// <summary>The row name, and the configuration path it falls back to.</summary>
        public string Key { get; }

        /// <summary>Heading it sits under in the panel.</summary>
        public string Group { get; }

        public string Label { get; }

        /// <summary>What it does, in a sentence, for somebody who did not write it.</summary>
        public string Description { get; }

        public ServerSettingKind Kind { get; }

        public string DefaultValue { get; }

        public string Placeholder { get; }

        /// <summary>Where to get the value, for the ones that come from somewhere else.</summary>
        public string Help { get; }

        /// <summary>
        ///     Suggestions, when there is a known set of sensible values. The field still accepts
        ///     anything typed into it: these lists go out of date faster than the site is deployed,
        ///     and a closed list would mean waiting for a release to use a new model.
        /// </summary>
        public IReadOnlyList<ServerSettingChoice> Choices { get; }

        public bool HasChoices => Choices != null && Choices.Count > 0;

        public bool IsSecret => Kind == ServerSettingKind.Secret;
    }

    public sealed class ServerSettingChoice
    {
        public ServerSettingChoice(string value, string label, string note = null)
        {
            Value = value;
            Label = label;
            Note = note;
        }

        public string Value { get; }
        public string Label { get; }
        public string Note { get; }
    }

    /// <summary>
    ///     Every setting the admin panel can change.
    /// </summary>
    /// <remarks>
    ///     Only things that are actually wired to something belong here. A knob on a page that turns
    ///     nothing is worse than no knob, because it will be set once and then trusted.
    /// </remarks>
    public static class ServerSettingsRegistry
    {
        public const string DiscordAnnouncementWebhook = "Discord:AnnouncementWebhook";

        /// <summary>
        ///     The same path the old hand written client reads, so a key already sitting in
        ///     appsettings.json keeps working and shows up here as the one in use.
        /// </summary>
        public const string OpenAiAccessToken = "OpenAI:AccessToken";

        public const string OpenAiModel = "OpenAI:Model";

        public static readonly IReadOnlyList<ServerSettingDefinition> All = new List<ServerSettingDefinition>
        {
            new ServerSettingDefinition(
                DiscordAnnouncementWebhook,
                "Discord",
                "Announcement webhook",
                "Where the Send to Discord button on a news post sends it. Without this the button is off and posts only appear on the site.",
                ServerSettingKind.Secret,
                placeholder: "https://discord.com/api/webhooks/...",
                help: "Discord: Server Settings, Integrations, Webhooks, New Webhook. Pick the channel, then Copy Webhook URL."),

            new ServerSettingDefinition(
                OpenAiAccessToken,
                "OpenAI",
                "API key",
                "Turns on the writing help in the news editor. Everything else on the site works without it.",
                ServerSettingKind.Secret,
                placeholder: "sk-...",
                help: "platform.openai.com, API keys, Create new secret key. It is shown once, so paste it straight in."),

            new ServerSettingDefinition(
                OpenAiModel,
                "OpenAI",
                "Model",
                "Which model the assistant uses. The suggestions are current; anything else can be typed in.",
                ServerSettingKind.Text,
                defaultValue: AiModels.Default,
                placeholder: AiModels.Default,
                choices: AiModels.Suggested
                    .Select(x => new ServerSettingChoice(x.Id, x.Label, x.Note))
                    .ToList()),
        };
    }
}
