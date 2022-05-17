using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace GMailMessageDeleter
{
    /// <summary>
    /// Wraps the <see cref="GmailService"/> from the Google.Apis.Gmail.v1 nuget package to provide
    /// higher level operations for finding/deleting messages.
    /// </summary>
    internal class GMailClient
    {
        private const string ApplicationName = "Gmail Message Deleter";
        private static readonly string[] Scopes = { GmailService.Scope.MailGoogleCom };

        private readonly GmailService service;

        public GMailClient(string credentialFileName)
        {
            UserCredential credential;

            using (var stream = new FileStream(credentialFileName, FileMode.Open, FileAccess.Read))
            {
                // The file token.json stores the user's access and refresh tokens, and is created
                // automatically when the authorization flow completes for the first time.
                string credPath = "token.json";
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
            }

            // Create Gmail API service.
            this.service = new GmailService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ApplicationName,
            });
        }

        public IList<Label> GetLabelsInAccount()
        {
            var request = service.Users.Labels.List("me");
            return request.Execute().Labels;
        }

        public string GetLoggedInUser()
        {
            return service.Users.GetProfile("me").Execute().EmailAddress;
        }

        public ListMessagesResponse FindMessagesToDelete(IReadOnlyCollection<Label>? labels, string? searchQuery, string? pageToken)
        {
            ListMessagesResponse searchResult = SearchForMessages(labels, searchQuery, pageToken);
            if (searchResult.NextPageToken is not null) // retrieve next page, if it exists.
            {
                var searchResultNextPage = SearchForMessages(labels, searchQuery, searchResult.NextPageToken);
                searchResult.Messages = searchResult.Messages.Concat(searchResultNextPage.Messages).ToList();
                searchResult.NextPageToken = searchResultNextPage.NextPageToken;
            }
            searchResult.Messages ??= Array.Empty<Message>();

            return searchResult;
        }

        public ListMessagesResponse SearchForMessages(IReadOnlyCollection<Label>? labels, string? searchQuery, string? pageToken)
        {
            var search = service.Users.Messages.List("me");
            search.MaxResults = 500;
            search.PageToken = pageToken;
            search.Q = searchQuery;
            search.LabelIds = labels?.Select(l => l.Id).ToArray() ?? Array.Empty<string>();
            return search.Execute();

        }

        public void DeleteMessages(ListMessagesResponse searchResult)
        {
            if (searchResult.Messages.Count == 0)
                return;

            var messageIds = searchResult.Messages.Select(m => m.Id).ToArray();
            var deleteRequest = service.Users.Messages.BatchDelete(new BatchDeleteMessagesRequest
            {
                Ids = messageIds
            }, "me");
            deleteRequest.Execute();
        }
    }
}
