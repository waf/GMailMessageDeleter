using Google.Apis.Gmail.v1.Data;
using Spectre.Console;

namespace GMailMessageDeleter
{
    internal static class UI
    {
        internal const string FilterByLabelOption = "Delete messages matching a label";
        internal const string FilterBySearchQueryOption = "Delete messages matching a search query";
        internal const string ConfirmDeletionOption = "Yes";
        internal const string AbortDeletionOption = "No";

        internal static MultiSelectionPrompt<string> BuildFilterSelectionPrompt()
        {
            return new MultiSelectionPrompt<string>()
                .Title("How would you like to select the messages to delete?" + Environment.NewLine + "You may select multiple options to delete messages that have multiple tags.")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle an option, [green]<enter>[/] to accept)[/]")
                .AddChoices(new[]
                {
                    FilterByLabelOption,
                    FilterBySearchQueryOption
                });
        }

        internal static MultiSelectionPrompt<string> BuildLabelPrompt(List<string> labelNames)
        {
            return new MultiSelectionPrompt<string>()
                .PageSize(12)
                .Title("Select label(s) to delete")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle a label, [green]<enter>[/] to accept)[/]")
                .MoreChoicesText("[grey](Move up and down to reveal more labels)[/]")
                .AddChoices(labelNames.OrderBy(i => i));
        }

        internal static SelectionPrompt<string> BuildDeletionConfirmationPrompt()
        {
            return new SelectionPrompt<string>()
                .Title("Start deleting?")
                .AddChoices(new[] { ConfirmDeletionOption, AbortDeletionOption });
        }

        internal static void UpdateStatusMessage(StatusContext ctx, int totalDeleted)
        {
            ctx.Spinner(Spinner.Known.BouncingBar);
            ctx.Status($"[bold blue]Total deleted so far: {totalDeleted} messages[/]");
        }

        internal static void PrintLogMessage(int deleteCount)
        {
            AnsiConsole.MarkupLine($"[grey]{DateTime.Now:s}:[/] Deleting [green]{deleteCount}[/] messages[grey]...[/]");
        }

        internal static void PrintDeletionPlan(IReadOnlyCollection<Label>? labels, string? searchQuery)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine("After confirming, the messages that match all of the following conditions will be deleted.");
            AnsiConsole.WriteLine();

            bool hasLabels = labels is not null && labels.Any();
            bool hasSearchQuery = !string.IsNullOrEmpty(searchQuery);
            if (hasLabels)
                AnsiConsole.WriteLine("Labels: " + string.Join(", ", labels.Select(l => l.Name)));
            if (hasSearchQuery)
                AnsiConsole.WriteLine("Search Query: " + searchQuery);

            if(!hasLabels && !hasSearchQuery)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine("[red]Warning! No filters are specified; continuing will delete ALL messages in the current account.[/]");
            }

            AnsiConsole.WriteLine();
        }
    }
}
