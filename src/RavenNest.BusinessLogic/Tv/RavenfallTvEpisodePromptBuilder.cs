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
            var prompt = @"Generate the script for a short sitcom/tv show, it needs to be funny, exciting and innovative, the result should be in the following json format:

{
   ""title"": ""name of the episode"",
   ""description"": ""A short description of the episode and what its about"",
   ""outro"": ""A short outro, finalizing and wrapping up the result of the episode and if the episode did not have an outcome, what event happened afterwards."",
   ""language"": ""The language of the episode, english, german, french, etc. This must be english unless said differently"",
   ""characters"": [
     ""id"": ""Character id"",
     ""name"": ""Name of the character"",
     ""gender"": ""male or female"",
     ""race"": ""human, dragon, skeleton, etc"",
     ""job"": ""tank, warrior, ranger, mage, healer, pirate, bartender, etc"",
     ""description"": ""A description of the character, describing its personality and more"",
     ""strength"": ""A value between 1 and 999 representing how strong or proficient the character is in their job"",
   ],
   ""dialogues"": [ {
     ""animation"": ""Animation_to_play"",
     ""action_description"": ""Describe what the character is doing"",
     ""location"": ""Current location of the character"",
     ""character_name"": ""The name of the character this dialogue belongs to"",
     ""text"": ""A text that will be spoken"",
   } ] 
}

Characters are the available characters in the show and can be used multiple times, they are referenced in the dialogues by their name

The strength of a character represent how strong they are in combat or how proficient they are in their job, it is a value between 1 and 999 where 999 is the strongest.

in the dialogue structure
Location is one of the islands Dawnhaven, Dreadmaw, Ironhill, Kyo, Heim, it can also be Tavern or Dungeon.
Text is the dialogue of the character spoken, be creative.
Animation is a name of a potential 3d animation that can be used to visualize what the character is doing.

The setting for the show is that it is a fantasy, medieval world with magic and monsters, based on a game called Ravenfall, 
there are 5 islands that can be explored: 
* Dawnhaven aka Home, it is the starting island where all players start.
* Dreadmaw, aka Away, it is the second island players visits and is filled with pirates.
* Ironhill, the third island and consists of old ruins and Dragons that lives there.
* Kyo, an island inspired by Japan, filled with aggressive and dangerous samurais to fight, but with also an onsen resting area.
* Heim, the last island available only for the strongest people, filled with ferocious vikings.

There is also a Tavern for the players to relax, play tic tac toe and drink in.
There are also random events of huge raid bosses appearing on the different islands as well as dungeons appearing containing big bosses to slay.

The only area used for the show is this world, the different islands, Dungeons, and the Tavern.
It is also very important that all characters are in the same area, so if one character is in the Tavern, all characters must be in the Tavern, because it would be impossible for the characters to talk to eachother otherwise.
If they are in different areas, they can still talk to eachother, but it has to be done in a way that makes sense, for example if one character is in the Tavern and the other is in the Dungeon, the Tavern character can ask the Dungeon character to come to the Tavern.
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

            prompt += @"It is important that when a character asks a question to another character, the question must be answered.

The episode has to have at least 15 dialogues but please make more if suitable.
The episode cannot end with a cliffhanger, it must have a conclusion!

";
            if (!string.IsNullOrEmpty(language))
            {
                prompt += "The dialogue text and action description must be translated into " + language + " and the language field in the json should be set to this language as well.\nAnimation must always be in english!\n";
            }

            prompt += "Your response should only include the json.";

            return prompt;
        }

    }
}
