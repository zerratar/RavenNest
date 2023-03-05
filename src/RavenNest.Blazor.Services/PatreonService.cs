using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RavenNest.BusinessLogic;
using RavenNest.BusinessLogic.Data;
using RavenNest.BusinessLogic.Game;
using RavenNest.BusinessLogic.Models.Patreon.API;
using RavenNest.DataModels;
using RavenNest.Sessions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace RavenNest.Blazor.Services
{
    public class PatreonService : RavenNestService
    {
        public readonly string[] AvailableLanguages = new string[] {
            "Afrikaans", "Albanian", "Amharic", "Arabic", "Armenian", "Azerbaijani", "Basque", "Belarusian", "Bengali", "Bosnian", "Bulgarian", "Burmese", "Catalan",
            "Cebuano", "Chichewa", "Chinese (Simplified)", "Chinese (Traditional)", "Corsican", "Croatian", "Czech", "Danish", "Dutch", "English", "Esperanto",
            "Estonian", "Filipino", "Finnish", "French", "Frisian", "Galician", "Georgian", "German", "Greek", "Gujarati", "Haitian Creole", "Hausa", "Hawaiian",
            "Hebrew", "Hindi", "Hmong", "Hungarian", "Icelandic", "Igbo", "Indonesian", "Irish", "Italian", "Japanese", "Javanese", "Kannada", "Kazakh", "Khmer",
            "Kinyarwanda", "Korean", "Kurdish (Kurmanji)", "Kyrgyz", "Lao", "Latin", "Latvian", "Lithuanian", "Luxembourgish", "Macedonian", "Malagasy", "Malay",
            "Malayalam", "Maltese", "Maori", "Marathi", "Mongolian", "Myanmar (Burmese)", "Nepali", "Norwegian", "Odia (Oriya)", "Pashto", "Persian", "Polish",
            "Portuguese", "Punjabi", "Romanian", "Russian", "Samoan", "Scots Gaelic", "Serbian", "Sesotho", "Shona", "Sindhi", "Sinhala (Sinhalese)", "Slovak",
            "Slovenian", "Somali", "Spanish", "Sundanese", "Swahili", "Swedish", "Tajik", "Tamil", "Tatar", "Telugu", "Thai", "Turkish", "Turkmen", "Ukrainian",
            "Urdu", "Uyghur", "Uzbek", "Vietnamese", "Welsh", "Xhosa", "Yiddish", "Yoruba", "Zulu"
        };

        private readonly ILogger<AuthService> logger;
        private readonly IRavenBotApiClient ravenbotApi;
        private readonly IPatreonManager patreonManager;
        private readonly IGameData gameData;
        private readonly IAuthManager authManager;
        private readonly IPlayerManager playerManager;
        private readonly LogoService logoService;
        private PatreonSettings patreon;


        public PatreonService(
            ILogger<AuthService> logger,
            IRavenBotApiClient ravenbotApi,
            IPatreonManager patreonManager,
            IGameData gameData,
            IAuthManager authManager,
            IPlayerManager playerManager,
            IHttpContextAccessor accessor,
            ISessionInfoProvider sessionInfoProvider,
            LogoService logoService)
            : base(accessor, sessionInfoProvider)
        {
            this.logger = logger;
            this.ravenbotApi = ravenbotApi;
            this.patreonManager = patreonManager;
            this.gameData = gameData;
            this.authManager = authManager;
            this.playerManager = playerManager;
            this.logoService = logoService;

            var data = (this.gameData as GameData);
            this.patreon = data.Patreon;
        }

        public Task UnlinkAsync()
        {
            return Task.Run(() =>
            {
                var session = GetSession();
                patreonManager.Unlink(session);
            });
        }

        public Task<UserPatreon> LinkAsync(string code)
        {
            var session = GetSession();
            return patreonManager.LinkAsync(session, code);
        }
        public Task<PatreonTier> GetPatreonTierAsync(int tierLevel)
        {
            return patreonManager.GetTierByLevelAsync(tierLevel);
        }

        public (string, bool) GetChatbotSettings()
        {
            var session = GetSession();
            var uid = session.AccountId;
            // IRavenBotApiClient    

            var language = gameData.GetUserProperty(uid, UserProperties.ChatBotLanguage, "None");
            var transformationStr = gameData.GetUserProperty(uid, UserProperties.ChatMessageTransformation, "0");
            int.TryParse(transformationStr, out var value);
            var transformation = (ChatMessageTransformation)value;

            var personalized = transformation == ChatMessageTransformation.Personalize || transformation == ChatMessageTransformation.TranslateAndPersonalize;
            return (language, personalized);
        }

        public async Task SaveChatbotSettingsAsync(string language, bool usePersonalizedMessages)
        {
            // set the values in the user settings
            var session = GetSession();
            var uid = session.AccountId;
            // IRavenBotApiClient    

            var transformation = ChatMessageTransformation.Standard;
            var useTranslation = AvailableLanguages.IndexOf(language) != -1;
            if (useTranslation && usePersonalizedMessages)
            {
                transformation = ChatMessageTransformation.TranslateAndPersonalize;
            }
            else if (useTranslation)
            {
                transformation = ChatMessageTransformation.Translate;
            }
            else if (usePersonalizedMessages)
            {
                transformation = ChatMessageTransformation.Personalize;
            }

            var transformationString = ((int)transformation).ToString();
            gameData.SetUserProperty(uid, UserProperties.ChatBotLanguage, language);
            gameData.SetUserProperty(uid, UserProperties.ChatMessageTransformation, transformationString);

            await ravenbotApi.SendUserSettingAsync(session.UserId, UserProperties.ChatBotLanguage, language);
            await ravenbotApi.SendUserSettingAsync(session.UserId, UserProperties.ChatMessageTransformation, transformationString);
        }

        public string GetPatreonLoginUrl()
        {
            var stateParametersList = new List<RavenNest.Models.StateParameters>();
            var scopes = new List<string>();

            scopes.Add(WebUtility.UrlEncode("identity"));
            scopes.Add(WebUtility.UrlEncode("identity[email]"));
            scopes.Add(WebUtility.UrlEncode("identity.memberships"));


            // The following is a super ugly hack, this is due to an issue in Patreon API
            // if the creator logs in, a new access token is generated and it overwrites the creator's token.
            // if this is using the creator account, the access token will be regenerated.
            // for the creators access token we need all permissions, add extra scopes if zerratar.
            var session = GetSession();
            if (session.UserName.Equals("zerratar", StringComparison.OrdinalIgnoreCase))
            {
                scopes.Add(WebUtility.UrlEncode("campaigns"));
                scopes.Add(WebUtility.UrlEncode("campaigns.members"));
                scopes.Add(WebUtility.UrlEncode("campaigns.members[email]"));
            }

            return $"https://www.patreon.com/oauth2/authorize?client_id={patreon.ClientId}"
                    + "&response_type=code&scope=" + String.Join("+", scopes)
                    + "&state=" + EncodeState(stateParametersList)
                    + $"&redirect_uri=" + patreonManager.GetRedirectUrl();
        }

        public string EncodeState(List<RavenNest.Models.StateParameters> stateParameters)
        {
            return authManager.GetRandomizedBase64EncodedStateParameters(stateParameters);
        }
    }
}
