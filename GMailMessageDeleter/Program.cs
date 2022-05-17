using Google.Apis.Gmail.v1.Data;
using Spectre.Console;

namespace GMailMessageDeleter
{
    /// <summary>
    /// Auth Setup:
    /// 1. On https://console.cloud.google.com/ for your organization, create or reuse an existing project
    /// 2. Under Library, ensure that gmail is enabled
    /// 3. Configure your oauth consent screen (internal is fine)
    /// 4. Ensure that the https://mail.google.com/ oauth scope is added
    /// 5. Go to Credentials, and add an oauth client of type "Desktop app"
    /// 6. Download the JSON file for the oauth client, and name it as the filename inside the OAuthFileName constant below.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            const string OAuthFileName = "oauth-client-credentials.json";
            if (!File.Exists(OAuthFileName))
            {
                Console.WriteLine($"Could not find {OAuthFileName} in the working directory. Please download this file from the \"OAuth 2.0 Client ID\" section of https://console.cloud.google.com/apis/credentials (you may need to ask your Google Workspace administrator).");
                return;
            }
            var client = new GMailClient(OAuthFileName);
            var user = client.GetLoggedInUser();

            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine($"Logged in as user: {user}");
            AnsiConsole.WriteLine();

            while(true)
            {
                var filters = AnsiConsole.Prompt(UI.BuildFilterSelectionPrompt());
                var labels = PromptForLabelsToDelete(client, filters);
                var searchQuery = PromptForSearchQueryToDelete(filters);

                if (RunSearchDeleteLoop(client, labels, searchQuery)) // false if user cancels, prompt again.
                    break;
            }

            AnsiConsole.WriteLine("Successfully deleted messages.");
        }

        private static IReadOnlyCollection<Label>? PromptForLabelsToDelete(GMailClient client, List<string> filters)
        {
            if (!filters.Contains(UI.FilterByLabelOption))
            {
                return null;
            }

            IList<Label> labels = client.GetLabelsInAccount();

            var selectedLabels = AnsiConsole.Prompt(
                UI.BuildLabelPrompt(labels.Select(l => l.Name).ToList())
            );

            return labels.Where(l => selectedLabels.Contains(l.Name)).ToList();
        }

        private static string? PromptForSearchQueryToDelete(List<string> filters)
        {
            if (!filters.Contains(UI.FilterBySearchQueryOption))
            {
                return null;
            }

            AnsiConsole.WriteLine("Please enter a search query. The syntax here matches the search box on gmail.com.");
            AnsiConsole.WriteLine("It is strongly recommended to test your query in the search box before entering it here!");
            AnsiConsole.Write("Enter Query: ");
            return Console.ReadLine()?.Trim();
        }

        private static bool RunSearchDeleteLoop(GMailClient client, IReadOnlyCollection<Label>? labels, string? searchQuery)
        {
            UI.PrintDeletionPlan(labels, searchQuery);

            var shouldContinue = AnsiConsole.Prompt(
                UI.BuildDeletionConfirmationPrompt()
            );

            if (shouldContinue.Equals(UI.AbortDeletionOption))
                return false;

            int totalDeleted = 0;
            var success = AnsiConsole.Status()
                .AutoRefresh(true)
                .Spinner(Spinner.Known.Default)
                .SpinnerStyle(Style.Plain)
                .Start("[bold blue]Starting to delete...[/]", ctx =>
                {
                    string? pageToken = null;
                    // search for messages and then delete them in batches. The Search API returns results page by page, with
                    // each page containing 500 results. We can batch delete 1000 results, so we delete up to two pages at a time.
                    while (true)
                    {
                        var searchResult = client.FindMessagesToDelete(labels, searchQuery, pageToken);

                        UI.PrintLogMessage(searchResult.Messages.Count);
                        DeleteMessages(client, searchResult);
                        totalDeleted += searchResult.Messages.Count;

                        UI.UpdateStatusMessage(ctx, totalDeleted);

                        if (searchResult.NextPageToken is null) // all messages deleted!
                            return true;

                        pageToken = searchResult.NextPageToken;
                    }
                });

            AnsiConsole.MarkupLine($"[bold blue]Done! Deleted {totalDeleted} messages[/]");
            return success;
        }

        private static void DeleteMessages(GMailClient client, ListMessagesResponse searchResult, int backOffMilliseconds = 1000)
        {
            try
            {
                client.DeleteMessages(searchResult);
            }
            catch (Exception ex)
            {
                var message = ex.Message.Replace("\r", "").Replace("\n", "").Replace("[", " ").Replace("]", " ");
                AnsiConsole.MarkupLine($"[red]{DateTime.Now:s}: Received error:[/] {message}");
                System.Threading.Thread.Sleep(backOffMilliseconds);
                DeleteMessages(client, searchResult, backOffMilliseconds * 2);
            }
        }
    }
}
