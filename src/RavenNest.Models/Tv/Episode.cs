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
        public Guid? Id { get; set; }
        public Guid? UserId { get; set; }
        public string Language { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Outro { get; set; }
        public Character[] Characters { get; set; }
        public Dialogue[] Dialogues { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Requested { get; set; }

        public class Character
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Gender { get; set; }
            public string Race { get; set; }
            public string Job { get; set; }
            public string Description { get; set; }
            public int Strength { get; set; }

            /// <summary>
            ///     Whether or not this character is a real Ravenfall character or not. If false, 
            ///     this is an AI generated character. It will be required to load the character data from the server.
            /// </summary>
            public bool IsReal { get; set; }
            public override string ToString()
            {
                return "Id: " + Id +
                    ", Name: \"" + Name + "\"" +
                    ", Gender: " + Gender +
                    ", Race: " + Race +
                    ", Job: " + Job +
                    ", Strength: " + Strength +
                    ", Description: \"" + Description + "\"";
            }
        }

        public class Dialogue
        {
            public string Character { get; set; }
            public string Location { get; set; }
            public string Text { get; set; }
            public Action Action { get; set; }
        }

        public class Action
        {
            public string Animation { get; set; }
            public string Description { get; set; }
            public ActionTarget Target { get; set; }
        }

        public class ActionTarget
        {
            public string Type { get; set; }
            public string Identifier { get; set; }
        }
    }
}
