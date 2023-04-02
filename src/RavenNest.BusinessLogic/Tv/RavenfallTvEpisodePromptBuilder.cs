using RavenNest.Models.Tv;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.Tv
{
    public class RavenfallTvEpisodePromptBuilder
    {
        private string episodeTitle;
        private string episodeDescription;
        private string title;
        private string description;
        private string language;
        private List<Episode.Character> characters;

        public RavenfallTvEpisodePromptBuilder SetEpisodeTitle(string title)
        {
            episodeTitle = title;

            return this;
        }

        public RavenfallTvEpisodePromptBuilder SetEpisodeDescription(string description)
        {
            episodeDescription = description;
            return this;
        }

        public RavenfallTvEpisodePromptBuilder SetLanguage(string language)
        {
            this.language = language;
            return this;
        }

        public RavenfallTvEpisodePromptBuilder SetShowTitle(string title)
        {
            this.title = title;
            return this;
        }

        public RavenfallTvEpisodePromptBuilder SetShowDescription(string description)
        {
            this.description = description;
            return this;
        }

        public RavenfallTvEpisodePromptBuilder SetCharacters(List<Episode.Character> characters)
        {
            this.characters = characters;
            return this;
        }

        public override string ToString()
        {
            var prompt = @"Generate the script for a short sitcom/tv show, it needs to be funny, exciting, and innovative. The result should be in the following JSON format:
{
""Title"": ""name of the episode"",
""Description"": ""A short description of the episode and what it's about"",
""Outro"": ""A short outro, finalizing and wrapping up the result of the episode and if the episode did not have an outcome, what event happened afterward."",
""Language"": ""The language of the episode, English, German, French, etc. This must be English unless said differently"",
""Characters"": [
""Id"": ""Character id"",
""Name"": ""Name of the character"",
""Gender"": ""male or female"",
""Race"": ""human, dragon, skeleton, etc"",
""Job"": ""tank, warrior, ranger, mage, healer, pirate, bartender, etc"",
""Description"": ""A description of the character, describing its personality and more"",
""Strength"": ""A value between 1 and 999 representing how strong or proficient the character is in their job"",
],
""Dialogues"": [ {
""Action"": {
""Animation"": ""animation_name"",
""Description"": ""Describe what the character is doing, but express it in a simple, understandable format"",
""Target"": {
""Type"": ""Character, Group, Object, or None"",
""Identifier"": ""Identifier of the target (if applicable)""
}
},
""Location"": ""Current location of the character"",
""Character"": ""The name of the character this dialogue belongs to"",
""Text"": ""A text that will be spoken"",
} ]
}

Characters are the available characters in the show and can be used multiple times, they are referenced in the dialogues by their name
If a character is used in a dialogue either by target or text, it must also be included in the list of characters, except for when it is not an actual character of the show.

The strength of a character represent how strong they are in combat or how proficient they are in their job, it is a value between 1 and 999 where 999 is the strongest.

in the dialogue structure
Location is one of the islands Dawnhaven, Dreadmaw, Ironhill, Kyo, Heim, it can also be Tavern or Dungeon.
Text is the dialogue of the character spoken, be creative.
Animation is a name of a potential 3d animation that can be used to visualize what the character is doing.

The setting for the show is a fantasy, medieval world with magic and monsters, based on a game called Ravenfall. There are 5 islands that can be explored:

* Dawnhaven aka Home, the starting island where all players start.
* Dreadmaw, aka Away, the second island players visit and is filled with pirates.
* Ironhill, the third island and consists of old ruins and dragons that live there.
* Kyo, an island inspired by Japan, filled with aggressive and dangerous samurais to fight, but with also an onsen resting area.
* Heim, the last island available only for the strongest people, filled with ferocious Vikings.

There is also a Tavern for the players to relax, play tic-tac-toe, and drink in. There are also random events of huge raid bosses appearing on the different islands and dungeons containing big bosses to slay.

The episode has to have at least 15 dialogues but please make more if suitable. The episode cannot end with a cliffhanger; it must have a conclusion!

Your response should only include the JSON.

Use the following predefined animations when creating dialogues: ""idle"", ""sit"", ""drink"", ""shoot_arrow"", ""walk"", ""run"", ""jump"", ""attack"", ""defend"", ""cast_spell"", ""heal"", ""talk"", ""point"", ""agree"", ""disagree"", ""wave"", ""celebrate"", ""suprised"", and ""hurt"". Add more animations if necessary, but try to keep the list limited for easier implementation in Unity.

In the dialogue structure:

* Location is one of the islands Dawnhaven, Dreadmaw, Ironhill, Kyo, Heim, it can also be Tavern or Dungeon.
* Text is the dialogue of the character spoken, be creative.
* Use the ""Animation"" and ""Description"" fields inside the ""Action"" object to describe what the character is doing.
* Use the ""Target"" field inside the ""Action"" object to specify the target character, group, object, or none involved in the action (if applicable). The target can be identified by their name.

When a character asks a question to another character, the question must be answered. It is important that all characters are in the same area, so if one character is in the Tavern, all characters must be in the Tavern because it would be impossible for the characters to talk to each other otherwise. If they are in different areas, they can still talk to each other, but it has to be done in a way that makes sense, 
for example, if one character is in the Tavern and the other is in the Dungeon, the Tavern character can ask the Dungeon character to come to the Tavern. You can be creative with characters picking up things; they can do random funny stuff.
Be sure to maintain consistency in locations, animations, and actions throughout the generated episode.

Keep in mind that the setting for the show is a fantasy, medieval world with magic and monsters, and the characters' interactions and dialogues should be based on this theme. Feel free to include humor, plot twists, and exciting events to keep the audience engaged.

To ensure a smooth transition between different scenes and character interactions, make sure that the dialogues flow naturally and that the characters' actions are consistent with their personality and role. Additionally, remember to use the predefined animations and action structure as mentioned in the prompt to facilitate easier implementation in Unity.

When creating new episodes, ensure that each episode has a self-contained plot that concludes by the end of the episode, avoiding cliffhangers. However, you may add overarching themes or character arcs that span multiple episodes, adding depth to the story and characters.

";

            if (characters != null && characters.Count > 0)
            {
                prompt += "The different characters that can be used are:\n";
                prompt += string.Join("\n", characters.Select(x => "- " + x.ToString()).OrderBy(x => Random.Shared.NextDouble()));
                prompt += "\n\n";
                prompt += "Not all characters need to be used, but when using a character it has to be one of them unless there are not enough characters then you are free to come up with your own.\nGenerated characters and not used from the list should use index and not Guid as Id.\n\n\n";
            }

            prompt += @"You can be creative with characters picking up things, they can do random funny stuff.
";

            if (!string.IsNullOrEmpty(title))
            {
                prompt += "The name of the whole show is " + title + ".\n";
            }

            if (!string.IsNullOrEmpty(description))
            {
                prompt += "Additional description and theme of the whole show, which the episodes should make sure to follow:\n" + description + "\n\n";
            }

            if (!string.IsNullOrEmpty(episodeTitle))
            {
                prompt += "The title of the episode is " + episodeTitle + ".\n";
            }

            if (!string.IsNullOrEmpty(episodeDescription))
            {
                prompt += "The outline/description of the episode need to be with the following story:\n";
                prompt += "\"" + episodeDescription + "\"\n";
            }

            if (!string.IsNullOrEmpty(language))
            {
                prompt += "The dialogue text and action description must be translated into " + language + " and the language field in the json should be set to this language as well.\nAnimation must always be in english!\n";
            }

            return prompt;
        }

    }
}
