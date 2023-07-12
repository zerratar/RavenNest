using RavenNest.BusinessLogic.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace RavenNest.BusinessLogic.OpenAI.Conversations
{
    public class AIConversationManager
    {
        private static readonly TimeSpan RetentionTime = TimeSpan.FromDays(30);

        private readonly ConcurrentDictionary<Guid, AIConversation> conversationsById = new();
        private readonly ConcurrentDictionary<Guid, List<AIConversation>> conversationsByUserId = new();
        private readonly ConcurrentDictionary<Guid, string> conversationsFilePathByUserId = new();
        private readonly GameData gameData;

        public AIConversationManager(GameData gameData)
        {
            this.gameData = gameData;
            LoadConversations();
        }

        /// <summary>
        ///     Gets all available conversations for a given user.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public IReadOnlyList<AIConversation> GetAll(Guid userId)
        {
            if (!conversationsByUserId.TryGetValue(userId, out var conversations))
            {
                conversationsByUserId[userId] = conversations = new List<AIConversation>();
            }

            if (conversations.Count > 0)
            {
                // make sure we remove old conversations older than a certain date
                var utcNow = DateTime.UtcNow;
                var convoCopy = conversations.ToArray();
                foreach (var c in convoCopy)
                {
                    var activity = c.GetLastActivity();
                    if (utcNow - activity >= RetentionTime)
                    {
                        conversations.Remove(c);
                        continue;
                    }
                }
            }
            return conversations;
        }

        /// <summary>
        ///    Gets a conversation by its id. Returns null if no conversation with the same ID exists.
        /// </summary>
        /// <param name="conversationId"></param>
        /// <returns></returns>
        public AIConversation Get(Guid conversationId)
        {
            if (conversationsById.TryGetValue(conversationId, out var conversation))
                return conversation;

            return null;
        }

        /// <summary>
        ///     Removes all conversations for a given user.
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public bool RemoveAll(Guid uid)
        {
            var success = conversationsByUserId.TryRemove(uid, out _);

            if (success)
            {
                DeleteConversationsJson(uid);
            }

            return success;
        }

        /// <summary>
        ///     Clears all messages in a given conversation specified by provided conversationId and saves the conversation to disk.
        /// </summary>
        /// <param name="conversationId"></param>
        public AIConversation ClearConversation(Guid conversationId)
        {
            var conversation = Get(conversationId);
            if (conversation != null)
            {
                conversation.Messages.Clear();
                SaveConversationsJson(conversation.UserId);
                return conversation;
            }

            return null;
        }

        /// <summary>
        /// Clears all messages in the provided conversation and saves the conversation to disk.
        /// </summary>
        /// <param name="conversation"></param>
        /// <returns></returns>
        public AIConversation ClearConversation(AIConversation conversation)
        {
            if (conversation != null)
            {
                conversation.Messages.Clear();
                SaveConversationsJson(conversation.UserId);
                return conversation;
            }

            return null;
        }

        /// <summary>
        ///     Clears the latest conversation for the user from messages.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public bool ClearCurrentConversation(Guid userId)
        {
            if (!TryGetLatest(userId, out var convo))
                return false;

            convo.Messages.Clear();

            SaveConversationsJson(userId);
            return true;
        }

        /// <summary>
        /// Try to get the latest conversation for the user. Returns false if no conversation exists.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="conversation"></param>
        /// <returns></returns>
        public bool TryGetLatest(Guid userId, out AIConversation conversation)
        {
            var conversations = GetAll(userId) as List<AIConversation>;
            conversation = conversations.OrderByDescending(x => x.GetLastActivity()).FirstOrDefault();
            return conversation != null;
        }

        /// <summary>
        ///     Gets the latest conversation or creates one if none exists.
        ///     This will save the conversation to disk if it was created.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public AIConversation GetLatestOrCreate(Guid userId)
        {
            var conversations = GetAll(userId) as List<AIConversation>;
            var conversation = conversations.OrderByDescending(x => x.GetLastActivity()).FirstOrDefault();
            if (conversation == null)
            {
                conversation = new AIConversation
                {
                    Id = Guid.NewGuid(),
                    Messages = new List<AIConversationMessage>(),
                    StartTime = DateTime.UtcNow,
                    UserId = userId,
                };
                conversation.Init(this);
                conversationsById[conversation.Id] = conversation;
                conversations.Add(conversation);
                SaveConversationsJson(userId, conversations);
            }


            return conversation;
        }

        private void DeleteConversationsJson(Guid userId)
        {
            if (!conversationsFilePathByUserId.TryGetValue(userId, out var path))
            {
                var user = gameData.GetUser(userId);
                path = GetMessageRepositoryFilePath(user);
                conversationsFilePathByUserId.TryRemove(userId, out _);
            }

            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
        }

        public void SaveConversationsJson(Guid userId)
        {
            if (!conversationsByUserId.TryGetValue(userId, out var conversations))
            {
                conversationsByUserId[userId] = conversations = new List<AIConversation>();
            }

            SaveConversationsJson(userId, conversations);
        }

        public void SaveConversationsJson(Guid userId, List<AIConversation> conversations)
        {
            if (!conversationsFilePathByUserId.TryGetValue(userId, out var path))
            {
                var user = gameData.GetUser(userId);
                path = GetMessageRepositoryFilePath(user);
                conversationsFilePathByUserId[userId] = path;
            }

            try
            {
                System.IO.File.WriteAllText(path,
                        Newtonsoft.Json.JsonConvert.SerializeObject(conversations)
                    );
            }
            catch
            {
                // ignored
            }
        }

        private void LoadConversations()
        {
            var folder = GetRepositoryFolder();
            var conversationRepos = System.IO.Directory.GetFiles(folder, "*.json", System.IO.SearchOption.AllDirectories);
            foreach (var userConversationPath in conversationRepos)
            {
                try
                {
                    var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<List<AIConversation>>(
                            System.IO.File.ReadAllText(userConversationPath)
                        );

                    if (loaded.Count > 0)
                    {
                        var uid = loaded[0].UserId;
                        conversationsByUserId[uid] = loaded;
                        conversationsFilePathByUserId[uid] = userConversationPath;

                        foreach (var conversation in loaded)
                        {
                            conversation.Init(this);
                            conversationsById[conversation.Id] = conversation;
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        private static string GetRepositoryFolder()
        {
            var folder = System.IO.Path.Combine(FolderPaths.GeneratedData, "ai-conversations");
            if (!System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
            }

            return folder;
        }

        private static string GetMessageRepositoryFilePath(DataModels.User user)
        {
            var folder = GetRepositoryFolder();
            var messagesFile = System.IO.Path.Combine(folder, user.UserName + ".json");
            return messagesFile;
        }
    }
}
