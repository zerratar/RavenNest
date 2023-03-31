using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RavenNest.Models.Tv
{
    public class GenerateEpisodeRequest
    {
        /// <summary>
        ///     The requested description of the episode, leave empty for have one generated.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        ///     The requested description of the episode, leave empty for have one generated.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     The requested outro/conclusion of the episode, leave empty for have one generated.
        /// </summary>
        public string Outro { get; set; }

        /// <summary>
        ///     The available characters to be in the show, not all characters are guaranteed to be included.
        /// </summary>
        public List<Episode.Character> Characters { get; set; }

        /// <summary>
        ///     This will be the Id of the generated episode.
        /// </summary>
        /// 
        public Guid Id { get; set; }

    }

    public enum EpisodeGenerationStatus
    {
        Generating,
        Completed,
        Error,
        NotFound,
    }

    public class EpisodeResult
    {
        public Guid Id { get; set; }
        public Episode Episode { get; set; }
        public EpisodeGenerationStatus Status { get; set; }
    }

    public class Episode
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        [JsonPropertyName("user_id")]
        public Guid? UserId { get; set; }
        [JsonPropertyName("language")]
        public string Language { get; set; }
        [JsonPropertyName("title")]
        public string Title { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("outro")]
        public string Outro { get; set; }

        [JsonPropertyName("characters")]
        public Character[] Characters { get; set; }

        [JsonPropertyName("dialogues")]
        public Dialogue[] Dialogues { get; set; }

        public class Character
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
            [JsonPropertyName("name")]
            public string Name { get; set; }
            [JsonPropertyName("gender")]
            public string Gender { get; set; }
            [JsonPropertyName("race")]
            public string Race { get; set; }
            [JsonPropertyName("job")]
            public string Job { get; set; }
            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("strength")]
            public int Strength { get; set; }

            public override string ToString()
            {
                return "id: " + Id +
                    ", name: \"" + Name + "\"" +
                    ", gender: " + Gender +
                    ", race: " + Race +
                    ", job: " + Job +
                    ", strength: " + Strength +
                    ", description: \"" + Description + "\"";
            }
        }

        public class Dialogue
        {
            [JsonPropertyName("animation")]
            public string Animation { get; set; }
            [JsonPropertyName("character_name")]
            public string CharacterName { get; set; }
            [JsonPropertyName("location")]
            public string Location { get; set; }

            [JsonPropertyName("text")]
            public string Text { get; set; }

            public override string ToString()
            {
                return Animation + ", " + CharacterName + ": \"" + Text + "\"";
            }
        }
    }

}
